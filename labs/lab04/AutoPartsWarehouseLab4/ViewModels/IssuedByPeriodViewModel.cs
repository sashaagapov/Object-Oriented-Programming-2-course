using AutoPartsWarehouse.Models;

namespace AutoPartsWarehouse.ViewModels;

public class IssuedByPeriodViewModel
{
    public List<PartIssue> Issues { get; set; } = new();
    public List<IssuedPartSummary> Summary { get; set; } = new();
}

public class IssuedPartSummary
{
    public string PartName { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public int TotalIssued { get; set; }
    public int TotalReturned { get; set; }
    public int Net => TotalIssued - TotalReturned;
}
