# Object-Oriented Programming

Coursework on an auto-parts warehouse: UML models for the domain and an ASP.NET Core MVC application that manages parts, suppliers, purchase orders, and stock issues.

## What the application does

The [Lab 2 application](labs/lab02/AutoPartsWarehouse/AutoPartsWarehouse.csproj) opens on the parts list. You can search by part name or manufacturer, inspect a part and its compatible vehicle models, add or edit parts, and filter for critical stock. The filter includes parts with `0 < QuantityInStock <= MinimumStock`; out-of-stock parts are excluded.

The [Lab 2 report](labs/lab02/%D0%9B%D0%A0_2_%D0%9E%D0%9E%D0%9F_%D0%90%D0%B3%D0%B0%D0%BF%D0%BE%D0%B2.docx) contains screenshots of the parts list, critical-stock filter, part details, issue form, suppliers, and purchase orders. To illustrate the [implemented actions](labs/lab02/AutoPartsWarehouse/Controllers/AutoPartsController.cs) and [order handling](labs/lab02/AutoPartsWarehouse/Controllers/PurchaseOrdersController.cs): a part with 4 units in stock and a minimum of 5 appears in the critical-stock list; issuing 2 units after payment confirmation reduces its stock to 2; receiving an order for 3 units then raises it to 5. These numbers are an example, not recorded database output.

## Coursework and files

| Item | Where to look | Result |
| --- | --- | --- |
| Lab 1 — warehouse modelling | [Lab 1 report](labs/lab01/%D0%9B%D0%A0_1_%D0%9E%D0%9E%D0%9F_%D0%90%D0%B3%D0%B0%D0%BF%D0%BE%D0%B2.docx) | The domain is described through the [class](diagrams/auto_parts_warehouse_classes.drawio), [use-case](diagrams/auto_parts_warehouse_use_case.drawio), [activity](diagrams/auto_parts_warehouse_activity.drawio), and [state](diagrams/auto_parts_warehouse_state.drawio) diagrams. Open the `.drawio` files in diagrams.net. |
| Lab 2 — MVC warehouse | [Application](labs/lab02/AutoPartsWarehouse/AutoPartsWarehouse.csproj), [lab report](labs/lab02/%D0%9B%D0%A0_2_%D0%9E%D0%9E%D0%9F_%D0%90%D0%B3%D0%B0%D0%BF%D0%BE%D0%B2.docx), [MVC assignment](labs/lab02/%D0%9B%D0%A0_2_MVC.docx), and [working notes](labs/lab02/%D0%9A%D0%9E%D0%9D%D0%A2%D0%95%D0%9A%D0%A1%D0%A2_%D0%9B%D0%A0_2_MVC.md) | C# / .NET 8 application with Razor views, Entity Framework Core 8, SQLite, and migrations. It supports part management, supplier creation, multi-item purchase orders, receiving deliveries, and issuing stock after payment confirmation. |
| Cover sheet | [Course cover sheet](docs/%D0%A2%D0%B8%D1%82%D1%83%D0%BB-%D0%BE%D0%BE%D0%BF.docx) | Document supplied with the coursework. |

The reports and application interface are in Ukrainian. The repository has no seeded warehouse database; use the steps below to create a local database and try the workflows.

## Run Lab 2

Install the .NET 8 SDK and make `dotnet-ef` version 8.0.22 available. If needed, install it with `dotnet tool install --global dotnet-ef --version 8.0.22`. Package and tool installation require access to NuGet.

From the repository root:

```bash
cd labs/lab02/AutoPartsWarehouse
dotnet restore
dotnet ef database update
dotnet run --launch-profile http
```

Open `http://localhost:5291`, or the address printed by `dotnet run`. Apply the migrations before starting: the app does not apply them automatically. The SQLite file `AutoPartsWarehouse.db` is created locally and is not tracked. A new database starts empty; add parts and suppliers through the application before creating purchase orders.

The included `dotnet-tools.json` is outside the usual `.config/dotnet-tools.json` location, so the commands above assume `dotnet-ef` is available globally or otherwise on your path.
