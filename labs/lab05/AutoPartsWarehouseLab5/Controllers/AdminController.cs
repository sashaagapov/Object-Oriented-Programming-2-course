using AutoPartsWarehouse.Models;
using AutoPartsWarehouse.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoPartsWarehouse.Controllers;

[Authorize(Roles = WarehouseRoles.Administrator)]
public class AdminController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly WarehouseContext _context;
    public AdminController(UserManager<ApplicationUser> users, WarehouseContext context)
    {
        _users = users;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _users.Users.OrderBy(user => user.Email).ToListAsync();
        var rows = new List<AdminUserRow>();
        foreach (var user in users)
            rows.Add(new AdminUserRow { User = user, Role = (await _users.GetRolesAsync(user)).SingleOrDefault() ?? "—" });
        return View(rows);
    }

    [HttpGet]
    public IActionResult Create() => View(new AdminCreateUserViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminCreateUserViewModel model)
    {
        if (!AllowedRole(model.Role)) ModelState.AddModelError(nameof(model.Role), "Виберіть одну з трьох ролей.");
        if (!ModelState.IsValid) return View(model);
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var user = new ApplicationUser { UserName = model.Email.Trim(), Email = model.Email.Trim(), DisplayName = model.DisplayName.Trim(), EmailConfirmed = true };
        var created = await _users.CreateAsync(user, model.Password);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }
        var assigned = await _users.AddToRoleAsync(user, model.Role);
        if (!assigned.Succeeded)
        {
            foreach (var error in assigned.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }
        await transaction.CommitAsync();
        TempData["Success"] = "Обліковий запис створено.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(string id, string role)
    {
        if (!AllowedRole(role)) return BadRequest();
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();
        if (user.Id == _users.GetUserId(User)) return BadRequest("Власну роль змінити не можна.");
        var existing = await _users.GetRolesAsync(user);
        var removed = await _users.RemoveFromRolesAsync(user, existing);
        if (!removed.Succeeded) return BadRequest();
        var added = await _users.AddToRoleAsync(user, role);
        if (!added.Succeeded) return BadRequest();
        await _users.UpdateSecurityStampAsync(user);
        await transaction.CommitAsync();
        TempData["Success"] = "Роль оновлено.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetLocked(string id, bool locked)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();
        if (user.Id == _users.GetUserId(User)) return BadRequest("Власний запис заблокувати не можна.");
        await _users.SetLockoutEnabledAsync(user, true);
        var result = await _users.SetLockoutEndDateAsync(user, locked ? DateTimeOffset.MaxValue : null);
        if (!result.Succeeded) return BadRequest();
        await _users.UpdateSecurityStampAsync(user);
        TempData["Success"] = locked ? "Користувача заблоковано." : "Користувача розблоковано.";
        return RedirectToAction(nameof(Index));
    }

    private static bool AllowedRole(string role) => role is WarehouseRoles.Administrator or WarehouseRoles.Manager or WarehouseRoles.WarehouseWorker;
}
