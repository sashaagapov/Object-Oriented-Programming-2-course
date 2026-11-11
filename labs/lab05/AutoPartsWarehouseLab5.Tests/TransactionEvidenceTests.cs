using System.Data.Common;
using AutoPartsWarehouse.Controllers;
using AutoPartsWarehouse.Models;
using AutoPartsWarehouse.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AutoPartsWarehouseLab5.Tests;

[Trait("Category", "Durability")]
public class TransactionEvidenceTests : IDisposable
{
    private readonly string path = TestDb.CreateNewDatabasePath();
    public void Dispose() => TestDb.Delete(path);

    private WarehouseContext Open(DbTransactionInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<WarehouseContext>()
            .UseSqlite($"Data Source={path};Pooling=False");
        if (interceptor != null) options.AddInterceptors(interceptor);
        return new WarehouseContext(options.Options);
    }

    private async Task<int> SeedAsync()
    {
        await using var db = Open();
        db.Users.Add(new ApplicationUser { Id = "test-worker", UserName = "worker" });
        var part = new AutoPart { Name = "Test part", Manufacturer = "Test", QuantityInStock = 10, MinimumStock = 1 };
        db.AutoParts.Add(part);
        await db.SaveChangesAsync();
        return part.Id;
    }

    [Fact]
    public async Task Adjustment_RemainsAfterCommitAndClosingTheConnection()
    {
        var id = await SeedAsync();
        await using (var db = Open())
        {
            var controller = new AutoPartsController(db);
            TestDb.ActAs(controller, "test-worker");
            await controller.AdjustStock(new StockAdjustmentInput { AutoPartId = id, Delta = -3, Reason = "Інвентаризація" });
        }

        // A separate, non-pooled physical connection reads the committed rows.
        await using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT QuantityInStock, (SELECT COUNT(*) FROM StockAdjustments WHERE AutoPartId = $id AND Delta = -3) FROM AutoParts WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(7, reader.GetInt32(0));
        Assert.Equal(1, reader.GetInt32(1));
    }

    [Fact]
    [Trait("Category", "Atomicity")]
    public async Task Adjustment_ErrorBeforeCommit_RollsBackBothWrittenRows()
    {
        var id = await SeedAsync();
        var failure = new FailBeforeCommit();
        await using (var db = Open(failure))
        {
            var controller = new AutoPartsController(db);
            TestDb.ActAs(controller, "test-worker");
            await Assert.ThrowsAsync<InvalidOperationException>(() => controller.AdjustStock(
                new StockAdjustmentInput { AutoPartId = id, Delta = -3, Reason = "Інвентаризація" }));
        }
        Assert.True(failure.CommitReached);
        await using var verify = Open();
        Assert.Equal(10, (await verify.AutoParts.FindAsync(id))!.QuantityInStock);
        Assert.False(await verify.StockAdjustments.AnyAsync(x => x.AutoPartId == id));
    }

    private sealed class FailBeforeCommit : DbTransactionInterceptor
    {
        public bool CommitReached { get; private set; }
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
        {
            CommitReached = true; // SaveChangesAsync has completed; COMMIT has not run yet.
            throw new InvalidOperationException("Controlled failure before commit");
        }
    }
}
