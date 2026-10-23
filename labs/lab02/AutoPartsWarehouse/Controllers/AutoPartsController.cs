using AutoPartsWarehouse.Models;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoPartsWarehouse.ViewModels;
namespace AutoPartsWarehouse.Controllers;

public class AutoPartsController : Controller
{
    private readonly WarehouseContext _context;

    public AutoPartsController(WarehouseContext context)
    {
        _context = context;
    }
    public async Task<IActionResult> Index(string? query, bool criticalOnly = false)
    {
        var autoParts = _context.AutoParts.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            query = query.Trim();
            autoParts = autoParts.Where(part =>
                part.Name.Contains(query) || part.Manufacturer.Contains(query));
        }

        if (criticalOnly)
        {
            autoParts = autoParts.Where(part =>
                part.QuantityInStock > 0 && part.QuantityInStock <= part.MinimumStock);
        }

        ViewBag.Query = query;
        ViewBag.CriticalOnly = criticalOnly;
        return View(await autoParts.OrderBy(part => part.Name).ToListAsync());
    }
    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Manufacturer,QuantityInStock,MinimumStock")] AutoPart autoPart)
    {
        NormalizeNameAndManufacturer(autoPart);

        if (ModelState.IsValid && await PartAlreadyExists(autoPart.Name, autoPart.Manufacturer))
        {
            ModelState.AddModelError(string.Empty,
                "Запчастина з такою назвою та виробником уже існує. Змініть її кількість у наявному записі.");
        }

        if (!ModelState.IsValid)
        {
            return View(autoPart);
        }

        _context.AutoParts.Add(autoPart);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            ModelState.AddModelError(string.Empty,
                "Запчастина з такою назвою та виробником уже існує. Змініть її кількість у наявному записі.");
            return View(autoPart);
        }

        return RedirectToAction(nameof(Index));
    }
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var autoPart = await _context.AutoParts.FindAsync(id);

        if (autoPart == null)
        {
            return NotFound();
        }

        return View(autoPart);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Manufacturer,QuantityInStock,MinimumStock")] AutoPart autoPart)
    {
        if (id != autoPart.Id)
        {
            return BadRequest();
        }

        NormalizeNameAndManufacturer(autoPart);

        if (ModelState.IsValid && await PartAlreadyExists(autoPart.Name, autoPart.Manufacturer, id))
        {
            ModelState.AddModelError(string.Empty,
                "Запчастина з такою назвою та виробником уже існує.");
        }

        if (!ModelState.IsValid)
        {
            return View(autoPart);
        }

        var existingPart = await _context.AutoParts.FindAsync(id);
        if (existingPart == null)
        {
            return NotFound();
        }

        existingPart.Name = autoPart.Name;
        existingPart.Manufacturer = autoPart.Manufacturer;
        existingPart.QuantityInStock = autoPart.QuantityInStock;
        existingPart.MinimumStock = autoPart.MinimumStock;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            ModelState.AddModelError(string.Empty,
                "Запчастина з такою назвою та виробником уже існує.");
            return View(autoPart);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> PartAlreadyExists(string name, string manufacturer, int? excludedId = null)
    {
        var existingParts = await _context.AutoParts
            .Where(part => !excludedId.HasValue || part.Id != excludedId.Value)
            .Select(part => new { part.Name, part.Manufacturer })
            .ToListAsync();

        return existingParts.Any(part =>
            string.Equals(part.Name.Trim(), name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(part.Manufacturer.Trim(), manufacturer, StringComparison.OrdinalIgnoreCase));
    }

    private static void NormalizeNameAndManufacturer(AutoPart autoPart)
    {
        autoPart.Name = (autoPart.Name ?? string.Empty).Trim();
        autoPart.Manufacturer = (autoPart.Manufacturer ?? string.Empty).Trim();
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqliteException { SqliteErrorCode: 19 };
    }
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var autoPart = await _context.AutoParts
            .FirstOrDefaultAsync(part => part.Id == id);

        if (autoPart == null)
        {
            return NotFound();
        }

        return View(autoPart);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var autoPart = await _context.AutoParts.FindAsync(id);

        if (autoPart == null)
        {
            return NotFound();
        }

        if (await _context.PartIssues.AnyAsync(issue => issue.AutoPartId == id) ||
            await _context.PurchaseOrderItems.AnyAsync(item => item.AutoPartId == id))
        {
            ModelState.AddModelError(string.Empty,
                "Запчастину з історією видач або замовлень видалити не можна.");
            return View("Delete", autoPart);
        }

        _context.AutoParts.Remove(autoPart);

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
    public async Task<IActionResult> Details(int id)
    {
        var autoPart = await _context.AutoParts
            .Include(part => part.VehicleModels)
            .Include(part => part.PartIssues)
            .FirstOrDefaultAsync(part => part.Id == id);

        if (autoPart == null)
        {
            return NotFound();
        }

        return View(autoPart);
    }
    [HttpGet]
    public async Task<IActionResult> Issue(int id)
    {
        var autoPart = await _context.AutoParts.FindAsync(id);

        if (autoPart == null)
        {
            return NotFound();
        }

        var viewModel = new IssuePartViewModel
        {
            AutoPartId = autoPart.Id,
            PartName = autoPart.Name,
            QuantityInStock = autoPart.QuantityInStock,
            Quantity = 1
        };

        return View(viewModel);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(IssuePartViewModel viewModel)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var autoPart = await _context.AutoParts.FindAsync(viewModel.AutoPartId);

        if (autoPart == null)
        {
            return NotFound();
        }

        if (viewModel.Quantity <= 0)
        {
            ModelState.AddModelError(
                nameof(viewModel.Quantity),
                "Кількість повинна бути більшою за 0."
            );
        }

        if (viewModel.Quantity > autoPart.QuantityInStock)
        {
            ModelState.AddModelError(
                nameof(viewModel.Quantity),
                "На складі недостатньо запчастин."
            );
        }

        if (!viewModel.IsPaymentConfirmed)
        {
            ModelState.AddModelError(
                nameof(viewModel.IsPaymentConfirmed),
                "Оплата повинна бути підтверджена."
            );
        }

        if (!ModelState.IsValid)
        {
            viewModel.PartName = autoPart.Name;
            viewModel.QuantityInStock = autoPart.QuantityInStock;

            return View(viewModel);
        }

        var partIssue = new PartIssue
        {
            AutoPartId = autoPart.Id,
            Quantity = viewModel.Quantity,
            IssuedAt = DateTime.Now
        };

        autoPart.QuantityInStock -= viewModel.Quantity;

        _context.PartIssues.Add(partIssue);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return RedirectToAction(nameof(Index));
    }
}
