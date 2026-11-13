using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fixora.Models.Concerns;

namespace Fixora.Models;

[Table("Technician")]
public class Technician : FixoraAccount
{
    [Key]
    public int Technician_ID { get; set; }

    [Required]
    [MaxLength(10)]
    [Column(TypeName = "char(10)")]
    public string National_ID { get; set; } = "";

    [Required]
    [MaxLength(300)]
    public string Vocational_License { get; set; } = "";

    [Required]
    [MaxLength(10)]
    [Column(TypeName = "varchar(10)")]
    public string Verification_Status { get; set; } = "Pending";

    [Column(TypeName = "decimal(6,2)")]
    public double Hourly_Rate { get; set; }

    [Required]
    [MaxLength(300)]
    public string Coverage_Zone { get; set; } = "";

    [Column(TypeName = "decimal(2,1)")]
    public double? Average_Rating { get; set; }

    public bool Is_Available { get; set; }

    [Required]
    [MaxLength(20)]
    [Column(TypeName = "varchar(20)")]
    public string City { get; set; } = "";

    [Required]
    [MaxLength(300)]
    public string National_ID_File { get; set; } = "";

    [Column(TypeName = "decimal(9,6)")]
    public double Latitude { get; set; }

    [Column(TypeName = "decimal(9,6)")]
    public double Longitude { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime? Verified_At { get; set; }

    [MaxLength(300)]
    public string? Verification_Note { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime? Phone_Verified_At { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime Created_At { get; set; }

    [NotMapped]
    public int Reviews_Count { get; set; }

    [NotMapped]
    public int Jobs_Count { get; set; }

    public virtual ICollection<Service> Services { get; set; } = new List<Service>();

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public override int GetKey()
    {
        return Technician_ID;
    }

    [NotMapped]
    public override DateTime? PhoneVerifiedAt
    {
        get => Phone_Verified_At;
        set => Phone_Verified_At = value;
    }

    [NotMapped]
    public string Role => "technician";

    [NotMapped]
    public List<string> Zones => (Coverage_Zone ?? "")
        .Split(',')
        .Select(z => z.Trim())
        .Where(z => z != "")
        .ToList();

    public bool IsVerified()
    {
        return Verification_Status == "Verified";
    }

    public ServiceType? ServiceType()
    {
        var s = Services.FirstOrDefault();

        return s?.Type;
    }

    public string TradeLabel()
    {
        var t = ServiceType();

        return t?.Type_Name switch
        {
            "Plumbing" => "Plumber",
            "Electrical" => "Electrician",
            "Gas delivery" => "Gas technician",
            "Painting" => "Painter",
            "Furniture assembly" => "Furniture assembler",
            _ => "Technician",
        };
    }

    public bool IsDeleted()
    {
        return (Email ?? "").EndsWith("@fixora.invalid");
    }
}
