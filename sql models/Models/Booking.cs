using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;
using Fixora.Models.Concerns;
using Fixora.Support;

namespace Fixora.Models;

[Table("Booking")]
public class Booking : FixoraTable
{
    [Key]
    public int Booking_ID { get; set; }

    public int Home_owner_ID { get; set; }

    public int Technician_ID { get; set; }

    [Required]
    [MaxLength(10)]
    [Column(TypeName = "varchar(10)")]
    public string Booking_Type { get; set; } = "";

    [Column(TypeName = "datetime2(0)")]
    public DateTime Scheduled_Date_Time { get; set; }

    [MaxLength(1000)]
    public string? Issue_Attachment_Photo { get; set; }

    [Required]
    [MaxLength(10)]
    [Column(TypeName = "varchar(10)")]
    public string Status { get; set; } = "Pending";

    [Column(TypeName = "datetime2(0)")]
    public DateTime Created_At { get; set; }

    [Required]
    [MaxLength(12)]
    [Column(TypeName = "varchar(12)")]
    public string Job_Progress_Status { get; set; } = "Requested";

    [Required]
    [MaxLength(250)]
    public string Service_Address { get; set; } = "";

    [Required]
    [MaxLength(60)]
    public string Service_District { get; set; } = "";

    [Column(TypeName = "datetime2(0)")]
    public DateTime? Accepted_At { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime? On_The_Way_At { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime? Arrived_At { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime? Started_At { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime? Completed_At { get; set; }

    [MaxLength(200)]
    public string? Decline_Reason { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime? Cancelled_At { get; set; }

    [MaxLength(200)]
    public string? Cancel_Reason { get; set; }

    public virtual HomeOwner? Homeowner { get; set; }

    public virtual Technician? Technician { get; set; }

    public virtual ICollection<BookingService> Items { get; set; } = new List<BookingService>();

    public virtual Invoice? Invoice { get; set; }

    public virtual Review? Review { get; set; }

    public virtual DisputeTicket? Dispute { get; set; }

    [NotMapped]
    public string Ref => Fx.BookingRef(this);

    public BookingService? Item()
    {
        return Items.FirstOrDefault();
    }

    public Models.Service? Service()
    {
        return Item()?.Service;
    }

    public string ServiceName()
    {
        return Service()?.Service_Name ?? "Service";
    }

    public double LaborTotal()
    {
        return Php.Round(Items.Sum(i => i.Agreed_Price * i.Quantity), 2);
    }

    public double FeeTotal()
    {
        return Items.Count == 0
            ? FixoraConfig.Current.PlatformFee
            : Items.Select(i => i.Service?.Service_Commission_Fee ?? FixoraConfig.Current.PlatformFee).Max();
    }

    public double ExpressFee()
    {
        return Booking_Type == "Express" ? FixoraConfig.Current.ExpressFee : 0.0;
    }

    public double Estimate()
    {
        return Php.Round(LaborTotal() + FeeTotal() + ExpressFee(), 2);
    }

    public string AddressShort()
    {
        return Regex.Replace(Service_Address ?? "", @",\s*(Riyadh|Jeddah|Dammam)\s*$", "");
    }

    public string StreetPart()
    {
        var s = AddressShort();
        var d = Regex.Escape(Service_District ?? "");

        return Regex.Replace(s, @",\s*" + d + @"\s*$", "");
    }

    public static string FullAddress(string street, string district, string city)
    {
        street = street.Trim();
        if (Regex.IsMatch(street, @",\s*" + Regex.Escape(district) + @"(,\s*" + Regex.Escape(city) + @")?\s*$", RegexOptions.IgnoreCase))
        {
            return street.ToLowerInvariant().EndsWith(city.ToLowerInvariant()) ? street : street + ", " + city;
        }

        return street + ", " + district + ", " + city;
    }

    public List<string> Photos()
    {
        return (Issue_Attachment_Photo ?? "").Split('|').Where(p => p != "").ToList();
    }

    public bool IsExpress()
    {
        return Booking_Type == "Express";
    }

    public string WhenLabel()
    {
        if (IsExpress())
        {
            return Scheduled_Date_Time.Format("D j M, H:i");
        }
        var s = Scheduled_Date_Time;

        return s.Format("D j M") + " · " + s.Format("H:i") + " – " + s.AddHours(2).Format("H:i");
    }

    public int ProgressIndex()
    {
        var steps = new List<string> { "Requested", "Accepted", "On the way", "In progress", "Completed" };

        return Math.Max(0, steps.IndexOf(Job_Progress_Status));
    }

    public string StatusLabel()
    {
        if (Status == "Accepted")
        {
            return Job_Progress_Status == "Accepted" ? "Accepted" : Job_Progress_Status;
        }

        return Status;
    }

    public bool IsOpen()
    {
        return new[] { "Pending", "Accepted" }.Contains(Status);
    }

    public bool CanCancelByOwner()
    {
        return Status == "Pending" || Status == "Rejected"
            || (Status == "Accepted" && Job_Progress_Status == "Accepted");
    }
}
