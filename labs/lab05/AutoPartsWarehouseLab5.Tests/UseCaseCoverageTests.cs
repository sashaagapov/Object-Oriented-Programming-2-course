using AutoPartsWarehouse.Controllers;
using AutoPartsWarehouse.Models;
using AutoPartsWarehouse.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AutoPartsWarehouseLab5.Tests;

[Trait("Category", "UseCases")]
public class UseCaseCoverageTests : IDisposable
{
    private readonly string _dbPath = TestDb.CreateNewDatabasePath();
    public void Dispose() => TestDb.Delete(_dbPath);
    private WarehouseContext NewContext() => TestDb.Open(_dbPath);

    [Fact]
    public async Task Catalog_FilteredByVehicleModel_ReturnsOnlyCompatibleParts()
    {
        int vehicleId;
        using (var context = NewContext())
        {
            var vehicle = new VehicleModel { Brand = "UseCaseBrand", ModelName = "Compat-1", ProductionYear = 2020 };
            var compatible = new AutoPart { Name = "UC сумісна деталь", Manufacturer = "UC", MinimumStock = 1 };
            var other = new AutoPart { Name = "UC несумісна деталь", Manufacturer = "UC", MinimumStock = 1 };
            vehicle.AutoParts.Add(compatible);
            context.AddRange(vehicle, other);
            await context.SaveChangesAsync();
            vehicleId = vehicle.Id;
        }

        using var actContext = NewContext();
        var controller = new AutoPartsController(actContext);
        TestDb.ActAs(controller, "worker-1");
        var result = await controller.Index("UC", false, vehicleId);

        var parts = Assert.IsAssignableFrom<IEnumerable<AutoPart>>(Assert.IsType<ViewResult>(result).Model).ToList();
        var only = Assert.Single(parts);
        Assert.Equal("UC сумісна деталь", only.Name);
    }

    [Fact]
    public async Task PurchaseOrderCreate_FromCriticalPart_PrefillsFirstItem()
    {
        int partId;
        using (var context = NewContext())
        {
            var part = new AutoPart { Name = "UC критична деталь", Manufacturer = "UC", MinimumStock = 5 };
            context.AutoParts.Add(part);
            await context.SaveChangesAsync();
            partId = part.Id;
        }

        using var actContext = NewContext();
        var controller = new PurchaseOrdersController(actContext);
        TestDb.ActAs(controller, "manager-1");
        var result = await controller.Create(partId);

        var model = Assert.IsType<CreatePurchaseOrderViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(partId, model.Items[0].AutoPartId);
    }

    private async Task<int> AddPartAsync(string name, int quantity, int minimum)
    {
        using var context = NewContext();
        var part = new AutoPart { Name = name, Manufacturer = "UC", MinimumStock = minimum, QuantityInStock = quantity };
        context.AutoParts.Add(part);
        await context.SaveChangesAsync();
        return part.Id;
    }

    private async Task<string> AddUserAsync(string id)
    {
        using var context = NewContext();
        if (await context.Users.FindAsync(id) == null)
        {
            context.Users.Add(new ApplicationUser { Id = id, UserName = id, NormalizedUserName = id.ToUpperInvariant(), DisplayName = id });
            await context.SaveChangesAsync();
        }
        return id;
    }

    [Fact]
    public async Task NotifyManager_CreatesOneOpenNotice_ForCriticalPart()
    {
        var partId = await AddPartAsync("UC сповіщення", quantity: 1, minimum: 3);
        var worker = await AddUserAsync("worker-notice");

        using (var context = NewContext())
        {
            var controller = new AutoPartsController(context);
            TestDb.ActAs(controller, worker);
            Assert.IsType<RedirectToActionResult>(await controller.NotifyManager(partId));
            Assert.IsType<RedirectToActionResult>(await controller.NotifyManager(partId));
        }

        using var verify = NewContext();
        var notice = Assert.Single(verify.CriticalStockNotices.Where(n => n.AutoPartId == partId));
        Assert.Equal(1, notice.QuantityAtNotice);
        Assert.Equal(worker, notice.NotifiedByUserId);
        Assert.Null(notice.AcknowledgedAt);
    }

    [Fact]
    public async Task NotifyManager_IsRejected_WhenStockIsAboveMinimum()
    {
        var partId = await AddPartAsync("UC достатньо", quantity: 10, minimum: 3);
        var worker = await AddUserAsync("worker-notice-2");

        using (var context = NewContext())
        {
            var controller = new AutoPartsController(context);
            TestDb.ActAs(controller, worker);
            await controller.NotifyManager(partId);
        }

        using var verify = NewContext();
        Assert.Empty(verify.CriticalStockNotices.Where(n => n.AutoPartId == partId));
    }

    [Fact]
    public async Task AcknowledgeNotice_ClosesIt_AndAllowsANewOneLater()
    {
        var partId = await AddPartAsync("UC повторне", quantity: 0, minimum: 2);
        var worker = await AddUserAsync("worker-notice-3");
        var manager = await AddUserAsync("manager-notice-3");

        int noticeId;
        using (var context = NewContext())
        {
            var controller = new AutoPartsController(context);
            TestDb.ActAs(controller, worker);
            await controller.NotifyManager(partId);
            noticeId = context.CriticalStockNotices.Single(n => n.AutoPartId == partId).Id;
        }
        using (var context = NewContext())
        {
            var controller = new AutoPartsController(context);
            TestDb.ActAs(controller, manager);
            await controller.AcknowledgeNotice(noticeId);
        }
        using (var context = NewContext())
        {
            var controller = new AutoPartsController(context);
            TestDb.ActAs(controller, worker);
            await controller.NotifyManager(partId);
        }

        using var verify = NewContext();
        var notices = verify.CriticalStockNotices.Where(n => n.AutoPartId == partId).ToList();
        Assert.Equal(2, notices.Count);
        Assert.Single(notices, n => n.AcknowledgedAt != null && n.AcknowledgedByUserId == manager);
    }

    [Fact]
    public async Task Database_RejectsSecondOpenNotice_ForTheSamePart()
    {
        var partId = await AddPartAsync("UC індекс", quantity: 0, minimum: 2);
        var worker = await AddUserAsync("worker-notice-4");

        using var context = NewContext();
        context.CriticalStockNotices.Add(new CriticalStockNotice { AutoPartId = partId, NotifiedByUserId = worker, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        context.CriticalStockNotices.Add(new CriticalStockNotice { AutoPartId = partId, NotifiedByUserId = worker, CreatedAt = DateTime.UtcNow });
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task PurchaseOrderCreate_WithUnknownPart_DoesNotPrefill()
    {
        using var context = NewContext();
        var controller = new PurchaseOrdersController(context);
        TestDb.ActAs(controller, "manager-1");
        var result = await controller.Create(999999);

        var model = Assert.IsType<CreatePurchaseOrderViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Null(model.Items[0].AutoPartId);
    }
}
