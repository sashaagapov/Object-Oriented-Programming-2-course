using AutoPartsWarehouse.Controllers;
using AutoPartsWarehouse.Models;
using AutoPartsWarehouse.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoPartsWarehouseLab5.Tests;

// AdminController потребує реальних UserManager/RoleManager (хешування паролів,
// валідатори Identity), тому замість TestDb.Open беремо їх із DI-контейнера
// WarehouseAppFactory - без підняття HTTP-конвеєра (це робить RoleAuthorizationHttpTests).
[Trait("Category", "CRUD")]
[Trait("Category", "Roles")]
public class AdminRoleManagementTests : IAsyncLifetime
{
    private WarehouseAppFactory _factory = null!;
    private IServiceScope _scope = null!;
    private UserManager<ApplicationUser> _users = null!;
    private WarehouseContext _context = null!;
    private string _adminActorId = null!;

    public async Task InitializeAsync()
    {
        _factory = new WarehouseAppFactory();
        await _factory.SeedAdministratorAsync();
        _scope = _factory.Services.CreateScope();
        _users = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        _context = _scope.ServiceProvider.GetRequiredService<WarehouseContext>();
        _adminActorId = (await _users.FindByEmailAsync(WarehouseAppFactory.AdminEmail))!.Id;
    }

    public Task DisposeAsync()
    {
        _scope.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private AdminController NewController()
    {
        var controller = new AdminController(_users, _context);
        TestDb.ActAs(controller, _adminActorId);
        return controller;
    }

    [Fact]
    public async Task Create_AddsUserWithRole_AndChangeRole_SwitchesItToAnother()
    {
        var email = $"new-{Guid.NewGuid():N}@lab4.local";
        var controller = NewController();

        var createResult = await controller.Create(new AdminCreateUserViewModel
        {
            DisplayName = "New Worker", Email = email, Password = WarehouseAppFactory.TestPassword, Role = WarehouseRoles.WarehouseWorker
        });
        Assert.IsType<RedirectToActionResult>(createResult);

        var created = await _users.FindByEmailAsync(email);
        Assert.NotNull(created);
        Assert.Equal(WarehouseRoles.WarehouseWorker, (await _users.GetRolesAsync(created!)).Single());

        var changeResult = await controller.ChangeRole(created!.Id, WarehouseRoles.Manager);
        Assert.IsType<RedirectToActionResult>(changeResult);
        Assert.Equal(WarehouseRoles.Manager, (await _users.GetRolesAsync(created)).Single());
    }

    [Fact]
    public async Task ChangeRole_OnOwnAccount_IsRejected_RoleStaysAdministrator()
    {
        var controller = NewController();

        var result = await controller.ChangeRole(_adminActorId, WarehouseRoles.Manager);

        Assert.IsType<BadRequestObjectResult>(result);
        var admin = await _users.FindByIdAsync(_adminActorId);
        Assert.Equal(WarehouseRoles.Administrator, (await _users.GetRolesAsync(admin!)).Single());
    }

    [Fact]
    public async Task SetLocked_OnOwnAccount_IsRejected_AccountStaysUsable()
    {
        var controller = NewController();

        var result = await controller.SetLocked(_adminActorId, true);

        Assert.IsType<BadRequestObjectResult>(result);
        var admin = await _users.FindByIdAsync(_adminActorId);
        Assert.False(await _users.IsLockedOutAsync(admin!));
    }

    [Fact]
    public async Task SetLocked_OnAnotherUser_PreventsThatUserFromSigningInAfterward()
    {
        var email = $"lockable-{Guid.NewGuid():N}@lab4.local";
        var controller = NewController();
        await controller.Create(new AdminCreateUserViewModel
        {
            DisplayName = "Lockable Worker", Email = email, Password = WarehouseAppFactory.TestPassword, Role = WarehouseRoles.WarehouseWorker
        });
        var target = (await _users.FindByEmailAsync(email))!;

        var result = await controller.SetLocked(target.Id, true);
        Assert.IsType<RedirectToActionResult>(result);

        var signIn = _scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
        var signInResult = await signIn.CheckPasswordSignInAsync(target, WarehouseAppFactory.TestPassword, lockoutOnFailure: false);
        Assert.True(signInResult.IsLockedOut);
    }
}
