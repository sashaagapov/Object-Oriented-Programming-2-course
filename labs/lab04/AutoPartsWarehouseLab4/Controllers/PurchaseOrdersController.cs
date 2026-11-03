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

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var order = await _context.PurchaseOrders
            .Include(order => order.Items)
            .FirstOrDefaultAsync(order => order.Id == id);
        if (order == null)
        {
            return NotFound();
        }

        if (order.Status == PurchaseOrderStatus.Received)
        {
            TempData["Message"] = "Отримане замовлення редагувати не можна, оскільки його кількість уже додана на склад.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new EditPurchaseOrderViewModel
        {
            Id = order.Id,
            SupplierId = order.SupplierId,
            Items = order.Items.Select(item => new PurchaseOrderItemInput
            {
                AutoPartId = item.AutoPartId,
                Quantity = item.Quantity
            }).ToList()
        };
        await FillChoices(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditPurchaseOrderViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var order = await _context.PurchaseOrders
            .Include(order => order.Items)
            .FirstOrDefaultAsync(order => order.Id == id);
        if (order == null)
        {
            return NotFound();
        }

        if (order.Status == PurchaseOrderStatus.Received)
        {
            TempData["Message"] = "Отримане замовлення редагувати не можна.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var validItems = await ValidateOrder(model.SupplierId, model.Items);
        if (!ModelState.IsValid)
        {
            await FillChoices(model);
            return View(model);
        }

        order.SupplierId = model.SupplierId;
        _context.PurchaseOrderItems.RemoveRange(order.Items);
        order.Items = validItems.Select(item => new PurchaseOrderItem
        {
            AutoPartId = item.AutoPartId!.Value,
            Quantity = item.Quantity!.Value
        }).ToList();
        await _context.SaveChangesAsync();
        TempData["Success"] = "Замовлення оновлено.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
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

        if (order.Status == PurchaseOrderStatus.Received)
        {
            TempData["Message"] = "Отримане замовлення видалити не можна, оскільки воно вже вплинуло на залишки.";
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(order);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var order = await _context.PurchaseOrders
            .Include(order => order.Items)
            .FirstOrDefaultAsync(order => order.Id == id);
        if (order == null)
        {
            return NotFound();
        }

        if (order.Status == PurchaseOrderStatus.Received)
        {
            TempData["Message"] = "Отримане замовлення видалити не можна.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _context.PurchaseOrderItems.RemoveRange(order.Items);
        _context.PurchaseOrders.Remove(order);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Неотримане замовлення видалено.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Receive(int id)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
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

        var quantities = order.Items.GroupBy(item => item.AutoPartId)
            .Select(group => new { Part = group.First().AutoPart, Quantity = group.Sum(item => (long)item.Quantity) })
            .ToList();

        if (order.Items.Count == 0 || order.Items.Any(item => item.Quantity <= 0) ||
            quantities.Any(row => row.Part.QuantityInStock + row.Quantity > int.MaxValue))
        {
            TempData["Message"] = "Неможливо отримати замовлення: перевірте позиції та допустиму кількість запчастин.";
            return RedirectToAction(nameof(Details), new { id });
        }

        foreach (var row in quantities)
        {
            row.Part.QuantityInStock += (int)row.Quantity;
        }

        order.Status = PurchaseOrderStatus.Received;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
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

    private async Task FillChoices(EditPurchaseOrderViewModel model)
    {
        model.Suppliers = new SelectList(
            await _context.Suppliers.OrderBy(supplier => supplier.Name).ToListAsync(),
            "Id", "Name", model.SupplierId);
        model.AutoParts = new SelectList(
            await _context.AutoParts.OrderBy(part => part.Name).ToListAsync(),
            "Id", "Name");
    }

    private async Task<List<PurchaseOrderItemInput>> ValidateOrder(int supplierId, List<PurchaseOrderItemInput> items)
    {
        if (!await _context.Suppliers.AnyAsync(supplier => supplier.Id == supplierId))
        {
            ModelState.AddModelError(nameof(supplierId), "Виберіть наявного постачальника.");
        }

        var filledItems = items
            .Select((item, index) => new { item, index })
            .Where(row => row.item.AutoPartId.HasValue || row.item.Quantity.HasValue)
            .ToList();
        if (filledItems.Count == 0)
        {
            ModelState.AddModelError(nameof(items), "Додайте хоча б одну запчастину.");
        }

        var partIds = filledItems.Where(row => row.item.AutoPartId.HasValue)
            .Select(row => row.item.AutoPartId!.Value).Distinct().ToList();
        var existingPartIds = await _context.AutoParts.Where(part => partIds.Contains(part.Id))
            .Select(part => part.Id).ToListAsync();
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

        return filledItems.Select(row => row.item).ToList();
    }
}
