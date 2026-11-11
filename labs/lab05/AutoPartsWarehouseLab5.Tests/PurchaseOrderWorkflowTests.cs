using AutoPartsWarehouse.Controllers;
using AutoPartsWarehouse.Models;
using AutoPartsWarehouse.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoPartsWarehouseLab5.Tests;

[Trait("Category", "CRUD")]
public class PurchaseOrderWorkflowTests : IDisposable
{
    private readonly string _dbPath = TestDb.CreateNewDatabasePath();
    public void Dispose() => TestDb.Delete(_dbPath);
    private WarehouseContext NewContext() => TestDb.Open(_dbPath);

    private async Task<(int supplierId, int partId)> SeedSupplierAndPartAsync()
    {
        using var context = NewContext();
        var supplier = new Supplier { Name = "PO Supplier " + Guid.NewGuid(), Phone = "+380001112233" };
        var part = new AutoPart { Name = "PO Part " + Guid.NewGuid(), Manufacturer = "QA", QuantityInStock = 5, MinimumStock = 1 };
        context.Suppliers.Add(supplier);
        context.AutoParts.Add(part);
        await context.SaveChangesAsync();
        return (supplier.Id, part.Id);
    }

    [Fact]
    public async Task Create_AddsOrderWithItems_InCreatedStatus()
    {
        var (supplierId, partId) = await SeedSupplierAndPartAsync();
        int orderId;

        using (var context = NewContext())
        {
            var controller = new PurchaseOrdersController(context);
            TestDb.ActAs(controller, "manager-1");
            var model = new CreatePurchaseOrderViewModel
            {
                SupplierId = supplierId,
                Items = new List<PurchaseOrderItemInput> { new() { AutoPartId = partId, Quantity = 10 } }
            };
            var result = await controller.Create(model);
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            orderId = (int)redirect.RouteValues!["id"]!;
        }

        using var verify = NewContext();
        var order = await verify.PurchaseOrders.Include(o => o.Items).SingleAsync(o => o.Id == orderId);
        Assert.Equal(PurchaseOrderStatus.Created, order.Status);
        Assert.Single(order.Items);
        Assert.Equal(10, order.Items.Single().Quantity);
    }

    [Fact]
    public async Task Delete_CancelsInsteadOfRemovingTheRow_AndKeepsTheHistory()
    {
        var (supplierId, partId) = await SeedSupplierAndPartAsync();
        int orderId;
        using (var context = NewContext())
        {
            var order = new PurchaseOrder { SupplierId = supplierId, Items = { new PurchaseOrderItem { AutoPartId = partId, Quantity = 4 } } };
            context.PurchaseOrders.Add(order);
            await context.SaveChangesAsync();
            orderId = order.Id;
        }

        using (var context = NewContext())
        {
            var controller = new PurchaseOrdersController(context);
            TestDb.ActAs(controller, "manager-1");
            var result = await controller.DeleteConfirmed(orderId, "Помилка в замовленні");
            Assert.IsType<RedirectToActionResult>(result);
        }

        using var verify = NewContext();
        var order2 = await verify.PurchaseOrders.FindAsync(orderId);
        Assert.NotNull(order2); // рядок не видалено фізично
        Assert.Equal(PurchaseOrderStatus.Cancelled, order2!.Status);
        Assert.Equal("Помилка в замовленні", order2.CancellationReason);
        Assert.NotNull(order2.CancelledAt);
    }

    [Fact]
    public async Task Receive_IncreasesStock_AndMarksOrderReceived()
    {
        var (supplierId, partId) = await SeedSupplierAndPartAsync();
        int orderId;
        using (var context = NewContext())
        {
            var seedOrder = new PurchaseOrder { SupplierId = supplierId, Items = { new PurchaseOrderItem { AutoPartId = partId, Quantity = 4 } } };
            context.PurchaseOrders.Add(seedOrder);
            await context.SaveChangesAsync();
            orderId = seedOrder.Id;
        }

        using (var context = NewContext())
        {
            var controller = new PurchaseOrdersController(context);
            TestDb.ActAs(controller, "worker-1");
            var result = await controller.Receive(orderId);
            Assert.IsType<RedirectToActionResult>(result);
        }

        using var verify = NewContext();
        var part = await verify.AutoParts.FindAsync(partId);
        var order = await verify.PurchaseOrders.FindAsync(orderId);
        Assert.Equal(9, part!.QuantityInStock); // 5 (seed) + 4 (замовлення)
        Assert.Equal(PurchaseOrderStatus.Received, order!.Status);
    }

    [Fact]
    public async Task ReceivedOrder_CannotBeReceivedAgain_OrCancelled()
    {
        var (supplierId, partId) = await SeedSupplierAndPartAsync();
        int orderId;
        using (var context = NewContext())
        {
            var seedOrder = new PurchaseOrder { SupplierId = supplierId, Status = PurchaseOrderStatus.Received, Items = { new PurchaseOrderItem { AutoPartId = partId, Quantity = 4 } } };
            context.PurchaseOrders.Add(seedOrder);
            await context.SaveChangesAsync();
            orderId = seedOrder.Id;
        }

        using (var context = NewContext())
        {
            var controller = new PurchaseOrdersController(context);
            TestDb.ActAs(controller, "worker-1");
            await controller.Receive(orderId); // повторне отримання
        }

        using (var context = NewContext())
        {
            var controller = new PurchaseOrdersController(context);
            TestDb.ActAs(controller, "manager-1");
            await controller.DeleteConfirmed(orderId, "Спроба анулювати вже отримане"); // скасування вже отриманого
        }

        using var verify = NewContext();
        var part = await verify.AutoParts.FindAsync(partId);
        var order = await verify.PurchaseOrders.FindAsync(orderId);
        Assert.Equal(5, part!.QuantityInStock); // залишок не зріс вдруге
        Assert.Equal(PurchaseOrderStatus.Received, order!.Status); // статус не змінився на Cancelled
        Assert.Null(order.CancelledAt);
    }
}
