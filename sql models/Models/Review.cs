using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fixora.Models.Concerns;

namespace Fixora.Models;

[Table("Review")]
public class Review : FixoraTable
{
    [Key]
    public int Review_ID { get; set; }

    public int Booking_ID { get; set; }

    public int Home_owner_ID { get; set; }

    public int Technician_ID { get; set; }

    public int Rating_Score { get; set; }

    [MaxLength(600)]
    public string? Comment { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime Created_At { get; set; }

    public virtual Booking? Booking { get; set; }

    public virtual HomeOwner? Homeowner { get; set; }

    public virtual Technician? Technician { get; set; }
}
