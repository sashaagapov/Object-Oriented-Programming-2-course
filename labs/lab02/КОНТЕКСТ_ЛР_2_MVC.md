# ЛР №2 MVC — повний контекст і план реалізації

## 1. Що саме треба здати

**Тема:** розробка застосунку за шаблоном MVC.

**Обов'язкове завдання методички:** створити вебзастосунок ASP.NET MVC; на основі матеріалів попередньої лабораторної розробити модель даних, подання та контролери; для БД застосувати **Entity Framework Code First**.

Для цієї роботи логічно продовжити ЛР №1: **автоматизована система «Склад запчастин для авто»**. Її UML-модель уже містить предметні сутності, ролі та сценарії, тож не потрібно переносити приклад «BookStore» із презентації буквально. Книжковий магазин є лише навчальним зразком структури MVC + EF.

### Мінімальний функціональний результат

Застосунок має дозволяти щонайменше:

1. Переглядати список запчастин, шукати їх за назвою або виробником.
2. Відкривати картку запчастини та бачити залишок, мінімальний залишок, виробника й сумісні моделі авто.
3. Створювати або редагувати запчастину.
4. Оформляти видачу: вказати кількість; система зменшує залишок і не дозволяє видати більше, ніж є.
5. Переглядати критичні позиції: `QuantityInStock <= MinimumStock`.
6. Створювати замовлення постачальнику та приймати поставку, збільшуючи залишок.

Пункти 1–3 достатньо демонструють CRUD і MVC. Пункти 4–6 безпосередньо реалізують activity/state/use-case діаграми з ЛР №1 і роблять роботу цілісною.

## 2. Джерела, які вже проаналізовано

| Файл | Що з нього потрібно взяти |
|---|---|
| `ЛР_2_MVC.docx` | Формальне завдання, HTTP, GET/POST, контролери, EF, три підходи до БД, Code First |
| `Лекція_5_ASP_Net_MVC_ч1.pptx` | Покроковий приклад MVC 5 + EF6: модель, `DbContext`, рядок з'єднання, ініціалізація, контролер, Razor-в'ю, маршрути, GET/POST, layout |
| `ЛР_1_ООП_Агапов.docx` | Власна предметна область і пояснення UML-моделі |
| `auto_parts_warehouse_*.drawio` | Класи, ролі та сценарії для перенесення в вебзастосунок |

## 3. Важлива технологічна межа

Матеріали написані для **ASP.NET MVC 5 на .NET Framework**: це видно з `System.Web.Mvc`, `Global.asax`, `web.config`, `System.Data.Entity` та LocalDB `.mdf`. Це не той самий стек, що ASP.NET Core MVC.

Для повної відповідності презентації треба обрати в Visual Studio шаблон **ASP.NET Web Application (.NET Framework) → MVC**, а в NuGet встановити **EntityFramework 6.x**. Тоді використовуються `DbContext`, `DbSet<T>`, `Global.asax` і `web.config` саме в тому вигляді, який показано на лекції.

Якщо встановлена лише сучасна версія Visual Studio / .NET SDK і створюється ASP.NET Core MVC, архітектура MVC залишиться правильною, але код конфігурації БД, маршрутизації та запуску буде іншим. Не можна змішувати `System.Web.Mvc` із ASP.NET Core.

## 4. MVC простими словами і прив'язка до нашого проєкту

```text
Браузер
  │  HTTP GET /Parts або POST /Parts/Issue
  ▼
Маршрутизація → PartsController
  │                  │
  │                  ├─ читає/змінює дані через WarehouseContext (EF)
  │                  └─ готує ViewModel
  ▼
Razor View (.cshtml) → HTML-відповідь → браузер
```

| Частина | Відповідальність | Приклад у «Складі запчастин» |
|---|---|---|
| **Model** | Дані та бізнес-правила | `AutoPart`, `Supplier`, `PurchaseOrder`, `WarehouseContext`; перевірка доступного залишку |
| **View** | Інтерфейс і відображення | таблиця запчастин, форма видачі, список критичних залишків |
| **Controller** | Приймає HTTP-запит, координує Model і View | `PartsController.Index`, `PartsController.Issue` |

Контролер не повинен містити HTML-розмітку, а подання не має напряму працювати з БД. Це і є практичний сенс розділення MVC.

## 5. HTTP, URL і методи запиту

