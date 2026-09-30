namespace AutoPartsWarehouse.Models;

public class AutoPart
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Manufacturer { get; set; } = string.Empty;

    public int QuantityInStock { get; set; }

    public int MinimumStock { get; set; }
    public ICollection<PurchaseOrderItem> OrderItems { get; set; }
    = new List<PurchaseOrderItem>();
    public ICollection<VehicleModel> VehicleModels { get; set; }
    = new List<VehicleModel>();
    public ICollection<PartIssue> PartIssues { get; set; }
    = new List<PartIssue>();
}
