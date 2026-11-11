using AutoPartsWarehouse.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace AutoPartsWarehouse.Controllers;

[Authorize(Roles = WarehouseRoles.Manager)]
public class SuppliersController : Controller
{
    private readonly WarehouseContext _context;

    public SuppliersController(WarehouseContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _context.Suppliers.OrderBy(supplier => supplier.Name).ToListAsync());
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Phone")] Supplier supplier)
    {
        if (!ModelState.IsValid)
        {
            return View(supplier);
        }

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var supplier = await _context.Suppliers.FindAsync(id);
        return supplier == null ? NotFound() : View(supplier);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Phone")] Supplier supplier)
    {
        if (id != supplier.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(supplier);
        }

        var existingSupplier = await _context.Suppliers.FindAsync(id);
        if (existingSupplier == null)
        {
            return NotFound();
        }

        existingSupplier.Name = supplier.Name.Trim();
        existingSupplier.Phone = supplier.Phone.Trim();
        await _context.SaveChangesAsync();
        TempData["Success"] = "Дані постачальника оновлено.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var supplier = await _context.Suppliers.FindAsync(id);
        return supplier == null ? NotFound() : View(supplier);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var supplier = await _context.Suppliers.FindAsync(id);
        if (supplier == null)
        {
            return NotFound();
        }

        if (await _context.PurchaseOrders.AnyAsync(order => order.SupplierId == id))
        {
            ModelState.AddModelError(string.Empty,
                "Постачальника неможливо видалити, оскільки з ним пов'язані замовлення.");
            return View("Delete", supplier);
        }

        _context.Suppliers.Remove(supplier);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Постачальника видалено.";
        return RedirectToAction(nameof(Index));
    }
}
