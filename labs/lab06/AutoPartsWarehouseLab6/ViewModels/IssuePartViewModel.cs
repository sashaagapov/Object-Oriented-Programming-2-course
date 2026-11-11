using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace AutoPartsWarehouse.ViewModels;

public class IssuePartViewModel
{
    public int AutoPartId { get; set; }

    [ValidateNever]
    public string PartName { get; set; } = string.Empty;

    public int QuantityInStock { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Кількість має бути більшою за нуль.")]
    public int Quantity { get; set; }
}
