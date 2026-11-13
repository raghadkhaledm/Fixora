using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fixora.Models.Concerns;

namespace Fixora.Models;

[Table("Booking_Service")]
public class BookingService : FixoraTable
{
    [Key]
    public int Booking_Service_ID { get; set; }

    public int Booking_ID { get; set; }

    public int Service_ID { get; set; }

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(8,2)")]
    public double Agreed_Price { get; set; }

    public virtual Models.Service? Service { get; set; }

    public virtual Booking? Booking { get; set; }
}