HTTP працює за моделлю «запит — відповідь»: браузер передає метод, URI, версію протоколу, заголовки та, за потреби, тіло; сервер повертає статус, заголовки й тіло відповіді. Стартовий рядок має форму:

```http
GET /Parts/Details/12 HTTP/1.1
Host: localhost:12345
```

Перший рядок відповіді має форму `HTTP/Версія КодСтану Пояснення`. У методичці наведені приклади `201 Created`, `401 Unauthorized`, `507 Insufficient Storage`.

### GET та POST у цій роботі

| Операція | Метод | Чому |
|---|---|---|
| Список або пошук запчастин | `GET /Parts?query=filter` | Лише читає дані; параметр може бути в URL |
| Відкрити форму створення / редагування / видачі | `GET` | Повертає HTML-форму |
| Створити запчастину | `POST /Parts/Create` | Змінює стан БД |
| Зберегти редагування | `POST /Parts/Edit` | Змінює стан БД |
| Видати запчастину | `POST /Parts/Issue` | Зменшує залишок |
| Прийняти поставку | `POST /Orders/Receive` | Збільшує залишок і змінює статус замовлення |

`GET` передає параметри в URL, може потрапити в історію браузера та закладки, має обмеження довжини й призначений для отримання даних. `POST` передає поля форми в тілі запиту, не призначений для закладок і застосовується для зміни даних. POST сам по собі не шифрує дані: конфіденційність забезпечує HTTPS; однак паролі та інші чутливі дані не можна надсилати GET-запитом.

Після успішного POST варто повертати `RedirectToAction("Index")` або перенаправляти на картку створеного об'єкта. Так повторне оновлення сторінки не створить операцію повторно.

## 6. Маршрутизація і контролери

За стандартною угодою MVC 5 URL має вигляд `/{controller}/{action}/{id}`. Тому:

```text
/Parts                 → PartsController.Index()
/Parts/Details/12      → PartsController.Details(int id)
/Parts/Issue/12        → PartsController.Issue(int id) [GET]
POST /Parts/Issue      → PartsController.Issue(IssuePartViewModel model) [POST]
```

Назва контролера закінчується на `Controller`, а в URL використовується назва без цього суфікса. Методи-дії мають бути `public`. Від класу `Controller` краще успадковуватися у звичайному застосунку, бо тоді доступні `View()`, `RedirectToAction()`, `ModelState`, `HttpNotFound()` тощо.

У презентації є і приклад прямої реалізації `IController` з одним методом `Execute(RequestContext)`. Він корисний для розуміння request/response, але не потрібний для цієї ЛР: він обробляє всі запити одним методом і позбавляє зручної системи action-методів та view.

## 7. Що перенести з UML ЛР №1 у модель даних

У діаграмі класів є `Запчастина`, `Склад`, `Постачальник`, `МодельАвто`, абстрактний `Працівник` з нащадками `Комірник` і `Менеджер`, а також `Замовлення`. Для першої MVC-версії не варто бездумно переносити всі методи класів у таблиці. Потрібно зберегти дані, необхідні сценаріям, і реалізувати ключові правила.

### Рекомендована модель EF

| C# клас / таблиця | Мінімальні поля | Зв'язки |
|---|---|---|
| `AutoPart` | `Id`, `Name`, `Manufacturer`, `QuantityInStock`, `MinimumStock` | багато-до-багатьох із `VehicleModel`; один-до-багатьох із `PurchaseOrder` |
| `VehicleModel` | `Id`, `Brand`, `ModelName`, `ProductionYear` | багато-до-багатьох із `AutoPart` |
| `Supplier` | `Id`, `Name`, `Phone` | один постачальник має багато замовлень |
| `PurchaseOrder` | `Id`, `OrderedAt`, `Quantity`, `Status`, `AutoPartId`, `SupplierId` | належить одній запчастині й одному постачальнику |
| `Warehouse` *(опційно)* | `Id`, `Name`, `Address` | для ЛР достатньо одного запису або зв'язку з `AutoPart` |
| `Employee` / `Manager` / `Storekeeper` *(опційно)* | `Id`, `FullName`, спеціальні поля | додаються, якщо потрібна повна відповідність діаграмі ролей |

`Id` відповідає угоді Code First і стане первинним ключем. EF також розпізнає назву виду `AutoPartId`. Додавати вручну `[Key]` тут не обов'язково.

Не варто зберігати стан запчастини окремим текстовим полем (`«в наявності»`, `«критичний залишок»`, `«немає»`): він однозначно обчислюється з двох чисел. У в'ю його можна показати так:

