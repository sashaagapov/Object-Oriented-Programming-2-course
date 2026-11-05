using System.ComponentModel.DataAnnotations;

namespace AutoPartsWarehouse.ViewModels;

public class AdminCreateUserViewModel
{
    [Required, StringLength(100)]
    public string DisplayName { get; set; } = string.Empty;
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    [Required]
    public string Role { get; set; } = string.Empty;
}
