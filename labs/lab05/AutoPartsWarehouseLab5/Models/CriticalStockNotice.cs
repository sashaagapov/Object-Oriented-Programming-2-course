namespace AutoPartsWarehouse.Models;

public class CriticalStockNotice
{
    public int Id { get; set; }
    public int AutoPartId { get; set; }
    public AutoPart AutoPart { get; set; } = null!;
    public int QuantityAtNotice { get; set; }
    public DateTime CreatedAt { get; set; }
    public string NotifiedByUserId { get; set; } = string.Empty;
    public ApplicationUser NotifiedByUser { get; set; } = null!;
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedByUserId { get; set; }
}
