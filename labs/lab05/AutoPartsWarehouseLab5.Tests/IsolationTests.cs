using AutoPartsWarehouse.Controllers;
using AutoPartsWarehouse.Models;
using AutoPartsWarehouse.ViewModels;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace AutoPartsWarehouseLab5.Tests;

[Trait("Category", "Isolation")]
public class IsolationTests : IDisposable
{
    private readonly string _dbPath = TestDb.CreateNewDatabasePath();
    private readonly ITestOutputHelper _output;
    public IsolationTests(ITestOutputHelper output) => _output = output;
    public void Dispose() => TestDb.Delete(_dbPath);
    private WarehouseContext NewContext() => TestDb.Open(_dbPath);

    [Fact]
    public async Task TwoConcurrentReturnIssue_OnTheSameDocument_MustOnlyRestoreStockOnce()
    {
        int partId, issueId;
        using (var context = NewContext())
        {
            var part = new AutoPart { Name = "Isolation Part", Manufacturer = "QA", QuantityInStock = 10, MinimumStock = 1 };
            context.AutoParts.Add(part);
            await context.SaveChangesAsync();
            partId = part.Id;

            var controller = new AutoPartsController(context);
            TestDb.ActAs(controller, "actor-1");
            await controller.Issue(new IssuePartViewModel { AutoPartId = partId, Quantity = 3 });
            issueId = await context.PartIssues.Where(i => i.AutoPartId == partId).Select(i => i.Id).FirstAsync();
            await controller.ConfirmPayment(issueId);
            await controller.CompleteIssue(issueId); // stock now 7, issue Issued
        }

        async Task ReturnAsync(string actor)
        {
            using var context = NewContext();
            var controller = new AutoPartsController(context);
            TestDb.ActAs(controller, actor);
            await controller.ReturnIssue(issueId, "паралельна спроба повернення");
        }

        await Task.WhenAll(Task.Run(() => ReturnAsync("actor-A")), Task.Run(() => ReturnAsync("actor-B")));

        using var verify = NewContext();
        var verifiedPart = await verify.AutoParts.FindAsync(partId);
        var verifiedIssue = await verify.PartIssues.FindAsync(issueId);
        _output.WriteLine($"Final stock after two concurrent ReturnIssue on the same document: {verifiedPart!.QuantityInStock}");

        Assert.Equal(10, verifiedPart.QuantityInStock); // 7 + 3, exactly once - not 13
        Assert.Equal(PartIssueStatus.Returned, verifiedIssue!.Status);
    }
}
