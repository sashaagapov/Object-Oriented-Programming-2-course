using System.ComponentModel.DataAnnotations;

namespace AutoPartsWarehouse.Models;

public class AutoPart
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Вкажіть назву запчастини.")]
    [StringLength(100, ErrorMessage = "Назва не може перевищувати 100 символів.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Вкажіть виробника.")]
    [StringLength(100, ErrorMessage = "Назва виробника не може перевищувати 100 символів.")]
    public string Manufacturer { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Кількість не може бути від’ємною.")]
    public int QuantityInStock { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Мінімальний залишок не може бути від’ємним.")]
    public int MinimumStock { get; set; }
    public ICollection<PurchaseOrderItem> OrderItems { get; set; }
    = new List<PurchaseOrderItem>();
    public ICollection<VehicleModel> VehicleModels { get; set; }
    = new List<VehicleModel>();
    public ICollection<PartIssue> PartIssues { get; set; }
    = new List<PartIssue>();
}
