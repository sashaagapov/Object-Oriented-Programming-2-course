using AutoPartsWarehouse.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AutoPartsWarehouseLab5.Tests;

public sealed class WarehouseAppFactory : WebApplicationFactory<Program>
{
    public readonly string DbPath = Path.Combine(Path.GetTempPath(), $"AutoPartsWarehouseLab5.Http.{Guid.NewGuid():N}.db");
    public const string WorkerEmail = "worker-lr5@lab4.local";
    public const string ManagerEmail = "manager-lr5@lab4.local";
    public const string AdminEmail = "admin-lr5@lab4.local";
    public const string TestPassword = "Test-Passw0rd!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<WarehouseContext>));
            services.Remove(descriptor);
            services.AddDbContext<WarehouseContext>(options => options.UseSqlite($"Data Source={DbPath}"));
        });
    }

    public Task SeedWorkerAsync() => SeedUserAsync(WorkerEmail, WarehouseRoles.WarehouseWorker, "QA Worker");
    public Task SeedManagerAsync() => SeedUserAsync(ManagerEmail, WarehouseRoles.Manager, "QA Manager");
    public Task SeedAdministratorAsync() => SeedUserAsync(AdminEmail, WarehouseRoles.Administrator, "QA Administrator");

    private async Task SeedUserAsync(string email, string role, string displayName)
    {
        using var scope = Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync(role))
            await roles.CreateAsync(new IdentityRole(role));

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(email) != null) return;
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = displayName };
        var created = await users.CreateAsync(user, TestPassword);
        if (!created.Succeeded) throw new InvalidOperationException(string.Join("; ", created.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(user, role);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        foreach (var suffix in new[] { "", "-shm", "-wal", "-journal" })
        {
            var file = DbPath + suffix;
            if (File.Exists(file)) File.Delete(file);
        }
    }
}
