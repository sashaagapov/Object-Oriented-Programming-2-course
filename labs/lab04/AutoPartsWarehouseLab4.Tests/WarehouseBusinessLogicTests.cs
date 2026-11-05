using AutoPartsWarehouse.Controllers;
using AutoPartsWarehouse.Models;
using AutoPartsWarehouse.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoPartsWarehouseLab4.Tests;

/// <summary>
/// Exercises the real controller actions (not re-implemented copies) against an isolated
/// SQLite file database, covering the positive/negative warehouse scenarios requested in
/// the audit: purchase orders, issues, returns and stock adjustments.
/// </summary>
public class WarehouseBusinessLogicTests : IDisposable
{
    private readonly string _dbPath = TestDb.CreateNewDatabasePath();
    private const string ActorId = "actor-1";

    public void Dispose() => TestDb.Delete(_dbPath);

    private WarehouseContext NewContext() => TestDb.Open(_dbPath);

    private async Task<int> SeedPartAsync(int quantity = 0, int minimum = 5)
    {
        using var context = NewContext();
        var part = new AutoPart { Name = "QA Test Part " + Guid.NewGuid(), Manufacturer = "QA", QuantityInStock = quantity, MinimumStock = minimum };
        context.AutoParts.Add(part);
        await context.SaveChangesAsync();
        return part.Id;
    }

    private async Task SeedActorUserAsync(string id = ActorId)
    {
        using var context = NewContext();
        if (await context.Users.AnyAsync(u => u.Id == id)) return;
        context.Users.Add(new ApplicationUser { Id = id, UserName = id + "@lab4.local", Email = id + "@lab4.local", DisplayName = "QA Actor" });
        await context.SaveChangesAsync();
    }

    private static AutoPartsController MakeAutoPartsController(WarehouseContext context)
    {
        var controller = new AutoPartsController(context);
        TestDb.ActAs(controller, ActorId);
        return controller;
    }

    private static PurchaseOrdersController MakePurchaseOrdersController(WarehouseContext context)
    {
        var controller = new PurchaseOrdersController(context);
        TestDb.ActAs(controller, ActorId);
        return controller;
    }

    // ---------- Purchase orders ----------

    [Fact]
    public async Task Receive_AddsQuantityForEachLineItem()
    {
        var partId = await SeedPartAsync(quantity: 0);
        int orderId;
        using (var context = NewContext())
        {
            var order = new PurchaseOrder
            {
                SupplierId = (await SeedSupplierAsync(context)),
                Items = { new PurchaseOrderItem { AutoPartId = partId, Quantity = 4 }, new PurchaseOrderItem { AutoPartId = partId, Quantity = 2 } }
            };
            context.PurchaseOrders.Add(order);
            await context.SaveChangesAsync();
            orderId = order.Id;
        }

        using (var context = NewContext())
        {
            var controller = MakePurchaseOrdersController(context);
            var result = await controller.Receive(orderId);
            Assert.IsType<RedirectToActionResult>(result);
        }

        using (var context = NewContext())
        {
            var part = await context.AutoParts.FindAsync(partId);
            var order = await context.PurchaseOrders.FindAsync(orderId);
            Assert.Equal(6, part!.QuantityInStock);
            Assert.Equal(PurchaseOrderStatus.Received, order!.Status);
        }
    }

    [Fact]
    public async Task Receive_Twice_DoesNotDoubleTheStock()
    {
        var partId = await SeedPartAsync(quantity: 0);
        int orderId;
        using (var context = NewContext())
        {
            var order = new PurchaseOrder
            {
                SupplierId = await SeedSupplierAsync(context),
                Items = { new PurchaseOrderItem { AutoPartId = partId, Quantity = 5 } }
            };
            context.PurchaseOrders.Add(order);
            await context.SaveChangesAsync();
            orderId = order.Id;
        }

        using (var context = NewContext())
        {
            await MakePurchaseOrdersController(context).Receive(orderId);
        }
        using (var context = NewContext())
        {
            await MakePurchaseOrdersController(context).Receive(orderId);
        }

        using (var context = NewContext())
        {
            var part = await context.AutoParts.FindAsync(partId);
            Assert.Equal(5, part!.QuantityInStock);
        }
    }

