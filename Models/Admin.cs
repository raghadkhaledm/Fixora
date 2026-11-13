using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fixora.Models.Concerns;

namespace Fixora.Models;

[Table("Admin")]
public class Admin : FixoraAccount
{
    [Key]
    public int Admin_ID { get; set; }

    public virtual ICollection<DisputeTicket> Tickets { get; set; } = new List<DisputeTicket>();

    public override int GetKey()
    {
        return Admin_ID;
    }

    [NotMapped]
    public string Role => "admin";
}
