using AutoPartsWarehouse.Models;

namespace AutoPartsWarehouse.ViewModels;

public class AdminUserRow
{
    public ApplicationUser User { get; set; } = null!;
    public string Role { get; set; } = string.Empty;
}
