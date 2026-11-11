using Microsoft.AspNetCore.Identity;

namespace AutoPartsWarehouse.Models;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}

public static class WarehouseRoles
{
    public const string Administrator = "Administrator";
    public const string Manager = "Manager";
    public const string WarehouseWorker = "WarehouseWorker";
    public const string Staff = Manager + "," + WarehouseWorker;
}
