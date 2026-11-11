using AutoPartsWarehouse.Controllers;
using AutoPartsWarehouse.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoPartsWarehouseLab5.Tests;

[Trait("Category", "CRUD")]
public class SupplierCrudTests : IDisposable
{
    private readonly string _dbPath = TestDb.CreateNewDatabasePath();
    public void Dispose() => TestDb.Delete(_dbPath);
    private WarehouseContext NewContext() => TestDb.Open(_dbPath);

    [Fact]
    public async Task Create_ThenEdit_PersistsTheNewValues()
    {
        int supplierId;
        using (var context = NewContext())
        {
            var controller = new SuppliersController(context);
            TestDb.ActAs(controller, "manager-1");
            var result = await controller.Create(new Supplier { Name = "Постачальник CRUD", Phone = "+380001112233" });
            Assert.IsType<RedirectToActionResult>(result);
            supplierId = await context.Suppliers.Where(s => s.Name == "Постачальник CRUD").Select(s => s.Id).SingleAsync();
        }

        using (var context = NewContext())
        {
            var controller = new SuppliersController(context);
            TestDb.ActAs(controller, "manager-1");
            var result = await controller.Edit(supplierId, new Supplier { Id = supplierId, Name = "Постачальник оновлений", Phone = "+380009998877" });
            Assert.IsType<RedirectToActionResult>(result);
        }

        using var verify = NewContext();
        var supplier = await verify.Suppliers.FindAsync(supplierId);
        Assert.Equal("Постачальник оновлений", supplier!.Name);
        Assert.Equal("+380009998877", supplier.Phone);
    }

    [Fact]
    public async Task Delete_IsBlocked_WhenSupplierHasPurchaseOrderHistory()
    {
        int supplierId;
        using (var context = NewContext())
        {
            var part = new AutoPart { Name = "Supplier Test Part", Manufacturer = "QA", QuantityInStock = 0, MinimumStock = 1 };
            var supplier = new Supplier { Name = "Linked Supplier", Phone = "+380001112233" };
            context.AutoParts.Add(part);
            context.Suppliers.Add(supplier);
            await context.SaveChangesAsync();
            supplierId = supplier.Id;

            context.PurchaseOrders.Add(new PurchaseOrder
            {
                SupplierId = supplierId,
                Items = { new PurchaseOrderItem { AutoPartId = part.Id, Quantity = 1 } }
            });
            await context.SaveChangesAsync();
        }

        using (var context = NewContext())
        {
            var controller = new SuppliersController(context);
            TestDb.ActAs(controller, "manager-1");
            var result = await controller.DeleteConfirmed(supplierId);
            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
        }

        using var verify = NewContext();
        Assert.True(await verify.Suppliers.AnyAsync(s => s.Id == supplierId));
    }
}
