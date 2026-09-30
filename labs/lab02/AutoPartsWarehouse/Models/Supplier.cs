namespace AutoPartsWarehouse.Models;

public class Supplier
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public ICollection<PurchaseOrder> PurchaseOrders { get; set; }
    = new List<PurchaseOrder>();
}
