using AutoPartsWarehouse.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoPartsWarehouse.Controllers;

public class ReportsController : Controller
{
    private readonly WarehouseContext _context;

    public ReportsController(WarehouseContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? manufacturer)
    {
        var parts = _context.AutoParts.AsQueryable();

        if (!string.IsNullOrWhiteSpace(manufacturer))
        {
            manufacturer = manufacturer.Trim();

            parts = parts.Where(part =>
                part.Manufacturer.Contains(manufacturer));
        }

        ViewBag.Manufacturer = manufacturer;

        return View(
            await parts
                .OrderBy(part => part.Name)
                .ToListAsync()
        );
    }
}
