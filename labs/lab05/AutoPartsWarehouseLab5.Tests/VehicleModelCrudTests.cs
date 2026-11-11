using AutoPartsWarehouse.Controllers;
using AutoPartsWarehouse.Models;
using AutoPartsWarehouse.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoPartsWarehouseLab5.Tests;

[Trait("Category", "CRUD")]
public class VehicleModelCrudTests : IDisposable
{
    private readonly string _dbPath = TestDb.CreateNewDatabasePath();
    public void Dispose() => TestDb.Delete(_dbPath);
    private WarehouseContext NewContext() => TestDb.Open(_dbPath);

    [Fact]
    public async Task Create_ThenEdit_PersistsFieldsAndLinkedAutoParts()
    {
        int partId, modelId;
        using (var context = NewContext())
        {
            var part = new AutoPart { Name = "Linked Part", Manufacturer = "QA", QuantityInStock = 1, MinimumStock = 1 };
            context.AutoParts.Add(part);
            await context.SaveChangesAsync();
            partId = part.Id;
        }

        using (var context = NewContext())
        {
            var controller = new VehicleModelsController(context);
            TestDb.ActAs(controller, "manager-1");
            var result = await controller.Create(new VehicleModelViewModel
            {
                Brand = "Toyota", ModelName = "Corolla", ProductionYear = 2020, SelectedAutoPartIds = new List<int> { partId }
            });
            Assert.IsType<RedirectToActionResult>(result);
            modelId = await context.VehicleModels.Select(v => v.Id).SingleAsync();
        }

        using var verify = NewContext();
        var vehicle = await verify.VehicleModels.Include(v => v.AutoParts).SingleAsync(v => v.Id == modelId);
        Assert.Equal("Toyota", vehicle.Brand);
        Assert.Single(vehicle.AutoParts);
    }

    [Fact]
    public async Task Delete_RemovesVehicleModel_ButLeavesLinkedAutoPartIntact()
    {
        int partId, modelId;
        using (var context = NewContext())
        {
            var seedPart = new AutoPart { Name = "Untouched Part", Manufacturer = "QA", QuantityInStock = 7, MinimumStock = 1 };
            context.AutoParts.Add(seedPart);
            await context.SaveChangesAsync();
            partId = seedPart.Id;

            var vehicle = new VehicleModel { Brand = "BMW", ModelName = "X5", ProductionYear = 2019, AutoParts = { seedPart } };
            context.VehicleModels.Add(vehicle);
            await context.SaveChangesAsync();
            modelId = vehicle.Id;
        }

        using (var context = NewContext())
        {
            var controller = new VehicleModelsController(context);
            TestDb.ActAs(controller, "manager-1");
            var result = await controller.DeleteConfirmed(modelId);
            Assert.IsType<RedirectToActionResult>(result);
        }

        using var verify = NewContext();
        Assert.False(await verify.VehicleModels.AnyAsync(v => v.Id == modelId));
        var part = await verify.AutoParts.FindAsync(partId);
        Assert.NotNull(part); // видалення моделі авто не є історичним документом - запчастину не чіпає
        Assert.Equal(7, part!.QuantityInStock);
    }
}