```csharp
public bool IsOutOfStock => QuantityInStock == 0;
public bool IsLowStock => QuantityInStock <= MinimumStock;
```

Це прямо відповідає state-діаграмі ЛР №1 і не допускає суперечливих даних.

### Мінімальний клас запчастини

```csharp
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AutoPartsWarehouse.Models
{
    public class AutoPart
    {
        public int Id { get; set; }

        [Required, StringLength(120)]
        public string Name { get; set; }

        [Required, StringLength(100)]
        public string Manufacturer { get; set; }

        [Range(0, int.MaxValue)]
        public int QuantityInStock { get; set; }

        [Range(0, int.MaxValue)]
        public int MinimumStock { get; set; }

        public virtual ICollection<VehicleModel> CompatibleVehicleModels { get; set; }
            = new List<VehicleModel>();
    }
}
```

У класі `PurchaseOrder` потрібно мати зовнішні ключі і навігаційні властивості, наприклад `AutoPartId` + `AutoPart`, `SupplierId` + `Supplier`. Якщо замовлення може містити кілька різних запчастин, правильна нормалізована модель — `PurchaseOrder` + `PurchaseOrderItem`. Для мінімальної ЛР припустимий один рядок замовлення, як у вихідній UML-діаграмі.

## 8. Entity Framework і Code First

Entity Framework (у матеріалах — EF6) є ORM: код працює з C#-об'єктами й `DbSet<T>`, а EF відображає їх на таблиці, ключі та зв'язки БД. Для вибірок використовується LINQ.

Методичка розрізняє:

| Підхід | Послідовність |
|---|---|
| Database First | спочатку існує БД, EF генерує класи за схемою |
| Model First | спочатку моделюється схема, потім EF створює БД |
| **Code First** | спочатку пишуться C# класи, потім EF створює БД і таблиці |

Для ЛР №2 потрібен останній варіант. Обов'язкові складові: класи сутностей, клас контексту, `DbSet<T>` для таблиць, рядок підключення й (за потреби) початкові дані.

### Контекст даних

```csharp
using System.Data.Entity;

namespace AutoPartsWarehouse.Models
{
    public class WarehouseContext : DbContext
    {
        public WarehouseContext() : base("WarehouseContext") { }

        public DbSet<AutoPart> AutoParts { get; set; }
        public DbSet<VehicleModel> VehicleModels { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    }
}
```

`DbContext` є посередником між моделлю та БД. Кожний `DbSet<T>` представляє набір сутностей, а отже — таблицю. Метод `SaveChanges()` фіксує зміни: до його виклику `Add`, зміна властивостей або `Remove` не записують дані остаточно.

У методичці трапляється `: Base("DbConnection")`; це друкарська помилка. У C# потрібно саме `: base("...")` з малої літери.

### Рядок з'єднання у `web.config`

Ім'я в атрибуті `name` має збігатися з аргументом `base("WarehouseContext")`:

```xml
<connectionStrings>
  <add name="WarehouseContext"
       connectionString="Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=|DataDirectory|\AutoPartsWarehouse.mdf;Integrated Security=True"
       providerName="System.Data.SqlClient" />
</connectionStrings>
```

У вебпроєкті використовується `web.config`; у консольному або desktop-проєкті — `App.config`. Слайди використовують LocalDB і файл БД у `App_Data`. Це зручно для демонстрації у Windows + Visual Studio. Якщо LocalDB у середовищі недоступна, не слід мовчки змішувати інший стек із прикладом: треба окремо узгодити інший провайдер БД.

### Початкові дані

У слайдах використаний `DropCreateDatabaseAlways<TContext>`, який щоразу видаляє й створює БД. Це підходить лише для демонстрації: користувацькі дані зникатимуть при кожному запуску.

Для розробки краще `DropCreateDatabaseIfModelChanges<WarehouseContext>` або міграції. Щоб показати приклад із лекції, можна створити `WarehouseDbInitializer`, додати кілька запчастин, постачальників і викликати:

```csharp
Database.SetInitializer(new WarehouseDbInitializer());
```

у `Application_Start()` у `Global.asax`. У готовій демонстрації треба розуміти, що зміна ініціалізатора впливає на збережені дані.

## 9. Рекомендована структура проєкту

