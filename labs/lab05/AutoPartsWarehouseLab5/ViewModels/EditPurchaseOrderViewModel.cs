using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AutoPartsWarehouse.ViewModels;

public class EditPurchaseOrderViewModel
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Виберіть постачальника.")]
    public int SupplierId { get; set; }

    public List<PurchaseOrderItemInput> Items { get; set; } = new();

    [ValidateNever]
    public IEnumerable<SelectListItem> Suppliers { get; set; } = new List<SelectListItem>();

    [ValidateNever]
    public IEnumerable<SelectListItem> AutoParts { get; set; } = new List<SelectListItem>();
}
