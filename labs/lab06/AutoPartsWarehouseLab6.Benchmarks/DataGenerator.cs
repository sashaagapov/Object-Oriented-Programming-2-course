using AutoPartsWarehouse.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoPartsWarehouseLab6.Benchmarks;

/// <summary>
/// Programmatically fills a fresh, isolated SQLite database with AutoPart rows for benchmarking.
/// Every call starts from an empty database file, so repeated invocations never accumulate
/// duplicate rows; the fixed seed makes the generated dataset reproducible across runs.
/// </summary>
public static class DataGenerator
{
    private const int Seed = 20260601;

    private static readonly string[] PartTypes =
    {
        "Гальмівні колодки", "Масляний фільтр", "Повітряний фільтр", "Паливний фільтр",
        "Свічка запалювання", "Амортизатор", "Пружина підвіски", "Приводний ремінь",
        "Радіатор охолодження", "Термостат", "Водяна помпа", "Стартер",
        "Генератор", "Акумулятор", "Гальмівний диск", "Сайлентблок",
        "Рульова тяга", "Шарова опора", "Датчик кисню", "Свічка накалювання",
    };

    private static readonly string[] Manufacturers =
    {
        "Bosch", "Denso", "NGK", "Mann Filter", "Febi Bilstein", "Mahle",
        "Valeo", "TRW", "ACDelco", "Hella", "Sachs", "Monroe", "Delphi", "Continental",
    };

    // Pooling=False: without it, Microsoft.Data.Sqlite can hand back a pooled connection that
    // still points at a file we just deleted and recreated on disk, so a repeat Generate() call
    // sees the OLD schema and fails with "table already exists".
    public static string ConnectionString(string dbPath) => $"Data Source={dbPath};Pooling=False";

    private static WarehouseContext NewContext(string dbPath)
    {
        var options = new DbContextOptionsBuilder<WarehouseContext>()
            .UseSqlite(ConnectionString(dbPath))
            .Options;
        return new WarehouseContext(options);
    }

    /// <summary>
    /// Deletes any previous database at <paramref name="dbPath"/>, recreates the schema via the
    /// same EF Core model/migrations the web app uses, and inserts <paramref name="rowCount"/>
    /// AutoPart rows with a deterministic, reproducible mix of low-stock and healthy-stock rows.
    /// </summary>
    public static GenerationResult Generate(string dbPath, int rowCount)
    {
        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            var file = dbPath + suffix;
            if (File.Exists(file)) File.Delete(file);
        }

        using var context = NewContext(dbPath);
        context.Database.Migrate();

        var random = new Random(Seed);
        context.ChangeTracker.AutoDetectChangesEnabled = false;

        const int batchSize = 5000;
        var lowStockCount = 0;
        using var transaction = context.Database.BeginTransaction();
        for (var i = 0; i < rowCount; i++)
        {
            var partType = PartTypes[random.Next(PartTypes.Length)];
            var manufacturer = Manufacturers[random.Next(Manufacturers.Length)];
            // The sequence number keeps (Name, Manufacturer) unique even at 100 000 rows,
            // without needing to track which combinations were already used.
            var name = $"{partType} #{i + 1:D6}";
            var minimumStock = random.Next(1, 20);

            int quantity;
            var isLowStock = random.NextDouble() < 0.4;
            if (isLowStock)
            {
                quantity = random.Next(0, minimumStock + 1); // <= MinimumStock: a part to reorder
                lowStockCount++;
            }
            else
            {
                quantity = minimumStock + random.Next(1, 200); // comfortably above the threshold
            }

            context.AutoParts.Add(new AutoPart
            {
                Name = name,
                Manufacturer = manufacturer,
                QuantityInStock = quantity,
                MinimumStock = minimumStock,
            });

            if ((i + 1) % batchSize == 0 || i == rowCount - 1)
            {
                context.SaveChanges();
                context.ChangeTracker.Clear();
            }
        }
        transaction.Commit();

        return new GenerationResult(rowCount, lowStockCount, dbPath);
    }

    public readonly record struct GenerationResult(int TotalRows, int LowStockRows, string DbPath);
}
