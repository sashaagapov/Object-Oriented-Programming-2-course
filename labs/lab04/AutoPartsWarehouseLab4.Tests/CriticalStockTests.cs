using AutoPartsWarehouse.Controllers;
using AutoPartsWarehouse.Models;
using AutoPartsWarehouse.ViewComponents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoPartsWarehouseLab4.Tests;

/// <summary>
/// A part is critical the moment its stock reaches the minimum, including zero - a part with
/// nothing left on the shelf is the most critical case there is, not an exception to it. This
/// covers the three surfaces that compute criticality (AutoPartsController's criticalOnly
/// filter, ReportsController's statistics, and the new manager-facing banner) at the four
/// boundary values the audit asked for: 0, below minimum, exactly at minimum, above minimum.
/// </summary>
public class CriticalStockTests : IDisposable
{
    private readonly string _dbPath = TestDb.CreateNewDatabasePath();
    public void Dispose() => TestDb.Delete(_dbPath);
    private WarehouseContext NewContext() => TestDb.Open(_dbPath);

    [Theory]
    [InlineData(0, 5, true)]   // нульовий залишок - критично
    [InlineData(3, 5, true)]   // нижче мінімуму - критично
    [InlineData(5, 5, true)]   // точно на межі мінімуму - критично
    [InlineData(6, 5, false)]  // вище мінімуму - не критично
    [InlineData(0, 0, true)]   // немає запасу й мінімум теж 0 - усе одно критично (нічого немає на складі)
    public async Task AutoPartsIndex_CriticalOnlyFilter_MatchesBoundary(int quantity, int minimum, bool expectedCritical)
    {
        using var context = NewContext();
        var part = new AutoPart { Name = "Boundary Part", Manufacturer = "QA", QuantityInStock = quantity, MinimumStock = minimum };
        context.AutoParts.Add(part);
        await context.SaveChangesAsync();

        var controller = new AutoPartsController(context);
        var result = Assert.IsType<ViewResult>(await controller.Index(query: null, criticalOnly: true));
        var parts = Assert.IsAssignableFrom<IEnumerable<AutoPart>>(result.Model).ToList();

        Assert.Equal(expectedCritical, parts.Any(p => p.Id == part.Id));
    }

    [Theory]
    [InlineData(0, 5, 1)]
    [InlineData(3, 5, 1)]
    [InlineData(5, 5, 1)]
    [InlineData(6, 5, 0)]
    public async Task ManufacturerStatistics_CriticalPartCount_MatchesBoundary(int quantity, int minimum, int expectedCriticalCount)
    {
        using var context = NewContext();
        context.AutoParts.Add(new AutoPart { Name = "Boundary Part", Manufacturer = "QA-Boundary", QuantityInStock = quantity, MinimumStock = minimum });
        await context.SaveChangesAsync();

        var controller = new ReportsController(context);
        var result = Assert.IsType<ViewResult>(await controller.ManufacturerStatistics());
        var row = Assert.Single((List<AutoPartsWarehouse.ViewModels.ManufacturerStockViewModel>)result.Model!, r => r.Manufacturer == "QA-Boundary");

        Assert.Equal(expectedCriticalCount, row.CriticalPartCount);
    }

    [Theory]
    [InlineData(0, 5, 1)]
    [InlineData(3, 5, 1)]
    [InlineData(5, 5, 1)]
    [InlineData(6, 5, 0)]
    public async Task CriticalStockBanner_CountsBoundaryCasesForManager(int quantity, int minimum, int expectedCount)
    {
        using var context = NewContext();
        context.AutoParts.Add(new AutoPart { Name = "Boundary Part", Manufacturer = "QA", QuantityInStock = quantity, MinimumStock = minimum });
        await context.SaveChangesAsync();

        var component = new CriticalStockBannerViewComponent(context);
        var result = Assert.IsType<ViewViewComponentResult>(await component.InvokeAsync());

        Assert.Equal(expectedCount, result.ViewData!.Model);
    }

    [Fact]
    public async Task CriticalStockBanner_ReflectsStockAfterAWarehouseOperation_NotAStaleSnapshot()
    {
        using var context = NewContext();
        var part = new AutoPart { Name = "Freshness Part", Manufacturer = "QA", QuantityInStock = 10, MinimumStock = 5 };
        context.AutoParts.Add(part);
        context.Users.Add(new ApplicationUser { Id = "actor-1", UserName = "actor-1@lab4.local", Email = "actor-1@lab4.local", DisplayName = "QA Actor" });
        await context.SaveChangesAsync();

        var before = Assert.IsType<ViewViewComponentResult>(await new CriticalStockBannerViewComponent(context).InvokeAsync());
        Assert.Equal(0, before.ViewData!.Model); // 10 > 5, not critical yet

        var autoPartsController = new AutoPartsController(context);
        TestDb.ActAs(autoPartsController, "actor-1");
        await autoPartsController.AdjustStock(new AutoPartsWarehouse.ViewModels.StockAdjustmentInput
        {
            AutoPartId = part.Id,
            Delta = -6, // 10 -> 4, now below the minimum of 5
            Reason = "Перевірка актуальності банера після складської операції"
        });

        var after = Assert.IsType<ViewViewComponentResult>(await new CriticalStockBannerViewComponent(context).InvokeAsync());
        Assert.Equal(1, after.ViewData!.Model);
    }
}
