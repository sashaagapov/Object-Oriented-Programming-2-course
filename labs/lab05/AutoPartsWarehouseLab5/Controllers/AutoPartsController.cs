using AutoPartsWarehouse.Models;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoPartsWarehouse.ViewModels;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
namespace AutoPartsWarehouse.Controllers;

[Authorize(Roles = WarehouseRoles.Staff)]
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
            // Нульовий залишок теж критичний: запчастини зовсім немає на складі.
            autoParts = autoParts.Where(part =>
                part.QuantityInStock <= part.MinimumStock);
        }

        ViewBag.Query = query;
        ViewBag.CriticalOnly = criticalOnly;
        return View(await autoParts.OrderBy(part => part.Name).ToListAsync());
    }
    [HttpGet]
    [Authorize(Roles = WarehouseRoles.Manager)]
    public IActionResult Create()
    {
        return View();
    }
    [HttpPost]
    [Authorize(Roles = WarehouseRoles.Manager)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Manufacturer,MinimumStock")] AutoPart autoPart)
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
    [Authorize(Roles = WarehouseRoles.Manager)]
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
    [Authorize(Roles = WarehouseRoles.Manager)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Manufacturer,MinimumStock")] AutoPart autoPart)
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
    [Authorize(Roles = WarehouseRoles.Manager)]
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
    [Authorize(Roles = WarehouseRoles.Manager)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var autoPart = await _context.AutoParts.FindAsync(id);

        if (autoPart == null)
        {
            return NotFound();
        }

        if (await _context.PartIssues.AnyAsync(issue => issue.AutoPartId == id) ||
            await _context.StockAdjustments.AnyAsync(adjustment => adjustment.AutoPartId == id) ||
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
            .Include(part => part.StockAdjustments)
            .FirstOrDefaultAsync(part => part.Id == id);

        if (autoPart == null)
        {
            return NotFound();
        }

        ViewBag.UserNames = await _context.Users.ToDictionaryAsync(user => user.Id, user => user.DisplayName);
        return View(autoPart);
    }
    [Authorize(Roles = WarehouseRoles.Staff)]
    public async Task<IActionResult> Issues()
    {
        var issues = await _context.PartIssues.Include(issue => issue.AutoPart)
            .OrderByDescending(issue => issue.CreatedAt).ThenByDescending(issue => issue.Id).ToListAsync();
        ViewBag.UserNames = await _context.Users.ToDictionaryAsync(user => user.Id, user => user.DisplayName);
        return View(issues);
    }

    [HttpGet]
    [Authorize(Roles = WarehouseRoles.WarehouseWorker)]
    public async Task<IActionResult> Issue(int id)
    {
        var part = await _context.AutoParts.FindAsync(id);
        if (part == null) return NotFound();
        return View(new IssuePartViewModel { AutoPartId = id, PartName = part.Name, QuantityInStock = part.QuantityInStock, Quantity = 1 });
    }

    [HttpPost]
    [Authorize(Roles = WarehouseRoles.WarehouseWorker)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(IssuePartViewModel model)
    {
        var part = await _context.AutoParts.FindAsync(model.AutoPartId);
        if (part == null) return NotFound();
        if (model.Quantity <= 0) ModelState.AddModelError(nameof(model.Quantity), "Кількість має бути більшою за нуль.");
        if (!ModelState.IsValid)
        {
            model.PartName = part.Name;
            model.QuantityInStock = part.QuantityInStock;
            return View(model);
        }
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor == null) return Forbid();
        _context.PartIssues.Add(new PartIssue
        {
            AutoPartId = part.Id,
            Quantity = model.Quantity,
            Status = PartIssueStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = actor
        });
        await _context.SaveChangesAsync();
        TempData["Success"] = "Майбутню видачу оформлено. Залишок не змінено.";
        return RedirectToAction(nameof(Issues));
    }

    [HttpPost]
    [Authorize(Roles = WarehouseRoles.Manager)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPayment(int id)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var issue = await _context.PartIssues.FindAsync(id);
        if (issue == null) return NotFound();
        if (issue.Status != PartIssueStatus.Pending || issue.IsPaymentConfirmed)
        {
            TempData["Error"] = "Оплату можна підтвердити лише один раз для очікуваної видачі.";
            return RedirectToAction(nameof(Issues));
        }
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor == null) return Forbid();
        issue.IsPaymentConfirmed = true;
        issue.PaymentConfirmedAt = DateTime.UtcNow;
        issue.PaymentConfirmedByUserId = actor;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = "Оплату підтверджено менеджером.";
        return RedirectToAction(nameof(Issues));
    }

    [HttpPost]
    [Authorize(Roles = WarehouseRoles.WarehouseWorker)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteIssue(int id)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var issue = await _context.PartIssues.Include(item => item.AutoPart).FirstOrDefaultAsync(item => item.Id == id);
        if (issue == null) return NotFound();
        if (issue.Status != PartIssueStatus.Pending || !issue.IsPaymentConfirmed)
        {
            TempData["Error"] = "Видати можна тільки очікуваний документ із підтвердженою оплатою.";
            return RedirectToAction(nameof(Issues));
        }
        if (issue.Quantity <= 0 || issue.Quantity > issue.AutoPart.QuantityInStock)
        {
            TempData["Error"] = "Недостатній залишок для фактичної видачі.";
            return RedirectToAction(nameof(Issues));
        }
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor == null) return Forbid();
        issue.AutoPart.QuantityInStock -= issue.Quantity;
        issue.Status = PartIssueStatus.Issued;
        issue.IssuedAt = DateTime.UtcNow;
        issue.IssuedByUserId = actor;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = "Запчастину фактично видано. Залишок зменшено.";
        return RedirectToAction(nameof(Issues));
    }

    [HttpPost]
    [Authorize(Roles = WarehouseRoles.WarehouseWorker)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelIssue(int id, string reason)
    {
        reason = (reason ?? string.Empty).Trim();
        if (reason.Length == 0 || reason.Length > 500)
        {
            TempData["Error"] = "Вкажіть причину анулювання (до 500 символів).";
            return RedirectToAction(nameof(Issues));
        }
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var issue = await _context.PartIssues.FindAsync(id);
        if (issue == null) return NotFound();
        if (issue.Status != PartIssueStatus.Pending)
        {
            TempData["Error"] = "Анулювати можна лише очікувану видачу.";
            return RedirectToAction(nameof(Issues));
        }
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor == null) return Forbid();
        issue.Status = PartIssueStatus.Cancelled;
        issue.CancelledAt = DateTime.UtcNow;
        issue.CancelledByUserId = actor;
        issue.CancellationReason = reason;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = "Очікувану видачу анульовано без зміни залишку.";
        return RedirectToAction(nameof(Issues));
    }

    [HttpPost]
    [Authorize(Roles = WarehouseRoles.WarehouseWorker)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnIssue(int id, string reason)
    {
        reason = (reason ?? string.Empty).Trim();
        if (reason.Length == 0 || reason.Length > 500)
        {
            TempData["Error"] = "Вкажіть причину повернення (до 500 символів).";
            return RedirectToAction(nameof(Issues));
        }
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var issue = await _context.PartIssues.Include(item => item.AutoPart).FirstOrDefaultAsync(item => item.Id == id);
        if (issue == null) return NotFound();
        if (issue.Status != PartIssueStatus.Issued)
        {
            TempData["Error"] = "Повернути можна лише фактично видану запчастину.";
            return RedirectToAction(nameof(Issues));
        }
        if ((long)issue.AutoPart.QuantityInStock + issue.Quantity > int.MaxValue)
        {
            TempData["Error"] = "Повернення перевищить допустимий залишок.";
            return RedirectToAction(nameof(Issues));
        }
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor == null) return Forbid();
        issue.AutoPart.QuantityInStock += issue.Quantity;
        issue.Status = PartIssueStatus.Returned;
        issue.ReturnedAt = DateTime.UtcNow;
        issue.ReturnedByUserId = actor;
        issue.ReturnReason = reason;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = "Повернення оформлено. Залишок відновлено.";
        return RedirectToAction(nameof(Issues));
    }

    [HttpGet]
    [Authorize(Roles = WarehouseRoles.WarehouseWorker)]
    public async Task<IActionResult> AdjustStock(int id)
    {
        var part = await _context.AutoParts.FindAsync(id);
        if (part == null) return NotFound();
        return View(new StockAdjustmentInput { AutoPartId = id, PartName = part.Name, QuantityInStock = part.QuantityInStock });
    }

    [HttpPost]
    [Authorize(Roles = WarehouseRoles.WarehouseWorker)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustStock(StockAdjustmentInput model)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var part = await _context.AutoParts.FindAsync(model.AutoPartId);
        if (part == null) return NotFound();
        model.Reason = (model.Reason ?? string.Empty).Trim();
        if (model.Delta == 0) ModelState.AddModelError(nameof(model.Delta), "Зміна кількості не може бути нульовою.");
        if (model.Reason.Length == 0 || model.Reason.Length > 500) ModelState.AddModelError(nameof(model.Reason), "Вкажіть причину до 500 символів.");
        var after = (long)part.QuantityInStock + model.Delta;
        if (after < 0 || after > int.MaxValue) ModelState.AddModelError(nameof(model.Delta), "Результат має бути від 0 до максимальної кількості.");
        if (!ModelState.IsValid)
        {
            model.PartName = part.Name;
            model.QuantityInStock = part.QuantityInStock;
            return View(model);
        }
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor == null) return Forbid();
        _context.StockAdjustments.Add(new StockAdjustment
        {
            AutoPartId = part.Id,
            Delta = model.Delta,
            QuantityBefore = part.QuantityInStock,
            QuantityAfter = (int)after,
            Reason = model.Reason,
            OccurredAt = DateTime.UtcNow,
            PerformedByUserId = actor
        });
        part.QuantityInStock = (int)after;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = "Інвентаризаційне коригування збережено.";
        return RedirectToAction(nameof(Details), new { id = part.Id });
    }
}
