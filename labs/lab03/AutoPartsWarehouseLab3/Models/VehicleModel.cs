namespace AutoPartsWarehouse.Models;

public class VehicleModel
{
    public int Id { get; set; }

    public string Brand { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    public int ProductionYear { get; set; }
    public ICollection<AutoPart> AutoParts { get; set; }
    = new List<AutoPart>();
}
