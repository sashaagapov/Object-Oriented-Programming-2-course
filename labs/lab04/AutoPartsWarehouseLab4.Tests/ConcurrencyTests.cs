using AutoPartsWarehouse.Controllers;
using AutoPartsWarehouse.Models;
using AutoPartsWarehouse.ViewModels;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace AutoPartsWarehouseLab4.Tests;

/// <summary>
/// Fires genuinely parallel requests (separate DbContext + SQLite connection per task, exactly
/// like two separate HTTP requests would each get their own scoped DbContext) against the last
/// unit of stock, to check whether the controllers' transactions actually prevent a lost update
/// or whether the last-read-wins. This is the scenario the report's table 6, rows 25-26 claims
/// was verified ("two concurrent requests for the last unit -> issued once").
/// </summary>
public class ConcurrencyTests : IDisposable
{
    private readonly string _dbPath = TestDb.CreateNewDatabasePath();
    private const string ActorA = "actor-A";
    private const string ActorB = "actor-B";
    private readonly ITestOutputHelper _output;

    public ConcurrencyTests(ITestOutputHelper output) => _output = output;

    public void Dispose() => TestDb.Delete(_dbPath);

    private WarehouseContext NewContext() => TestDb.Open(_dbPath);

    private async Task<int> SeedPartAsync(int quantity)
    {
        using var context = NewContext();
        var part = new AutoPart { Name = "Concurrent Part " + Guid.NewGuid(), Manufacturer = "QA", QuantityInStock = quantity, MinimumStock = 1 };
        context.AutoParts.Add(part);
        await context.SaveChangesAsync();
        return part.Id;
    }

    private async Task SeedActorsAsync()
    {
        using var context = NewContext();
        context.Users.Add(new ApplicationUser { Id = ActorA, UserName = "a@lab4.local", Email = "a@lab4.local", DisplayName = "A" });
        context.Users.Add(new ApplicationUser { Id = ActorB, UserName = "b@lab4.local", Email = "b@lab4.local", DisplayName = "B" });
        await context.SaveChangesAsync();
    }

    private static AutoPartsController MakeController(WarehouseContext context, string actor)
    {
        var controller = new AutoPartsController(context);
        TestDb.ActAs(controller, actor);
        return controller;
    }

    [Fact]
    public async Task TwoConcurrentCompleteIssue_OnLastUnit_MustNotIssueBothOrGoNegative()
    {
        var partId = await SeedPartAsync(quantity: 1);
        int issueA, issueB;
        using (var context = NewContext())
        {
            var controller = MakeController(context, ActorA);
            await controller.Issue(new IssuePartViewModel { AutoPartId = partId, Quantity = 1 });
            await controller.Issue(new IssuePartViewModel { AutoPartId = partId, Quantity = 1 });
            var ids = await context.PartIssues.Where(i => i.AutoPartId == partId).Select(i => i.Id).OrderBy(id => id).ToListAsync();
            issueA = ids[0];
            issueB = ids[1];
            await controller.ConfirmPayment(issueA);
            await controller.ConfirmPayment(issueB);
        }

        async Task CompleteAsync(int issueId, string actor)
        {
            using var context = NewContext();
            await MakeController(context, actor).CompleteIssue(issueId);
        }

        await Task.WhenAll(
            Task.Run(() => CompleteAsync(issueA, ActorA)),
            Task.Run(() => CompleteAsync(issueB, ActorB)));

        using var verify = NewContext();
        var part = await verify.AutoParts.FindAsync(partId);
        var issuedCount = await verify.PartIssues.CountAsync(i => i.Id == issueA || i.Id == issueB ? i.Status == PartIssueStatus.Issued : false);
        _output.WriteLine($"Final stock: {part!.QuantityInStock}, issued count: {issuedCount}");

        Assert.True(part.QuantityInStock >= 0, "Stock must never go negative.");
        Assert.True(issuedCount <= 1, $"Only one of the two issues for the single remaining unit must end up Issued, but {issuedCount} did (lost-update / overselling).");
        if (issuedCount == 1)
        {
            Assert.Equal(0, part.QuantityInStock);
        }
    }

    [Fact]
    public async Task TwoConcurrentReceive_OnSameOrder_MustOnlyApplyOnce()
    {
        var partId = await SeedPartAsync(quantity: 0);
        int orderId;
        using (var context = NewContext())
        {
            var supplier = new Supplier { Name = "QA Supplier " + Guid.NewGuid(), Phone = "+380000000000" };
            context.Suppliers.Add(supplier);
            await context.SaveChangesAsync();
            var order = new PurchaseOrder { SupplierId = supplier.Id, Items = { new PurchaseOrderItem { AutoPartId = partId, Quantity = 5 } } };
            context.PurchaseOrders.Add(order);
            await context.SaveChangesAsync();
            orderId = order.Id;
        }

        async Task ReceiveAsync()
        {
            using var context = NewContext();
            var controller = new PurchaseOrdersController(context);
            TestDb.ActAs(controller, ActorA);
            await controller.Receive(orderId);
        }

        await Task.WhenAll(Task.Run(ReceiveAsync), Task.Run(ReceiveAsync));

        using var verify = NewContext();
        var part = await verify.AutoParts.FindAsync(partId);
        _output.WriteLine($"Final stock after two concurrent Receive: {part!.QuantityInStock}");
        Assert.Equal(5, part.QuantityInStock);
    }

    [Fact]
    public async Task ConcurrentAdjustStockAndCompleteIssue_BothApply_NoLostUpdate()
    {
        await SeedActorsAsync();
        var partId = await SeedPartAsync(quantity: 10);
        int issueId;
        using (var context = NewContext())
        {
            var controller = MakeController(context, ActorA);
            await controller.Issue(new IssuePartViewModel { AutoPartId = partId, Quantity = 3 });
            issueId = await context.PartIssues.Where(i => i.AutoPartId == partId).Select(i => i.Id).FirstAsync();
            await controller.ConfirmPayment(issueId);
        }

        async Task CompleteIssueAsync()
        {
            using var context = NewContext();
            await MakeController(context, ActorA).CompleteIssue(issueId);
        }

        async Task AdjustAsync()
        {
            using var context = NewContext();
            await MakeController(context, ActorB).AdjustStock(new StockAdjustmentInput { AutoPartId = partId, Delta = -2, Reason = "паралельна інвентаризація" });
        }

        await Task.WhenAll(Task.Run(CompleteIssueAsync), Task.Run(AdjustAsync));

        using var verify = NewContext();
        var part = await verify.AutoParts.FindAsync(partId);
        _output.WriteLine($"Final stock after concurrent issue(-3) + adjustment(-2) from base 10: {part!.QuantityInStock}");
        // Expected if both operations are correctly applied: 10 - 3 - 2 = 5.
        Assert.Equal(5, part.QuantityInStock);
    }
}
