using System.ComponentModel.DataAnnotations;

namespace AutoPartsWarehouse.Models;

public class Supplier
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Вкажіть назву постачальника.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Вкажіть телефон.")]
    [StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    public ICollection<PurchaseOrder> PurchaseOrders { get; set; }
    = new List<PurchaseOrder>();
}