    [Fact]
    public async Task ReceivedOrder_CannotBeCancelled()
    {
        var partId = await SeedPartAsync();
        int orderId;
        using (var context = NewContext())
        {
            var order = new PurchaseOrder { SupplierId = await SeedSupplierAsync(context), Items = { new PurchaseOrderItem { AutoPartId = partId, Quantity = 1 } } };
            context.PurchaseOrders.Add(order);
            await context.SaveChangesAsync();
            orderId = order.Id;
        }
        using (var context = NewContext()) { await MakePurchaseOrdersController(context).Receive(orderId); }

        using (var context = NewContext())
        {
            var controller = MakePurchaseOrdersController(context);
            await controller.DeleteConfirmed(orderId, "спроба анулювати отримане");
            var order = await context.PurchaseOrders.FindAsync(orderId);
            Assert.Equal(PurchaseOrderStatus.Received, order!.Status);
        }
    }

    // ---------- Issues: pending -> paid -> issued -> returned ----------

    [Fact]
    public async Task Issue_CreatesPending_WithoutChangingStock()
    {
        var partId = await SeedPartAsync(quantity: 6);
        using var context = NewContext();
        var controller = MakeAutoPartsController(context);
        await controller.Issue(new IssuePartViewModel { AutoPartId = partId, Quantity = 2 });

        var part = await context.AutoParts.FindAsync(partId);
        var issue = await context.PartIssues.SingleAsync(i => i.AutoPartId == partId);
        Assert.Equal(6, part!.QuantityInStock);
        Assert.Equal(PartIssueStatus.Pending, issue.Status);
        Assert.False(issue.IsPaymentConfirmed);
    }

    [Fact]
    public async Task CompleteIssue_WithoutPaymentConfirmation_IsRejected()
    {
        var partId = await SeedPartAsync(quantity: 6);
        int issueId = await CreatePendingIssueAsync(partId, 2);

        using var context = NewContext();
        var controller = MakeAutoPartsController(context);
        await controller.CompleteIssue(issueId);

        var issue = await context.PartIssues.FindAsync(issueId);
        var part = await context.AutoParts.FindAsync(partId);
        Assert.Equal(PartIssueStatus.Pending, issue!.Status);
        Assert.Equal(6, part!.QuantityInStock);
    }

    [Fact]
    public async Task CompleteIssue_AfterPayment_DecrementsStockAndMarksIssued()
    {
        var partId = await SeedPartAsync(quantity: 6);
        int issueId = await CreatePendingIssueAsync(partId, 2);
        using (var context = NewContext()) { await MakeAutoPartsController(context).ConfirmPayment(issueId); }

        using (var context = NewContext())
        {
            await MakeAutoPartsController(context).CompleteIssue(issueId);
        }

        using var verify = NewContext();
        var part = await verify.AutoParts.FindAsync(partId);
        var issue = await verify.PartIssues.FindAsync(issueId);
        Assert.Equal(4, part!.QuantityInStock);
        Assert.Equal(PartIssueStatus.Issued, issue!.Status);
        Assert.NotNull(issue.IssuedAt);
    }

    [Fact]
    public async Task CompleteIssue_Twice_DoesNotDoubleDecrementStock()
    {
        var partId = await SeedPartAsync(quantity: 6);
        int issueId = await CreatePendingIssueAsync(partId, 2);
        using (var context = NewContext()) { await MakeAutoPartsController(context).ConfirmPayment(issueId); }
        using (var context = NewContext()) { await MakeAutoPartsController(context).CompleteIssue(issueId); }
        using (var context = NewContext()) { await MakeAutoPartsController(context).CompleteIssue(issueId); }

        using var verify = NewContext();
        var part = await verify.AutoParts.FindAsync(partId);
        Assert.Equal(4, part!.QuantityInStock);
    }

