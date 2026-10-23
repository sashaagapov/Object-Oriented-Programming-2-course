using AutoPartsWarehouse.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoPartsWarehouse.ViewModels;
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
            parts = parts.Where(part =>
                part.Manufacturer.Contains(manufacturer));
        }

        var result = await parts
            .OrderBy(part => part.Name)
            .ToListAsync();
        ViewBag.Manufacturer = manufacturer;
        return View(result);
    }
    public async Task<IActionResult> CriticalStock()
    {
        var parts = await _context.AutoParts
            .Where(part =>
                part.QuantityInStock > 0 &&
                part.QuantityInStock <= part.MinimumStock)
            .OrderBy(part => part.QuantityInStock)
            .ToListAsync();

        return View(parts);
    }
    public async Task<IActionResult> ManufacturerStatistics()
    {
        var statistics = await _context.AutoParts
            .GroupBy(part => part.Manufacturer)
            .Select(group => new ManufacturerStockViewModel
            {
                Manufacturer = group.Key,
                PartCount = group.Count(),
                TotalQuantity = group.Sum(part => part.QuantityInStock)
            })
            .OrderBy(item => item.Manufacturer)
            .ToListAsync();

        return View(statistics);
    }
    public async Task<IActionResult> SupplierOrders()
    {
        var orders = await _context.PurchaseOrders
            .Include(order => order.Supplier)
            .Include(order => order.Items)
                .ThenInclude(item => item.AutoPart)
            .OrderByDescending(order => order.OrderDate)
            .ToListAsync();

        return View(orders);
    }
    public async Task<IActionResult> IssuedByPeriod(DateTime? from, DateTime? to)
    {
        var issues = _context.PartIssues
            .Include(issue => issue.AutoPart)
            .AsQueryable();

        if (from.HasValue)
        {
            issues = issues.Where(issue => issue.IssuedAt >= from.Value);
        }

        if (to.HasValue)
        {
            issues = issues.Where(issue =>
                issue.IssuedAt < to.Value.Date.AddDays(1));
        }

        var totalQuantity = await issues
            .SumAsync(issue => (int?)issue.Quantity) ?? 0;

        var result = await issues
            .OrderByDescending(issue => issue.IssuedAt)
            .ToListAsync();

        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.TotalQuantity = totalQuantity;

        return View(result);
    }
}