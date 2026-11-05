using AutoPartsWarehouse.Models;
using AutoPartsWarehouse.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AutoPartsWarehouse.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signIn;
    public AccountController(SignInManager<ApplicationUser> signIn) => _signIn = signIn;

    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginViewModel());
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return View(model);
        var result = await _signIn.PasswordSignInAsync(model.Email.Trim(), model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            if (Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl!);
            var user = await _signIn.UserManager.FindByEmailAsync(model.Email.Trim());
            return user != null && await _signIn.UserManager.IsInRoleAsync(user, WarehouseRoles.Administrator)
                ? RedirectToAction("Index", "Admin") : RedirectToAction("Index", "AutoParts");
        }
        ModelState.AddModelError(string.Empty, result.IsLockedOut ? "Обліковий запис заблоковано." : "Неправильна пошта або пароль.");
        return View(model);
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    public IActionResult AccessDenied() => View();
}
