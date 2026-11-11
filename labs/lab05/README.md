# ЛР5 — CRUD, ACID і ролі

ЛР5 використовує застосунок з `labs/lab04/AutoPartsWarehouseLab4` і окремі тести з цієї папки. Для перенесення потрібні обидві папки `labs/lab04` та `labs/lab05` зі збереженням структури; переносити робочу SQLite-базу не потрібно. Потрібні .NET SDK 8 або сумісний новіший SDK **разом із .NET 8 ASP.NET Core runtime**, доступ до NuGet для першого відновлення пакетів і браузер. Команди нижче розраховані на macOS / Linux, `zsh` або `bash`, з кореня репозиторію.

## Демонстрація на окремій базі

Паролі придумайте самі: щонайменше 10 символів, великі та малі літери, цифра і спеціальний символ. Приховане введення не показує їх у терміналі. При кожному запуску команда створює нову тимчасову папку та нову базу; старі облікові записи і робоча база не змінюються.

```bash
DEMO_DIR=$(mktemp -d "${TMPDIR:-/tmp}/lab5-demo.XXXXXXXX")
read -s 'LAB4_ADMIN_PASSWORD?Пароль адміністратора: '; echo
read -s 'LAB4_MANAGER_PASSWORD?Пароль менеджера: '; echo
read -s 'LAB4_WORKER_PASSWORD?Пароль комірника: '; echo
export LAB4_ADMIN_PASSWORD LAB4_MANAGER_PASSWORD LAB4_WORKER_PASSWORD
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://127.0.0.1:5298
export ConnectionStrings__WarehouseContext="Data Source=${DEMO_DIR}/demo.db"
dotnet run --project labs/lab04/AutoPartsWarehouseLab4/AutoPartsWarehouseLab4.csproj --no-launch-profile
```

Синтаксис `read -s 'ЗМІННА?текст'` наведено для стандартного macOS **zsh**. У bash використайте `read -rs -p 'Пароль: ' LAB4_ADMIN_PASSWORD; echo` і так само для інших змінних. Відкрийте http://127.0.0.1:5298/ і увійдіть: `admin@lab4.local`, `manager@lab4.local`, `worker@lab4.local` відповідно з введеними паролями. Міграції й початкові ролі/облікові записи застосовуються під час першого запуску. Зупиніть програму Ctrl+C; у цьому ж терміналі виконайте `unset LAB4_ADMIN_PASSWORD LAB4_MANAGER_PASSWORD LAB4_WORKER_PASSWORD ConnectionStrings__WarehouseContext ASPNETCORE_ENVIRONMENT ASPNETCORE_URLS`. Тимчасова база залишається в папці, шлях до якої виведе `echo "$DEMO_DIR"`.

## Тести на ізольованих копіях

Тест ЛР4 у присутності реальної БД створює її копію для перевірки міграцій. Щоб взагалі не читати робочу БД, скопіюйте код без неї; тест на копії реальної бази у такому запуску повертається без перевірки, хоча xUnit зараховує його як пройдений.

```bash
TEST_DIR=$(mktemp -d "${TMPDIR:-/tmp}/lab5-tests.XXXXXXXX")
TEST_DIR=$(cd "$TEST_DIR" && pwd -P)
mkdir -p "$TEST_DIR/labs/lab04" "$TEST_DIR/labs/lab05"
rsync -a --exclude='*.db*' --exclude=bin --exclude=obj labs/lab04/AutoPartsWarehouseLab4 labs/lab04/AutoPartsWarehouseLab4.Tests "$TEST_DIR/labs/lab04/"
rsync -a --exclude=bin --exclude=obj labs/lab05/AutoPartsWarehouseLab5.Tests "$TEST_DIR/labs/lab05/"
dotnet test "$TEST_DIR/labs/lab04/AutoPartsWarehouseLab4.Tests/AutoPartsWarehouseLab4.Tests.csproj"
dotnet test "$TEST_DIR/labs/lab05/AutoPartsWarehouseLab5.Tests/AutoPartsWarehouseLab5.Tests.csproj"
```

У ЛР5 перевірено серверну валідацію, обмеження зовнішніх ключів, паралельне повернення, rollback коригування та читання після COMMIT через нове фізичне з'єднання. Ці сценарії не є доказом роботи всіх можливих операцій або стійкості до відключення живлення.
