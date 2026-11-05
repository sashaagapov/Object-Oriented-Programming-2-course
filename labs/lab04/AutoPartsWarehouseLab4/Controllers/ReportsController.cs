using AutoPartsWarehouse.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoPartsWarehouse.ViewModels;
using Microsoft.AspNetCore.Authorization;
namespace AutoPartsWarehouse.Controllers;

[Authorize(Roles = WarehouseRoles.Manager)]
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
                CriticalPartCount = group.Count(part => part.QuantityInStock > 0 && part.QuantityInStock <= part.MinimumStock)
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
        var fromUtc = from?.Date.ToUniversalTime();
        var toUtc = to?.Date.AddDays(1).ToUniversalTime();
        var all = await _context.PartIssues.Include(i => i.AutoPart).ToListAsync();
        bool InPeriod(DateTime? date) => date.HasValue && (!fromUtc.HasValue || date.Value >= fromUtc.Value) && (!toUtc.HasValue || date.Value < toUtc.Value);
        var summary = all.GroupBy(i => new { i.AutoPartId, i.AutoPart.Name, i.AutoPart.Manufacturer })
            .Select(g => new IssuedPartSummary
            {
                PartName = g.Key.Name, Manufacturer = g.Key.Manufacturer,
                TotalIssued = g.Where(i => (i.Status == PartIssueStatus.Issued || i.Status == PartIssueStatus.Returned || i.Status == PartIssueStatus.LegacyIssued) && InPeriod(i.IssuedAt)).Sum(i => i.Quantity),
                TotalReturned = g.Where(i => i.Status == PartIssueStatus.Returned && InPeriod(i.ReturnedAt)).Sum(i => i.Quantity)
            }).Where(s => s.TotalIssued != 0 || s.TotalReturned != 0).OrderBy(s => s.PartName).ToList();
        var history = all.Where(i => InPeriod(i.CreatedAt)).OrderByDescending(i => i.CreatedAt).ToList();
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        return View(new IssuedByPeriodViewModel { Issues = history, Summary = summary });
    }
}
