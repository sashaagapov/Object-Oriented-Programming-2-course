# Object-Oriented Programming

Coursework for Object-Oriented Programming, including UML diagrams and an ASP.NET Core MVC laboratory project.

## Structure

```text
labs/lab01/                     # OOP laboratory report
labs/lab02/AutoPartsWarehouse/  # ASP.NET Core MVC application
diagrams/                       # draw.io UML diagrams
docs/                           # submission template
labs/*/                         # submitted reports and context files
```

## Technologies

- C# and .NET 8
- ASP.NET Core MVC
- Entity Framework Core
- SQLite for local development

## Run the MVC project

From the repository root:

```bash
dotnet run --project labs/lab02/AutoPartsWarehouse/AutoPartsWarehouse.csproj
```

The local SQLite database is intentionally not versioned. The EF Core migrations in `labs/lab02/AutoPartsWarehouse/Migrations/` are the source of truth for the schema.
