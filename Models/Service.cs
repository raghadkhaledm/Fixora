using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fixora.Models.Concerns;

namespace Fixora.Models;

[Table("Service")]
public class Service : FixoraTable
{
    [Key]
    public int Service_ID { get; set; }

    public int Service_Type_ID { get; set; }

    [Required]
    [MaxLength(80)]
    public string Service_Name { get; set; } = "";

    [Required]
    [MaxLength(15)]
    [Column(TypeName = "varchar(15)")]
    public string Service_Category { get; set; } = "";

    [Column(TypeName = "decimal(8,2)")]
    public double Price { get; set; }

    public bool Flexible_Rate { get; set; }

    [Column(TypeName = "decimal(6,2)")]
    public double Service_Commission_Fee { get; set; } = 15;

    [Required]
    [MaxLength(250)]
    public string Description { get; set; } = "";

    [Required]
    [MaxLength(10)]
    [Column(TypeName = "varchar(10)")]
    public string Unit { get; set; } = "job";

    public bool Is_Active { get; set; } = true;

    [NotMapped]
    public int Technicians_Count { get; set; }

    public virtual ServiceType? Type { get; set; }

    public virtual ICollection<Technician> Technicians { get; set; } = new List<Technician>();

    public virtual ICollection<BookingService> BookingServices { get; set; } = new List<BookingService>();

    public string UnitSuffix()
    {
        return Unit == "job" ? "" : " / " + Unit;
    }

    [NotMapped]
    public bool Exists => Service_ID != 0;

    public int MaxQty()
    {
        return Flexible_Rate ? 8 : 10;
    }

    public string QtyLabel(int q)
    {
        return Unit switch
        {
            "hr" => q + " " + (q == 1 ? "hour" : "hours"),
            "cylinder" => q + " " + (q == 1 ? "cylinder" : "cylinders"),
            "room" => q + " " + (q == 1 ? "room" : "rooms"),
            _ => q + " " + (q == 1 ? "job" : "jobs"),
        };
    }
}
