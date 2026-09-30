using Microsoft.EntityFrameworkCore;

namespace AutoPartsWarehouse.Models;

public class WarehouseContext : DbContext
{
    public WarehouseContext(DbContextOptions<WarehouseContext> options)
        : base(options)
    {
    }

    public DbSet<AutoPart> AutoParts { get; set; }
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public ICollection<PurchaseOrderItem> Items { get; set; }
    = new List<PurchaseOrderItem>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
    public DbSet<VehicleModel> VehicleModels { get; set; }
    public DbSet<PartIssue> PartIssues { get; set; }
}
