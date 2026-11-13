using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fixora.Models.Concerns;

namespace Fixora.Models;

[Table("Technician_Service")]
public class TechnicianService : FixoraTable
{
    [Key]
    public int Technician_Service_ID { get; set; }

    public int Technician_ID { get; set; }

    public int Service_ID { get; set; }

    public virtual Technician? Technician { get; set; }

    public virtual Service? Service { get; set; }
}
