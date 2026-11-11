using AutoPartsWarehouse.Controllers;
using AutoPartsWarehouse.Models;
using AutoPartsWarehouse.ViewModels;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoPartsWarehouseLab5.Tests;

[Trait("Category", "Atomicity")]
public class AtomicityRollbackTests : IDisposable
{
    private readonly string _dbPath = TestDb.CreateNewDatabasePath();
    public void Dispose() => TestDb.Delete(_dbPath);
    private WarehouseContext NewContext() => TestDb.Open(_dbPath);

    [Fact]
    public async Task AdjustStock_WhenTheAuditRowViolatesAForeignKey_RollsBackTheQuantityChangeToo()

    {
        int partId;
        using (var context = NewContext())
        {
            var part = new AutoPart { Name = "Rollback Part", Manufacturer = "QA", QuantityInStock = 10, MinimumStock = 1 };
            context.AutoParts.Add(part);
            await context.SaveChangesAsync();
            partId = part.Id;
        }

        using (var context = NewContext())
        {
            var controller = new AutoPartsController(context);
            TestDb.ActAs(controller, "user-that-does-not-exist-in-aspnetusers");

            var ex = await Record.ExceptionAsync(() => controller.AdjustStock(new StockAdjustmentInput
            {
                AutoPartId = partId,
                Delta = -4,
                Reason = "Навмисна помилка для перевірки rollback (ЛР5, Atomicity)"
            }));

            Assert.NotNull(ex); // a real FK violation must surface, not be silently swallowed
        }

        using var verify = NewContext();
        var verifiedPart = await verify.AutoParts.FindAsync(partId);
        Assert.Equal(10, verifiedPart!.QuantityInStock); // unchanged - the staged -4 never committed
        Assert.False(await verify.StockAdjustments.AnyAsync(a => a.AutoPartId == partId)); // no orphan audit row either
    }

    [Fact]
    public async Task ForeignKeyRestrictOnPurchaseOrderItem_IsEnforcedBySqliteItself_NotOnlyByTheController()
    {
        int supplierId, partBId;
        using (var context = NewContext())
        {
            var supplier = new Supplier { Name = "QA Supplier", Phone = "+380000000000" };
            var partB = new AutoPart { Name = "Line B", Manufacturer = "QA", QuantityInStock = 0, MinimumStock = 1 };
            context.Suppliers.Add(supplier);
            context.AutoParts.Add(partB);
            await context.SaveChangesAsync();
            supplierId = supplier.Id;
            partBId = partB.Id;

            context.PurchaseOrders.Add(new PurchaseOrder
            {
                SupplierId = supplierId,
                Items = { new PurchaseOrderItem { AutoPartId = partBId, Quantity = 3 } }
            });
            await context.SaveChangesAsync();
        }

        using var verify = NewContext();
        var sqliteError = await Record.ExceptionAsync(() =>
            verify.Database.ExecuteSqlRawAsync("DELETE FROM AutoParts WHERE Id = {0}", partBId));

        Assert.NotNull(sqliteError);
        Assert.Contains("FOREIGN KEY constraint failed", sqliteError!.Message);
        Assert.True(await verify.AutoParts.AnyAsync(p => p.Id == partBId)); // the row survives - nothing was partially deleted
    }
}
