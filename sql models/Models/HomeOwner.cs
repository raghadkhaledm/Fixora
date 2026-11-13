using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fixora.Models.Concerns;

namespace Fixora.Models;

[Table("Home_Owner")]
public class HomeOwner : FixoraAccount
{
    [Key]
    public int Home_owner_ID { get; set; }

    [Required]
    [MaxLength(200)]
    public string Address { get; set; } = "";

    [Required]
    [MaxLength(20)]
    [Column(TypeName = "varchar(20)")]
    public string City { get; set; } = "";

    [MaxLength(40)]
    public string? Credit_Card { get; set; }

    public int Loyalty_Points { get; set; }

    [Required]
    [MaxLength(60)]
    public string District { get; set; } = "";

    [Column(TypeName = "decimal(9,6)")]
    public double Latitude { get; set; }

    [Column(TypeName = "decimal(9,6)")]
    public double Longitude { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime? Phone_Verified_At { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime Created_At { get; set; }

    [NotMapped]
    public int Bookings_Count { get; set; }

    [NotMapped]
    public int Completed_Count { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual ICollection<LoyaltyTransaction> Loyalty { get; set; } = new List<LoyaltyTransaction>();

    public override int GetKey()
    {
        return Home_owner_ID;
    }

    [NotMapped]
    public override DateTime? PhoneVerifiedAt
    {
        get => Phone_Verified_At;
        set => Phone_Verified_At = value;
    }

    [NotMapped]
    public string Role => "homeowner";

    [NotMapped]
    public string FullAddress => Address + ", " + District + ", " + City;

    public bool IsDeleted()
    {
        return (Email ?? "").EndsWith("@fixora.invalid");
    }
}
