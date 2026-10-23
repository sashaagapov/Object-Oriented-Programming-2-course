using AutoPartsWarehouse.Models;
using AutoPartsWarehouse.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AutoPartsWarehouse.Controllers;

public class PurchaseOrdersController : Controller
{
    private readonly WarehouseContext _context;

    public PurchaseOrdersController(WarehouseContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var orders = await _context.PurchaseOrders
            .Include(order => order.Supplier)
            .Include(order => order.Items)
            .OrderByDescending(order => order.OrderDate)
            .ToListAsync();

        return View(orders);
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await _context.PurchaseOrders
            .Include(order => order.Supplier)
            .Include(order => order.Items)
                .ThenInclude(item => item.AutoPart)
            .FirstOrDefaultAsync(order => order.Id == id);

        if (order == null)
        {
            return NotFound();
        }

        return View(order);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new CreatePurchaseOrderViewModel
        {
            Items = new List<PurchaseOrderItemInput>
            {
                new(), new()
            }
        };
        await FillChoices(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePurchaseOrderViewModel model)
    {
        if (!await _context.Suppliers.AnyAsync(supplier => supplier.Id == model.SupplierId))
        {
            ModelState.AddModelError(nameof(model.SupplierId), "Виберіть наявного постачальника.");
        }

        var filledItems = model.Items
            .Select((item, index) => new { item, index })
            .Where(row => row.item.AutoPartId.HasValue || row.item.Quantity.HasValue)
            .ToList();

        if (filledItems.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Items), "Додайте хоча б одну запчастину.");
        }

        var partIds = filledItems
            .Where(row => row.item.AutoPartId.HasValue)
            .Select(row => row.item.AutoPartId!.Value)
            .Distinct()
            .ToList();
        var existingPartIds = await _context.AutoParts
            .Where(part => partIds.Contains(part.Id))
            .Select(part => part.Id)
            .ToListAsync();

        foreach (var row in filledItems)
        {
            if (!row.item.AutoPartId.HasValue || !existingPartIds.Contains(row.item.AutoPartId.Value))
            {
                ModelState.AddModelError($"Items[{row.index}].AutoPartId", "Виберіть наявну запчастину.");
            }

            if (!row.item.Quantity.HasValue || row.item.Quantity.Value <= 0)
            {
                ModelState.AddModelError($"Items[{row.index}].Quantity", "Кількість повинна бути більшою за 0.");
            }
        }

        if (!ModelState.IsValid)
        {
            await FillChoices(model);
            return View(model);
        }

        var order = new PurchaseOrder
        {
            SupplierId = model.SupplierId,
            Items = filledItems.Select(row => new PurchaseOrderItem
            {
                AutoPartId = row.item.AutoPartId!.Value,
                Quantity = row.item.Quantity!.Value
            }).ToList()
        };

        _context.PurchaseOrders.Add(order);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = order.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Receive(int id)
    {
        var order = await _context.PurchaseOrders
            .Include(order => order.Items)
                .ThenInclude(item => item.AutoPart)
            .FirstOrDefaultAsync(order => order.Id == id);

        if (order == null)
        {
            return NotFound();
        }

        if (order.Status == PurchaseOrderStatus.Received)
        {
            TempData["Message"] = "Це замовлення вже отримано.";
            return RedirectToAction(nameof(Details), new { id });
        }

        foreach (var item in order.Items)
        {
            item.AutoPart.QuantityInStock += item.Quantity;
        }

        order.Status = PurchaseOrderStatus.Received;
        await _context.SaveChangesAsync();
        TempData["Message"] = "Замовлення отримано. Залишки оновлено.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task FillChoices(CreatePurchaseOrderViewModel model)
    {
        model.Suppliers = new SelectList(
            await _context.Suppliers.OrderBy(supplier => supplier.Name).ToListAsync(),
            "Id", "Name");
        model.AutoParts = new SelectList(
            await _context.AutoParts.OrderBy(part => part.Name).ToListAsync(),
            "Id", "Name");
    }
}
