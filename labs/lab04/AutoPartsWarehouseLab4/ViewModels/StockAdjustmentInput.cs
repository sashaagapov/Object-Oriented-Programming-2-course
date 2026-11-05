using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AutoPartsWarehouse.ViewModels;

public class StockAdjustmentInput
{
    public int AutoPartId { get; set; }
    [ValidateNever]
    public string PartName { get; set; } = string.Empty;
    public int QuantityInStock { get; set; }
    public int Delta { get; set; }
    [Required(ErrorMessage = "Вкажіть причину коригування.")]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}
