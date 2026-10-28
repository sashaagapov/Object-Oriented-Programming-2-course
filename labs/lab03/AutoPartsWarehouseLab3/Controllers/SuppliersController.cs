using AutoPartsWarehouse.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoPartsWarehouse.Controllers;

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
}