    [Fact]
    public async Task CompleteIssue_WhenQuantityExceedsStock_IsRejected()
    {
        var partId = await SeedPartAsync(quantity: 1);
        int issueId = await CreatePendingIssueAsync(partId, 2);
        using (var context = NewContext()) { await MakeAutoPartsController(context).ConfirmPayment(issueId); }

        using (var context = NewContext()) { await MakeAutoPartsController(context).CompleteIssue(issueId); }

        using var verify = NewContext();
        var part = await verify.AutoParts.FindAsync(partId);
        var issue = await verify.PartIssues.FindAsync(issueId);
        Assert.Equal(1, part!.QuantityInStock);
        Assert.Equal(PartIssueStatus.Pending, issue!.Status);
    }

    [Fact]
    public async Task ReturnIssue_RestoresStockOnceAndRejectsSecondReturn()
    {
        var partId = await SeedPartAsync(quantity: 6);
        int issueId = await CreatePendingIssueAsync(partId, 2);
        using (var context = NewContext()) { await MakeAutoPartsController(context).ConfirmPayment(issueId); }
        using (var context = NewContext()) { await MakeAutoPartsController(context).CompleteIssue(issueId); }

        using (var context = NewContext()) { await MakeAutoPartsController(context).ReturnIssue(issueId, "клієнт повернув"); }
        using (var context = NewContext()) { await MakeAutoPartsController(context).ReturnIssue(issueId, "повторна спроба"); }

        using var verify = NewContext();
        var part = await verify.AutoParts.FindAsync(partId);
        var issue = await verify.PartIssues.FindAsync(issueId);
        Assert.Equal(6, part!.QuantityInStock);
        Assert.Equal(PartIssueStatus.Returned, issue!.Status);
    }

    [Fact]
    public async Task ReturnIssue_WithoutReason_IsRejected()
    {
        var partId = await SeedPartAsync(quantity: 6);
        int issueId = await CreatePendingIssueAsync(partId, 2);
        using (var context = NewContext()) { await MakeAutoPartsController(context).ConfirmPayment(issueId); }
        using (var context = NewContext()) { await MakeAutoPartsController(context).CompleteIssue(issueId); }

        using (var context = NewContext()) { await MakeAutoPartsController(context).ReturnIssue(issueId, "   "); }

        using var verify = NewContext();
        var issue = await verify.PartIssues.FindAsync(issueId);
        Assert.Equal(PartIssueStatus.Issued, issue!.Status);
    }

    [Fact]
    public async Task CancelIssue_OnlyAllowedForPending()
    {
        var partId = await SeedPartAsync(quantity: 6);
        int issueId = await CreatePendingIssueAsync(partId, 2);
        using (var context = NewContext()) { await MakeAutoPartsController(context).ConfirmPayment(issueId); }
        using (var context = NewContext()) { await MakeAutoPartsController(context).CompleteIssue(issueId); }

        using (var context = NewContext()) { await MakeAutoPartsController(context).CancelIssue(issueId, "запізніла спроба скасувати"); }

        using var verify = NewContext();
        var issue = await verify.PartIssues.FindAsync(issueId);
        Assert.Equal(PartIssueStatus.Issued, issue!.Status);
    }

    // ---------- Stock adjustments ----------

    [Fact]
    public async Task AdjustStock_RecordsBeforeAfterDeltaAndUpdatesQuantity()
    {
        var partId = await SeedPartAsync(quantity: 6);
        await SeedActorUserAsync();

        using var context = NewContext();
        var controller = MakeAutoPartsController(context);
        await controller.AdjustStock(new StockAdjustmentInput { AutoPartId = partId, Delta = -2, Reason = "Інвентаризація" });

        var part = await context.AutoParts.FindAsync(partId);
        var adjustment = await context.StockAdjustments.SingleAsync(a => a.AutoPartId == partId);
        Assert.Equal(4, part!.QuantityInStock);
        Assert.Equal(6, adjustment.QuantityBefore);
        Assert.Equal(4, adjustment.QuantityAfter);
        Assert.Equal(-2, adjustment.Delta);
        Assert.Equal(ActorId, adjustment.PerformedByUserId);
    }

