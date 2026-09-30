namespace AutoPartsWarehouse.ViewModels;

public class IssuePartViewModel
{
    public int AutoPartId { get; set; }

    public string PartName { get; set; } = string.Empty;

    public int QuantityInStock { get; set; }

    public int Quantity { get; set; }

    public bool IsPaymentConfirmed { get; set; }
}
