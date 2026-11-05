namespace AutoPartsWarehouse.Models;

public enum PartIssueStatus
{
    Pending,
    Issued,
    Cancelled,
    Returned,
    LegacyIssued,
    LegacyReversed
}

public class PartIssue
{
    public int Id { get; set; }

    public int AutoPartId { get; set; }
    public AutoPart AutoPart { get; set; } = null!;

    public int Quantity { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? IssuedAt { get; set; }

    public PartIssueStatus Status { get; set; } = PartIssueStatus.Pending;

    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? ReturnReason { get; set; }
    public bool IsPaymentConfirmed { get; set; }
    public DateTime? PaymentConfirmedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? PaymentConfirmedByUserId { get; set; }
    public string? IssuedByUserId { get; set; }
    public string? CancelledByUserId { get; set; }
    public string? ReturnedByUserId { get; set; }
    public string StatusLabel => Status switch
    {
        PartIssueStatus.Pending => "Очікує", PartIssueStatus.Issued => "Видано",
        PartIssueStatus.Cancelled => "Анульовано", PartIssueStatus.Returned => "Повернуто",
        PartIssueStatus.LegacyIssued => "Історична видача", PartIssueStatus.LegacyReversed => "Історичне скасування",
        _ => "Невідомо"
    };
}
