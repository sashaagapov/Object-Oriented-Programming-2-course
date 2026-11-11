using System.ComponentModel.DataAnnotations;

namespace AutoPartsWarehouse.Models;

public class StockAdjustment
{
    public int Id { get; set; }
    public int AutoPartId { get; set; }
    public AutoPart AutoPart { get; set; } = null!;
    public int Delta { get; set; }
    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }
    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    [Required]
    public string PerformedByUserId { get; set; } = string.Empty;
    public ApplicationUser PerformedByUser { get; set; } = null!;
}
