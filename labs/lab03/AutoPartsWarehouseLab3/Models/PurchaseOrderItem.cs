using System.ComponentModel.DataAnnotations;

namespace AutoPartsWarehouse.Models;

public class PurchaseOrderItem
{
    public int Id { get; set; }

    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public int AutoPartId { get; set; }
    public AutoPart AutoPart { get; set; } = null!;

    [Range(1, int.MaxValue, ErrorMessage = "Кількість повинна бути більшою за 0.")]
    public int Quantity { get; set; }
}
