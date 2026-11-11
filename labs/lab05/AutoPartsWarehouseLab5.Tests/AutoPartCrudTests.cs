using AutoPartsWarehouse.Controllers;
using AutoPartsWarehouse.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoPartsWarehouseLab5.Tests;

[Trait("Category", "CRUD")]
public class AutoPartCrudTests : IDisposable
{
    private readonly string _dbPath = TestDb.CreateNewDatabasePath();
    public void Dispose() => TestDb.Delete(_dbPath);
    private WarehouseContext NewContext() => TestDb.Open(_dbPath);

    [Fact]
    public async Task Create_ThenEdit_PersistsTheNewValues()
    {
        int partId;
        using (var context = NewContext())
        {
            var controller = new AutoPartsController(context);
            TestDb.ActAs(controller, "manager-1");
            var result = await controller.Create(new AutoPart { Name = " Фільтр CRUD ", Manufacturer = " Bosch ", MinimumStock = 2 });
            Assert.IsType<RedirectToActionResult>(result);
            partId = await context.AutoParts.Where(part => part.Name == "Фільтр CRUD").Select(part => part.Id).SingleAsync();
        }

        using (var context = NewContext())
        {
            var controller = new AutoPartsController(context);
            TestDb.ActAs(controller, "manager-1");
            var result = await controller.Edit(partId, new AutoPart { Id = partId, Name = "Фільтр CRUD оновлений", Manufacturer = "Bosch", MinimumStock = 5 });
            Assert.IsType<RedirectToActionResult>(result);
        }

        using var verify = NewContext();
        var part = await verify.AutoParts.FindAsync(partId);
        Assert.Equal("Фільтр CRUD оновлений", part!.Name);
        Assert.Equal(5, part.MinimumStock);
    }

    [Fact]
    public async Task Delete_IsBlocked_WhenAutoPartHasIssueHistory()
    {
        int partId;
        using (var context = NewContext())
        {
            var part = new AutoPart { Name = "Part With History", Manufacturer = "QA", QuantityInStock = 5, MinimumStock = 1 };
            context.AutoParts.Add(part);
            await context.SaveChangesAsync();
            partId = part.Id;
            context.PartIssues.Add(new PartIssue { AutoPartId = partId, Quantity = 1, Status = PartIssueStatus.Pending, CreatedAt = DateTime.UtcNow, CreatedByUserId = "someone" });
            await context.SaveChangesAsync();
        }

        using (var context = NewContext())
        {
            var controller = new AutoPartsController(context);
            TestDb.ActAs(controller, "manager-1");
            var result = await controller.DeleteConfirmed(partId);
            Assert.IsType<ViewResult>(result); // повертає ту саму сторінку Delete з помилкою, не видаляє
            Assert.False(controller.ModelState.IsValid);
        }

        using var verify = NewContext();
        Assert.True(await verify.AutoParts.AnyAsync(part => part.Id == partId));
    }

    [Fact]
    public async Task Delete_Succeeds_WhenAutoPartHasNoHistory()
    {
        int partId;
        using (var context = NewContext())
        {
            var part = new AutoPart { Name = "Part Without History", Manufacturer = "QA", QuantityInStock = 0, MinimumStock = 1 };
            context.AutoParts.Add(part);
            await context.SaveChangesAsync();
            partId = part.Id;
        }

        using (var context = NewContext())
        {
            var controller = new AutoPartsController(context);
            TestDb.ActAs(controller, "manager-1");
            var result = await controller.DeleteConfirmed(partId);
            Assert.IsType<RedirectToActionResult>(result);
        }

        using var verify = NewContext();
        Assert.False(await verify.AutoParts.AnyAsync(part => part.Id == partId));
    }
}
