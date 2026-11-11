using AutoPartsWarehouse.Controllers;
using AutoPartsWarehouse.Models;
using AutoPartsWarehouse.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoPartsWarehouseLab4.Tests;

/// <summary>
/// Checks the LINQ reports group by individual part (not one meaningless grand total),
/// that returns net against the correct part's issuance, that legacy/cancelled rows are
/// counted according to their own status, that the date filter works, and that running
/// a report never writes to the database.
/// </summary>
public class ReportsTests : IDisposable
{
    private readonly string _dbPath = TestDb.CreateNewDatabasePath();
    public void Dispose() => TestDb.Delete(_dbPath);
    private WarehouseContext NewContext() => TestDb.Open(_dbPath);

    [Fact]
    public async Task IssuedByPeriod_GroupsByIndividualPart_NotOneGrandTotal()
    {
        using var context = NewContext();
        var partA = new AutoPart { Name = "Part A", Manufacturer = "QA", QuantityInStock = 0, MinimumStock = 1 };
        var partB = new AutoPart { Name = "Part B", Manufacturer = "QA", QuantityInStock = 0, MinimumStock = 1 };
        context.AutoParts.AddRange(partA, partB);
        await context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        context.PartIssues.AddRange(
            new PartIssue { AutoPartId = partA.Id, Quantity = 5, Status = PartIssueStatus.Issued, CreatedAt = now, IssuedAt = now },
            new PartIssue { AutoPartId = partB.Id, Quantity = 7, Status = PartIssueStatus.Issued, CreatedAt = now, IssuedAt = now });
        await context.SaveChangesAsync();

        var controller = new ReportsController(context);
        var result = Assert.IsType<ViewResult>(await controller.IssuedByPeriod(null, null));
        var model = Assert.IsType<IssuedByPeriodViewModel>(result.Model);

        var rowA = Assert.Single(model.Summary, s => s.PartName == "Part A");
        var rowB = Assert.Single(model.Summary, s => s.PartName == "Part B");
        Assert.Equal(5, rowA.TotalIssued);
        Assert.Equal(7, rowB.TotalIssued);
    }

    [Fact]
    public async Task IssuedByPeriod_NetIssuance_SubtractsReturnsForThatPartOnly()
    {
        using var context = NewContext();
        var part = new AutoPart { Name = "Part C", Manufacturer = "QA", QuantityInStock = 4, MinimumStock = 1 };
        context.AutoParts.Add(part);
        await context.SaveChangesAsync();
        var now = DateTime.UtcNow;
        context.PartIssues.Add(new PartIssue
        {
            AutoPartId = part.Id, Quantity = 2, Status = PartIssueStatus.Returned,
            CreatedAt = now, IssuedAt = now, ReturnedAt = now
        });
        await context.SaveChangesAsync();

        var controller = new ReportsController(context);
        var result = Assert.IsType<ViewResult>(await controller.IssuedByPeriod(null, null));
        var model = Assert.IsType<IssuedByPeriodViewModel>(result.Model);
        var row = Assert.Single(model.Summary);
        Assert.Equal(2, row.TotalIssued);
        Assert.Equal(2, row.TotalReturned);
        Assert.Equal(0, row.Net);
    }

    [Fact]
    public async Task IssuedByPeriod_DateFilter_ExcludesIssuesOutsideRange()
    {
        using var context = NewContext();
        var part = new AutoPart { Name = "Part D", Manufacturer = "QA", QuantityInStock = 0, MinimumStock = 1 };
        context.AutoParts.Add(part);
        await context.SaveChangesAsync();
        var inRange = DateTime.UtcNow.Date;
        var outOfRange = inRange.AddMonths(-2);
        context.PartIssues.AddRange(
            new PartIssue { AutoPartId = part.Id, Quantity = 3, Status = PartIssueStatus.Issued, CreatedAt = inRange, IssuedAt = inRange },
            new PartIssue { AutoPartId = part.Id, Quantity = 9, Status = PartIssueStatus.Issued, CreatedAt = outOfRange, IssuedAt = outOfRange });
        await context.SaveChangesAsync();

        var controller = new ReportsController(context);
        var result = Assert.IsType<ViewResult>(await controller.IssuedByPeriod(inRange.AddDays(-1), inRange.AddDays(1)));
        var model = Assert.IsType<IssuedByPeriodViewModel>(result.Model);
        var row = Assert.Single(model.Summary);
        Assert.Equal(3, row.TotalIssued);
    }

    [Fact]
    public async Task RunningReports_NeverWritesToTheDatabase()
    {
        using var context = NewContext();
        var part = new AutoPart { Name = "Part E", Manufacturer = "QA", QuantityInStock = 3, MinimumStock = 5 };
        context.AutoParts.Add(part);
        await context.SaveChangesAsync();

        var controller = new ReportsController(context);
        await controller.Index(null);
        await controller.CriticalStock();
        await controller.ManufacturerStatistics();
        await controller.SupplierOrders();
        await controller.IssuedByPeriod(null, null);

        using var verify = NewContext();
        var unchanged = await verify.AutoParts.SingleAsync(p => p.Id == part.Id);
        Assert.Equal(3, unchanged.QuantityInStock);
        Assert.Equal(5, unchanged.MinimumStock);
    }

    [Fact]
    public async Task CriticalStock_IncludesZeroStock_BelowMinimum_AndExactlyAtMinimum_ButNotAboveIt()
    {
        // Межові випадки за вимогою ЛР4/ЛР5: 0, нижче мінімуму, точно на межі мінімуму, вище мінімуму.
        using var context = NewContext();
        context.AutoParts.AddRange(
            new AutoPart { Name = "Zero", Manufacturer = "QA", QuantityInStock = 0, MinimumStock = 5 },
            new AutoPart { Name = "BelowMinimum", Manufacturer = "QA", QuantityInStock = 3, MinimumStock = 5 },
            new AutoPart { Name = "ExactlyAtMinimum", Manufacturer = "QA", QuantityInStock = 5, MinimumStock = 5 },
            new AutoPart { Name = "AboveMinimum", Manufacturer = "QA", QuantityInStock = 10, MinimumStock = 5 });
        await context.SaveChangesAsync();

        var controller = new ReportsController(context);
        var result = Assert.IsType<ViewResult>(await controller.CriticalStock());
        var parts = Assert.IsAssignableFrom<List<AutoPart>>(result.Model);
        Assert.Equal(
            new[] { "Zero", "BelowMinimum", "ExactlyAtMinimum" },
            parts.OrderBy(p => p.QuantityInStock).Select(p => p.Name));
    }
}
