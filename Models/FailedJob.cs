using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fixora.Models;

[Table("failed_jobs")]
public class FailedJob
{
    [Key]
    public long Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Uuid { get; set; } = "";

    [Required]
    public string Connection { get; set; } = "";

    [Required]
    public string Queue { get; set; } = "";

    [Required]
    public string Payload { get; set; } = "";

    [Required]
    public string Exception { get; set; } = "";

    [Column(TypeName = "datetime2(0)")]
    public DateTime Failed_At { get; set; }
}
