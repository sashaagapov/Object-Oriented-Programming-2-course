using AutoPartsWarehouse.Models;
using AutoPartsWarehouse.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace AutoPartsWarehouse.Controllers;

[Authorize(Roles = WarehouseRoles.Staff)]
public class VehicleModelsController : Controller
{
    private readonly WarehouseContext _context;

    public VehicleModelsController(WarehouseContext context) => _context = context;

    public async Task<IActionResult> Index()
    {
        var models = await _context.VehicleModels
            .Include(vehicle => vehicle.AutoParts)
            .OrderBy(vehicle => vehicle.Brand)
            .ThenBy(vehicle => vehicle.ModelName)
            .ThenBy(vehicle => vehicle.ProductionYear)
            .ToListAsync();
        return View(models);
    }

    [HttpGet]
    [Authorize(Roles = WarehouseRoles.Manager)]
    public async Task<IActionResult> Create()
    {
        var model = new VehicleModelViewModel();
        await FillAutoParts(model);
        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = WarehouseRoles.Manager)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VehicleModelViewModel model)
    {
        Normalize(model);
        await ValidateUniqueModel(model);
        if (!ModelState.IsValid)
        {
            await FillAutoParts(model);
            return View(model);
        }

        var autoParts = await _context.AutoParts
            .Where(part => model.SelectedAutoPartIds.Distinct().Contains(part.Id))
            .ToListAsync();
        var vehicleModel = new VehicleModel
        {
            Brand = model.Brand,
            ModelName = model.ModelName,
            ProductionYear = model.ProductionYear,
            AutoParts = autoParts
        };
        _context.VehicleModels.Add(vehicleModel);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Модель автомобіля додано.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = WarehouseRoles.Manager)]
    public async Task<IActionResult> Edit(int id)
    {
        var vehicleModel = await _context.VehicleModels
            .Include(vehicle => vehicle.AutoParts)
            .FirstOrDefaultAsync(vehicle => vehicle.Id == id);
        if (vehicleModel == null)
        {
            return NotFound();
        }

        var model = new VehicleModelViewModel
        {
            Id = vehicleModel.Id,
            Brand = vehicleModel.Brand,
            ModelName = vehicleModel.ModelName,
            ProductionYear = vehicleModel.ProductionYear,
            SelectedAutoPartIds = vehicleModel.AutoParts.Select(part => part.Id).ToList()
        };
        await FillAutoParts(model);
        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = WarehouseRoles.Manager)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, VehicleModelViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        Normalize(model);
        await ValidateUniqueModel(model, id);
        if (!ModelState.IsValid)
        {
            await FillAutoParts(model);
            return View(model);
        }

        var vehicleModel = await _context.VehicleModels
            .Include(vehicle => vehicle.AutoParts)
            .FirstOrDefaultAsync(vehicle => vehicle.Id == id);
        if (vehicleModel == null)
        {
            return NotFound();
        }

        var autoParts = await _context.AutoParts
            .Where(part => model.SelectedAutoPartIds.Distinct().Contains(part.Id))
            .ToListAsync();
        vehicleModel.Brand = model.Brand;
        vehicleModel.ModelName = model.ModelName;
        vehicleModel.ProductionYear = model.ProductionYear;
        vehicleModel.AutoParts.Clear();
        foreach (var autoPart in autoParts)
        {
            vehicleModel.AutoParts.Add(autoPart);
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Модель автомобіля оновлено.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = WarehouseRoles.Manager)]
    public async Task<IActionResult> Delete(int id)
    {
        var vehicleModel = await _context.VehicleModels
            .Include(vehicle => vehicle.AutoParts)
            .FirstOrDefaultAsync(vehicle => vehicle.Id == id);
        return vehicleModel == null ? NotFound() : View(vehicleModel);
    }

    [HttpPost, ActionName("Delete")]
    [Authorize(Roles = WarehouseRoles.Manager)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var vehicleModel = await _context.VehicleModels.FindAsync(id);
        if (vehicleModel == null)
        {
            return NotFound();
        }

        _context.VehicleModels.Remove(vehicleModel);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Модель автомобіля видалено. Запчастини не змінено.";
        return RedirectToAction(nameof(Index));
    }

    private async Task FillAutoParts(VehicleModelViewModel model)
    {
        model.AutoParts = new SelectList(
            await _context.AutoParts.OrderBy(part => part.Name).ToListAsync(),
            "Id", "Name", model.SelectedAutoPartIds);
    }

    private async Task ValidateUniqueModel(VehicleModelViewModel model, int? excludedId = null)
    {
        if (await _context.VehicleModels.AnyAsync(vehicle =>
                vehicle.Id != excludedId && vehicle.Brand == model.Brand &&
                vehicle.ModelName == model.ModelName && vehicle.ProductionYear == model.ProductionYear))
        {
            ModelState.AddModelError(string.Empty, "Така модель автомобіля з указаним роком уже існує.");
        }
    }

    private static void Normalize(VehicleModelViewModel model)
    {
        model.Brand = (model.Brand ?? string.Empty).Trim();
        model.ModelName = (model.ModelName ?? string.Empty).Trim();
        model.SelectedAutoPartIds ??= new List<int>();
    }
}
