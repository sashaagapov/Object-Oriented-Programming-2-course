using AutoPartsWarehouse.Models;

namespace AutoPartsWarehouse.ViewModels;

public class IssuedByPeriodViewModel
{
    public List<PartIssue> Issues { get; set; } = new();
    public List<IssuedPartSummary> Summary { get; set; } = new();
}

public class IssuedPartSummary
{
    public string PartName { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public int TotalIssued { get; set; }
}