using Microsoft.AspNetCore.Identity;

namespace AutoPartsWarehouse.Models;

public static class IdentitySeed
{
    public static async Task InitializeAsync(IServiceProvider services, IWebHostEnvironment environment)
    {
        using var scope = services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { WarehouseRoles.Administrator, WarehouseRoles.Manager, WarehouseRoles.WarehouseWorker })
        {
            if (!await roles.RoleExistsAsync(role))
            {
                var result = await roles.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }

        if (!environment.IsDevelopment()) return;

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var (role, email, variable, displayName) in new[]
        {
            (WarehouseRoles.Administrator, "admin@lab4.local", "LAB4_ADMIN_PASSWORD", "Адміністратор"),
            (WarehouseRoles.Manager, "manager@lab4.local", "LAB4_MANAGER_PASSWORD", "Менеджер"),
            (WarehouseRoles.WarehouseWorker, "worker@lab4.local", "LAB4_WORKER_PASSWORD", "Комірник")
        })
        {
            var password = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrEmpty(password)) continue;
            var user = await users.FindByEmailAsync(email);
            if (user != null) continue;
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = displayName };
            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded) throw new InvalidOperationException($"{variable}: " + string.Join("; ", created.Errors.Select(e => e.Description)));
            var assigned = await users.AddToRoleAsync(user, role);
            if (!assigned.Succeeded) throw new InvalidOperationException(string.Join("; ", assigned.Errors.Select(e => e.Description)));
        }
    }
}
