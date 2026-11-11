using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace AutoPartsWarehouse.Models;

public class WarehouseContext : IdentityDbContext<ApplicationUser>
{
    public WarehouseContext(DbContextOptions<WarehouseContext> options)
        : base(options)
    {
    }

    public DbSet<AutoPart> AutoParts { get; set; }
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
    public DbSet<VehicleModel> VehicleModels { get; set; }
    public DbSet<PartIssue> PartIssues { get; set; }
    public DbSet<StockAdjustment> StockAdjustments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<AutoPart>()
            .HasIndex(part => new { part.Name, part.Manufacturer })
            .IsUnique();

        // Забороняємо каскадне видалення там, де є бізнес-значуща історія.
        // Захист продубльований на рівні схеми БД (не тільки в контролері).
        modelBuilder.Entity<PartIssue>()
            .HasOne(issue => issue.AutoPart)
            .WithMany(part => part.PartIssues)
            .HasForeignKey(issue => issue.AutoPartId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PurchaseOrderItem>()
            .HasOne(item => item.AutoPart)
            .WithMany(part => part.OrderItems)
            .HasForeignKey(item => item.AutoPartId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PurchaseOrder>()
            .HasOne(order => order.Supplier)
            .WithMany(supplier => supplier.PurchaseOrders)
            .HasForeignKey(order => order.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockAdjustment>()
            .HasOne(adjustment => adjustment.AutoPart)
            .WithMany(part => part.StockAdjustments)
            .HasForeignKey(adjustment => adjustment.AutoPartId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<StockAdjustment>()
            .HasOne(adjustment => adjustment.PerformedByUser)
            .WithMany()
            .HasForeignKey(adjustment => adjustment.PerformedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