```text
AutoPartsWarehouse/
├── App_Data/                         # LocalDB .mdf, якщо використано цей спосіб
├── Content/Site.css
├── Controllers/
│   ├── PartsController.cs
│   ├── SuppliersController.cs         # за наявності CRUD постачальників
│   └── OrdersController.cs
├── Models/
│   ├── AutoPart.cs
│   ├── VehicleModel.cs
│   ├── Supplier.cs
│   ├── PurchaseOrder.cs
│   ├── WarehouseContext.cs
│   └── WarehouseDbInitializer.cs
├── ViewModels/
│   └── IssuePartViewModel.cs
├── Views/
│   ├── Parts/Index.cshtml
│   ├── Parts/Details.cshtml
│   ├── Parts/Create.cshtml
│   ├── Parts/Edit.cshtml
│   ├── Parts/Issue.cshtml
│   ├── Parts/LowStock.cshtml
│   ├── Orders/Create.cshtml
│   └── Shared/_Layout.cshtml
├── Global.asax
└── Web.config
```

`_Layout.cshtml` задає спільний каркас сторінок: `<head>`, меню, підключення `Site.css` та `@RenderBody()`. У поданнях можна вказати `Layout = "~/Views/Shared/_Layout.cshtml"`; частіше цей layout вказують один раз у `Views/_ViewStart.cshtml`.

## 10. Контролери та критична бізнес-операція

Не слід передавати всю модель у `ViewBag`, як у старому прикладі слайдів, коли дані вже мають визначену структуру. `ViewBag` динамічний і не перевіряється компілятором. Для списку використати `@model IEnumerable<AutoPart>`, для форми — окрему view model.

```csharp
public class IssuePartViewModel
{
    public int AutoPartId { get; set; }
    public string PartName { get; set; }
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}
```

Найважливіша дія за activity-діаграмою ЛР №1 — видача:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public ActionResult Issue(IssuePartViewModel model)
{
    var part = db.AutoParts.Find(model.AutoPartId);
    if (part == null) return HttpNotFound();

    if (!ModelState.IsValid) return View(model);
    if (model.Quantity > part.QuantityInStock)
    {
        ModelState.AddModelError("Quantity", "Недостатня кількість на складі.");
        model.PartName = part.Name;
        return View(model);
    }

    part.QuantityInStock -= model.Quantity;
    db.SaveChanges();
    return RedirectToAction("Details", new { id = part.Id });
}
```

Вона реалізує потрібні гілки: запчастини немає (`404`), кількості недостатньо (помилка форми), видача успішна (залишок зменшено), критичний стан відображається умовою `QuantityInStock <= MinimumStock`.

Для POST-форми у Razor треба додати `@Html.AntiForgeryToken()` всередині `@using (Html.BeginForm())`; атрибут `[ValidateAntiForgeryToken]` захищає від підробленого запиту. Усі POST-дії повинні повторно перевіряти `ModelState.IsValid`: клієнтські HTML-обмеження можна обійти.

## 11. Подання Razor

Подання знаходяться у `Views/<НазваКонтролера>/` і зазвичай повертаються дією з таким самим іменем: `return View()` в `PartsController.Index()` шукає `Views/Parts/Index.cshtml`.

Приклад фрагмента списку:

```cshtml
@model IEnumerable<AutoPartsWarehouse.Models.AutoPart>

<h2>Запчастини</h2>
<table>
    <tr><th>Назва</th><th>Виробник</th><th>Залишок</th><th></th></tr>
    @foreach (var part in Model)
    {
        <tr>
            <td>@part.Name</td>
            <td>@part.Manufacturer</td>
            <td>@part.QuantityInStock</td>
            <td>@Html.ActionLink("Деталі", "Details", new { id = part.Id })</td>
        </tr>
    }
