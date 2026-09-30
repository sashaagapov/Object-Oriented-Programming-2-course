namespace AutoPartsWarehouse.Models;

public enum PurchaseOrderStatus
{
    Created,
    Ordered,
    Received
}

public class PurchaseOrder
{
    public int Id { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.Now;

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Created;

    public int SupplierId { get; set; }

    public Supplier Supplier { get; set; } = null!;
}
