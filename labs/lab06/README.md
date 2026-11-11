# ЛР6 — Паралельна обробка даних

Самостійна лабораторна робота, що не залежить від файлів ЛР1–5. Застосунок — повна копія вебсистеми складу автозапчастин із ЛР5, перенесена в окремий проєкт зі своєю базою SQLite. До неї додано окремий консольний проєкт `AutoPartsWarehouseLab6.Benchmarks`, який порівнює чотири способи виконання одного й того самого запиту до бази.

## Що робить лабораторна

Бізнес-запит: пошук запчастин, які потрібно поповнити —

```csharp
part.QuantityInStock <= part.MinimumStock
```

Запит виконується чотирма способами, і для кожного вимірюється реальний час виконання над SQLite (EF Core) на базі з 20 записами та на базі з ~100 000 записами:

| Спосіб | Технологія |
|---|---|
| Sequential | один звичайний запит EF Core, без паралелізму |
| Manual Threads | `System.Threading.Thread`, по одному на діапазон Id, кожен зі своїм `DbContext` |
| TPL | `Parallel.ForEach` з явним `MaxDegreeOfParallelism`, кожен виконавець зі своїм `DbContext` |
| Multi-core / PLINQ | PLINQ (`AsParallel().WithDegreeOfParallelism(4)`), кожна партія зі своїм `DbContext` |

Усі чотири способи читають дані з SQLite окремо для кожного діапазону Id — жоден не фільтрує вже завантажений у пам'ять масив.

## Технології

ASP.NET Core 8 MVC, Entity Framework Core (SQLite), ASP.NET Core Identity — застосунок (як у ЛР5). `System.Threading.Thread`, Task Parallel Library (`Parallel.ForEach`), PLINQ, `System.Diagnostics.Stopwatch` — benchmark-проєкт.

## Структура

```text
lab06/
  AutoPartsWarehouseLab6/              веб-застосунок (копія ЛР5, своя база AutoPartsWarehouseLab6.db)
  AutoPartsWarehouseLab6.Benchmarks/   консольний інструмент вимірювання
    DataGenerator.cs                  генерація тестових баз (20 і ~100000 записів)
    QueryService.cs                   чотири стратегії вибірки
    BenchmarkRunner.cs                Stopwatch, медіана, CSV, таблиця в консолі
    SelfTest.cs                       автоматичні перевірки коректності
    Program.cs                        CLI
    bench-data/                       results.csv з вимірами (згенеровані бази не комітяться — див. .gitignore)
```

## Команди

Усі команди виконуються з `labs/lab06/AutoPartsWarehouseLab6.Benchmarks/`, у конфігурації Release:

```bash
dotnet build -c Release
```

Створити базу на 20 записів:

```bash
dotnet run -c Release --no-build -- generate-small
```

Створити базу на ~100 000 записів (займає кілька секунд):

```bash
dotnet run -c Release --no-build -- generate-large
```

Запустити порівняння всіх чотирьох способів на обох базах (виводить таблицю в консоль і зберігає `bench-data/results.csv`):

```bash
dotnet run -c Release --no-build -- benchmark
```

Автоматичні перевірки коректності (однакові результати в усіх чотирьох способів, порожня таблиця, відсутність дублікатів при повторній генерації, незміненість бази ЛР5):

```bash
dotnet run -c Release --no-build -- selftest
```

Запустити сам вебзастосунок ЛР6 (окремо від benchmark-проєкту, зі своєю базою):

```bash
cd ../AutoPartsWarehouseLab6
dotnet run
```

## Генерація даних

`DataGenerator.Generate(dbPath, rowCount)` щоразу видаляє стару базу за цим шляхом і створює нову з фіксованим seed (`20260601`), тому повторний запуск ніколи не накопичує дублікатів і дає відтворюваний набір даних. Унікальність пари (Name, Manufacturer), яку вимагає унікальний індекс моделі AutoPart, забезпечується порядковим номером у назві запчастини. Приблизно 40% рядків навмисно отримують `QuantityInStock <= MinimumStock` (критичний залишок), решта — безпечний запас; вставлення пакетне (батчі по 5000 рядків в одній транзакції).

## Результати вимірювань

Реальний запуск (Release, 8 логічних процесорів, DOP = 4, медіана з 9 повторів після 3 розігрівальних викликів):

| Спосіб | 20 записів, мс | 100 000 записів, мс | Прискорення на 100 000 |
|---|---:|---:|---:|
| Sequential | 0.76 | 18.04 | 1.00x |
| Manual Threads | 153.43 | 12.63 | 1.43x |
| TPL | 9.41 | 19.28 | 0.94x |
| Multi-core / PLINQ | 3.25 | 11.82 | 1.53x |

Повні сирі дані (усі 9 повторів на кожен спосіб) — у `AutoPartsWarehouseLab6.Benchmarks/bench-data/results.csv` після запуску `benchmark`. Пояснення цих цифр, зокрема їхньої нестабільності між запусками, і чому паралельні способи тут здебільшого не випереджають послідовний — у [docs/defense/LAB6_DEFENSE.md](docs/defense/LAB6_DEFENSE.md).