</table>
```

Символ `@` запускає Razor-код. `@foreach` формує рядки таблиці, `@part.Name` безпечно відображає значення, а `Html.ActionLink` створює URL через маршрутизацію замість ручного рядка на кшталт `href="/Home/Buy/@b.Id"` зі слайдів.

## 12. План роботи в Visual Studio

1. Створити ASP.NET Web Application (.NET Framework), вибрати **MVC**, без аутентифікації, якщо вона не є окремою вимогою.
2. Через NuGet встановити `EntityFramework` 6.x.
3. Додати сутності в `Models`, починаючи з `AutoPart`, `Supplier`, `VehicleModel`, `PurchaseOrder`.
4. Додати `WarehouseContext` із відповідними `DbSet<T>`.
5. Додати `connectionStrings` у `web.config`; звірити назву з `base("WarehouseContext")`.
6. Створити початкові дані, за потреби зареєструвати ініціалізатор у `Global.asax`.
7. Створити `PartsController`, реалізувати `Index`, `Details`, `Create` GET/POST, `Edit` GET/POST, `Issue` GET/POST, `LowStock`.
8. Додати strongly typed Razor-в'ю для кожної дії; для форм показувати validation messages.
9. Додати `OrdersController` для створення та приймання замовлення або реалізувати ці дії в `PartsController`, якщо обсяг потрібно зменшити.
10. Оформити `_Layout.cshtml`, навігацію і `Site.css`; перевірити, що всі посилання працюють.
11. Перевірити сценарії з розділу нижче й зробити скріншоти для звіту.

## 13. Чекліст перевірки перед здачею

- [ ] Проєкт запускається без помилок.
- [ ] БД створюється за Code First, у ній є таблиці для сутностей.
- [ ] Список запчастин відкривається через контролер, а не зі статичного HTML.
- [ ] Дані з EF відображаються у Razor-в'ю.
- [ ] Є щонайменше одна GET-форма й відповідна POST-дія.
- [ ] Після POST викликається `SaveChanges()` там, де дані змінюються.
- [ ] Валідація не допускає від'ємну/нульову кількість і видачу понад залишок.
- [ ] Критичні залишки обчислюються за кількістю та мінімальним порогом.
- [ ] Є демонстраційні дані для показу списку, видачі та критичного залишку.
- [ ] У звіті є скріншоти: головна/список, форма, результат операції, БД в SQL Server Object Explorer (якщо доступний).

## 14. Що пояснити на захисті

1. **MVC:** модель зберігає дані та правила, контролер обробляє запит, подання генерує HTML.
2. **Code First:** спочатку написані C#-класи й `DbContext`; EF створює таблиці за ними.
3. **`DbContext` і `DbSet<T>`:** контекст керує підключенням і змінами; `DbSet<T>` відповідає набору сутностей/таблиці.
4. **GET/POST:** GET читає або відкриває форму; POST створює/змінює дані. Видача й прийняття поставки не можуть бути GET.
5. **Маршрут:** `/Parts/Details/5` викликає `Details(int id)` у `PartsController`.
6. **Зв'язок із ЛР №1:** класи та сценарії не вигадані заново; вебзастосунок реалізує UML-модель складу, зокрема пошук, видачу, контроль залишків і замовлення.

## 15. Типові помилки та як їх уникнути

| Помилка | Наслідок | Правильний підхід |
|---|---|---|
| Узяти приклад BookStore як власну предметну область | Розрив із ЛР №1 | Зберегти архітектуру прикладу, але застосувати `AutoPart`/`Supplier`/`PurchaseOrder` |
| Змішати MVC 5 та ASP.NET Core | Не компілюються `System.Web`, `Global.asax`, конфігурація | На старті обрати один стек; за методичкою — MVC 5 + EF6 |
| `base` написати як `Base` | Помилка компіляції | У конструкторі контексту використовувати `base("...")` |
| Різні імена в `base(...)` і `connectionStrings` | EF не знаходить потрібне підключення | Ім'я повинно збігатися символ у символ |
| Використовувати `DropCreateDatabaseAlways` у готовому проєкті | БД очищується на кожному старті | Тільки для короткої демонстрації; краще `IfModelChanges` або міграції |
| Змінювати залишок у GET-дії | Повторне відкриття URL повторює операцію | Будь-яку зміну БД виконувати POST-дією |
| Довіряти тільки `min` у HTML | Обхід перевірки через підроблений запит | Валідувати model і залишок на сервері |
| Зберігати стан наявності рядком | Дані можуть суперечити кількості | Обчислювати стан з `QuantityInStock` і `MinimumStock` |
| Передавати складні дані через `ViewBag` | Немає перевірки типів, складно підтримувати | Використовувати strongly typed `@model` і ViewModel |

## 16. Висновок для звіту

У лабораторній роботі реалізовано вебзастосунок автоматизованої системи «Склад запчастин для авто» за шаблоном MVC. Модель даних сформовано з сутностей предметної області попередньої лабораторної роботи, контролери обробляють HTTP-запити та координують роботу застосунку, а Razor-подання відображають дані користувачу. Entity Framework Code First використано для створення та доступу до БД на основі C#-класів. Реалізовані операції перегляду запчастин, контролю залишків, видачі та роботи із замовленнями відповідають UML-сценаріям системи.
