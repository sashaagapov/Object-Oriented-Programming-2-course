namespace AutoPartsWarehouse.ViewModels;

public class ManufacturerStockViewModel
{
    public string Manufacturer { get; set; } = string.Empty;

    public int PartCount { get; set; }

    public int TotalQuantity { get; set; }
}