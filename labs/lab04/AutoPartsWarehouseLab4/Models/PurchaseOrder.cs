namespace AutoPartsWarehouse.Models;

public enum PurchaseOrderStatus
{
    Created,
    Ordered,
    Received,
    Cancelled
}

public class PurchaseOrder
{
    public int Id { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.Now;

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Created;
    public DateTime? CancelledAt { get; set; }
    public string? CancelledByUserId { get; set; }
    public string? CancellationReason { get; set; }

    public int SupplierId { get; set; }

    public Supplier Supplier { get; set; } = null!;

    public ICollection<PurchaseOrderItem> Items { get; set; }
        = new List<PurchaseOrderItem>();
}
