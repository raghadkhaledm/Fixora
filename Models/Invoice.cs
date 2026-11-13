using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Fixora.Models.Concerns;
using Fixora.Support;

namespace Fixora.Models;

[Table("Invoice")]
public class Invoice : FixoraTable
{
    [Key]
    public int Invoice_ID { get; set; }

    public int Booking_ID { get; set; }

    [Column(TypeName = "decimal(9,2)")]
    public double Labor_Cost { get; set; }

    [Column(TypeName = "decimal(9,2)")]
    public double Material_Cost { get; set; }

    [Column(TypeName = "decimal(7,2)")]
    public double Service_Commission_Fee { get; set; }

    [Column(TypeName = "decimal(7,2)")]
    public double Express_Fee { get; set; }

    [Column(TypeName = "decimal(9,2)")]
    public double Total_Amount { get; set; }

    [MaxLength(10)]
    [Column(TypeName = "varchar(10)")]
    public string? Payment_Method { get; set; }

    [Required]
    [MaxLength(20)]
    [Column(TypeName = "varchar(20)")]
    public string Payment_Status { get; set; } = "Unpaid";

    [MaxLength(6)]
    [Column(TypeName = "char(6)")]
    public string? Cash_OTP_Code { get; set; }

    [Required]
    [MaxLength(120)]
    public string Job_Title { get; set; } = "";

    [MaxLength(250)]
    public string? Work_Notes { get; set; }

    public string? Material_Items { get; set; }

    [Column(TypeName = "decimal(7,2)")]
    public double Points_Discount { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime Issued_At { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime? Paid_At { get; set; }

    [MaxLength(40)]
    public string? Card_Label { get; set; }

    [MaxLength(40)]
    [Column(TypeName = "varchar(40)")]
    public string? Transaction_Ref { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime? OTP_Expires_At { get; set; }

    public int OTP_Attempts { get; set; }

    public virtual Booking? Booking { get; set; }

    [NotMapped]
    public string Ref => Fx.InvoiceRef(Booking);

    public List<MaterialItem> Materials()
    {
        if (string.IsNullOrEmpty(Material_Items))
        {
            return new List<MaterialItem>();
        }
        try
        {
            return JsonSerializer.Deserialize<List<MaterialItem>>(Material_Items) ?? new List<MaterialItem>();
        }
        catch (JsonException)
        {
            return new List<MaterialItem>();
        }
    }

    public bool IsPaid()
    {
        return Payment_Status != "Unpaid";
    }

    public double GrossTotal()
    {
        return Php.Round(Labor_Cost + Material_Cost + Service_Commission_Fee + Express_Fee, 2);
    }

    public int PointsToEarn()
    {
        return Points_Discount > 0 ? 0 : (int)Php.Floor(Labor_Cost + Service_Commission_Fee + Express_Fee);
    }

    public string MethodLabel()
    {
        if (string.IsNullOrEmpty(Payment_Method))
        {
            return "—";
        }

        return !string.IsNullOrEmpty(Card_Label) ? Card_Label : Payment_Method;
    }

    public bool OtpLocked()
    {
        return OTP_Attempts >= FixoraConfig.Current.CashOtpAttempts
            || (OTP_Expires_At.HasValue && OTP_Expires_At.Value.Lt(Clock.Now()));
    }
}
