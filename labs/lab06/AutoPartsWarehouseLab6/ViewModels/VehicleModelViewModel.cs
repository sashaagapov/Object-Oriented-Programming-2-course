using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AutoPartsWarehouse.ViewModels;

public class VehicleModelViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Вкажіть марку автомобіля.")]
    [StringLength(50)]
    public string Brand { get; set; } = string.Empty;

    [Required(ErrorMessage = "Вкажіть модель автомобіля.")]
    [StringLength(50)]
    public string ModelName { get; set; } = string.Empty;

    [Range(1886, 2100, ErrorMessage = "Вкажіть коректний рік випуску.")]
    public int ProductionYear { get; set; }

    public List<int> SelectedAutoPartIds { get; set; } = new();

    [ValidateNever]
    public IEnumerable<SelectListItem> AutoParts { get; set; } = new List<SelectListItem>();
}
