namespace AutoPartsWarehouse.Models;

public class PartIssue
{
    public int Id { get; set; }

    public int AutoPartId { get; set; }
    public AutoPart AutoPart { get; set; } = null!;

    public int Quantity { get; set; }

    public DateTime IssuedAt { get; set; } = DateTime.Now;
}
