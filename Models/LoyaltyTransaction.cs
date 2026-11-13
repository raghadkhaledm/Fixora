using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fixora.Models.Concerns;

namespace Fixora.Models;

[Table("Loyalty_Transaction")]
public class LoyaltyTransaction : FixoraTable
{
    [Key]
    public int Transaction_ID { get; set; }

    public int Home_owner_ID { get; set; }

    public int Points_Earned { get; set; }

    public int Points_Redeemed { get; set; }

    [Required]
    [MaxLength(10)]
    [Column(TypeName = "varchar(10)")]
    public string Transaction_Type { get; set; } = "";

    [Column(TypeName = "datetime2(0)")]
    public DateTime Transaction_Date { get; set; }

    public int? Booking_ID { get; set; }

    public virtual HomeOwner? Homeowner { get; set; }

    public virtual Booking? Booking { get; set; }

    public int Delta()
    {
        return Transaction_Type == "Earned" ? Points_Earned : -Points_Redeemed;
    }
}
