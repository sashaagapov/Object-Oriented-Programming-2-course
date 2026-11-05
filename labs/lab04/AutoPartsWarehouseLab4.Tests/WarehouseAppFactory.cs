using AutoPartsWarehouse.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AutoPartsWarehouseLab4.Tests;

/// <summary>
/// Boots the real ASP.NET Core pipeline (real authorization, real Identity, real antiforgery)
/// against an isolated, file-based SQLite database so HTTP-level role/security checks run
/// against production code paths instead of re-implemented stand-ins.
/// </summary>
public sealed class WarehouseAppFactory : WebApplicationFactory<Program>
{
    public readonly string DbPath = Path.Combine(Path.GetTempPath(), $"AutoPartsWarehouseLab4.Http.{Guid.NewGuid():N}.db");

    public const string AdminEmail = "admin-test@lab4.local";
    public const string ManagerEmail = "manager-test@lab4.local";
    public const string WorkerEmail = "worker-test@lab4.local";
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

    public async Task SeedUsersAsync()
    {
        using var scope = Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { WarehouseRoles.Administrator, WarehouseRoles.Manager, WarehouseRoles.WarehouseWorker })
        {
            if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole(role));
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        async Task EnsureUser(string email, string role)
        {
            if (await users.FindByEmailAsync(email) != null) return;
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = email };
            var created = await users.CreateAsync(user, TestPassword);
            if (!created.Succeeded) throw new InvalidOperationException(string.Join("; ", created.Errors.Select(e => e.Description)));
            await users.AddToRoleAsync(user, role);
        }

        await EnsureUser(AdminEmail, WarehouseRoles.Administrator);
        await EnsureUser(ManagerEmail, WarehouseRoles.Manager);
        await EnsureUser(WorkerEmail, WarehouseRoles.WarehouseWorker);
    }

    public void DeleteDatabaseFiles()
    {
        foreach (var suffix in new[] { "", "-shm", "-wal", "-journal" })
        {
            var file = DbPath + suffix;
            if (File.Exists(file)) File.Delete(file);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        DeleteDatabaseFiles();
    }
}
