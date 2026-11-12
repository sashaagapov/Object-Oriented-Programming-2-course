using AutoPartsWarehouse.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoPartsWarehouse.ViewComponents;

/// <summary>
/// Невелике повідомлення менеджеру про критичні залишки (UML ЛР1: «Повідомити менеджера
/// про критичний залишок»). Запитує лічильник заново на кожному показі сторінки, тому дані
/// завжди актуальні після складських операцій — окремого сповіщення чи фонової служби не
/// потрібно.
/// </summary>
public class CriticalStockBannerViewComponent : ViewComponent
{
    private readonly WarehouseContext _context;

    public CriticalStockBannerViewComponent(WarehouseContext context)
    {
        _context = context;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var criticalCount = await _context.AutoParts
            .CountAsync(part => part.QuantityInStock <= part.MinimumStock);
        ViewData["OpenNotices"] = await _context.CriticalStockNotices
            .CountAsync(notice => notice.AcknowledgedAt == null);
        return View(criticalCount);
    }
}
