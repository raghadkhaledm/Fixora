using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fixora.Models.Concerns;
using Fixora.Support;

namespace Fixora.Models;

[Table("Dispute_Ticket")]
public class DisputeTicket : FixoraTable
{
    [Key]
    public int Ticket_ID { get; set; }

    public int Booking_ID { get; set; }

    public int? Admin_ID { get; set; }

    [Required]
    [MaxLength(500)]
    public string Issue_Description { get; set; } = "";

    [Required]
    [MaxLength(12)]
    [Column(TypeName = "varchar(12)")]
    public string Ticket_Status { get; set; } = "Open";

    [Column(TypeName = "decimal(9,2)")]
    public double? Refund_Amount { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime Created_At { get; set; }

    [Required]
    [MaxLength(30)]
    [Column(TypeName = "varchar(30)")]
    public string Problem_Type { get; set; } = "";

    [MaxLength(300)]
    public string? Photo_Path { get; set; }

    [MaxLength(500)]
    public string? Resolution_Note { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime? Resolved_At { get; set; }

    public virtual Booking? Booking { get; set; }

    public virtual Admin? Admin { get; set; }

    [NotMapped]
    public string Ref => "DSP-" + Created_At.Format("y") + "-" + Php.StrPadLeft(Ticket_ID.ToString(), 5, "0");

    public bool IsClosed()
    {
        return Ticket_Status == "Resolved" || Ticket_Status == "Rejected";
    }
}
