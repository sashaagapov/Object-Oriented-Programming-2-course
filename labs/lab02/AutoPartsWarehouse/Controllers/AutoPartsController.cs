using AutoPartsWarehouse.Models;
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

    public async Task<IActionResult> Index()
    {
        var autoParts = await _context.AutoParts.ToListAsync();

        return View(autoParts);
    }
    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AutoPart autoPart)
    {
        if (!ModelState.IsValid)
        {
            return View(autoPart);
        }

        _context.AutoParts.Add(autoPart);
        await _context.SaveChangesAsync();

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
    public async Task<IActionResult> Edit(int id, AutoPart autoPart)
    {
        if (id != autoPart.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(autoPart);
        }

        _context.AutoParts.Update(autoPart);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
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
    public async Task<IActionResult> Details(int id)
    {
        var autoPart = await _context.AutoParts
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
            QuantityInStock = autoPart.QuantityInStock
        };

        return View(viewModel);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(IssuePartViewModel viewModel)
    {
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

        return RedirectToAction(nameof(Index));
    }
}