    [Fact]
    public async Task AdjustStock_CannotProduceNegativeQuantity()
    {
        var partId = await SeedPartAsync(quantity: 4);
        await SeedActorUserAsync();

        using var context = NewContext();
        var controller = MakeAutoPartsController(context);
        await controller.AdjustStock(new StockAdjustmentInput { AutoPartId = partId, Delta = -20, Reason = "перевірка межі" });

        var part = await context.AutoParts.FindAsync(partId);
        Assert.Equal(4, part!.QuantityInStock);
        Assert.False(await context.StockAdjustments.AnyAsync(a => a.AutoPartId == partId));
    }

    [Fact]
    public async Task AdjustStock_WithoutReason_IsRejected()
    {
        var partId = await SeedPartAsync(quantity: 4);
        await SeedActorUserAsync();

        using var context = NewContext();
        var controller = MakeAutoPartsController(context);
        await controller.AdjustStock(new StockAdjustmentInput { AutoPartId = partId, Delta = -1, Reason = "" });

        var part = await context.AutoParts.FindAsync(partId);
        Assert.Equal(4, part!.QuantityInStock);
    }

    // ---------- CRUD integrity ----------

    [Fact]
    public async Task DeleteAutoPart_WithIssueHistory_IsBlocked()
    {
        var partId = await SeedPartAsync(quantity: 6);
        await CreatePendingIssueAsync(partId, 1);

        using var context = NewContext();
        var controller = MakeAutoPartsController(context);
        var result = await controller.DeleteConfirmed(partId);

        Assert.IsType<ViewResult>(result);
        Assert.True(await context.AutoParts.AnyAsync(p => p.Id == partId));
    }

    [Fact]
    public async Task CreateAutoPart_DuplicateNameAndManufacturer_IsRejected()
    {
        using var context = NewContext();
        var controller = MakeAutoPartsController(context);
        await controller.Create(new AutoPart { Name = "Дублікат", Manufacturer = "Bosch", MinimumStock = 1 });
        await controller.Create(new AutoPart { Name = "Дублікат", Manufacturer = "Bosch", MinimumStock = 1 });

        var count = await context.AutoParts.CountAsync(p => p.Name == "Дублікат" && p.Manufacturer == "Bosch");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task EditAutoPart_CannotOverpostQuantityInStock()
    {
        var partId = await SeedPartAsync(quantity: 0);
        using var context = NewContext();
        var controller = MakeAutoPartsController(context);

        var forged = new AutoPart { Id = partId, Name = "Перейменовано", Manufacturer = "QA", MinimumStock = 1, QuantityInStock = 999 };
        await controller.Edit(partId, forged);

        var part = await context.AutoParts.FindAsync(partId);
        Assert.Equal(0, part!.QuantityInStock);
        Assert.Equal("Перейменовано", part.Name);
    }

    private async Task<int> CreatePendingIssueAsync(int partId, int quantity)
    {
        using var context = NewContext();
        var controller = MakeAutoPartsController(context);
        await controller.Issue(new IssuePartViewModel { AutoPartId = partId, Quantity = quantity });
        return await context.PartIssues.Where(i => i.AutoPartId == partId).Select(i => i.Id).OrderByDescending(id => id).FirstAsync();
    }

    private static async Task<int> SeedSupplierAsync(WarehouseContext context)
    {
        var supplier = new Supplier { Name = "QA Supplier " + Guid.NewGuid(), Phone = "+380000000000" };
        context.Suppliers.Add(supplier);
        await context.SaveChangesAsync();
        return supplier.Id;
    }
}
