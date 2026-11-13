using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fixora.Models.Concerns;
using Fixora.Support;

namespace Fixora.Models;

[Table("Service_Type")]
public class ServiceType : FixoraTable
{
    [Key]
    public int Service_Type_ID { get; set; }

    [Required]
    [MaxLength(40)]
    public string Type_Name { get; set; } = "";

    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = "";

    [Required]
    [MaxLength(40)]
    [Column(TypeName = "varchar(40)")]
    public string Icon_Class { get; set; } = "";

    [Required]
    [MaxLength(400)]
    public string Image_URL { get; set; } = "";

    public virtual ICollection<Service> Services { get; set; } = new List<Service>();

    [NotMapped]
    public bool Exists => Service_Type_ID != 0;

    public IEnumerable<Service> ActiveServices()
    {
        return Services.Where(s => s.Is_Active).OrderBy(s => s.Service_ID);
    }

    public string IconStyle()
    {
        return Type_Name switch
        {
            "Plumbing" => "background:var(--blue-100);color:var(--blue)",
            "Electrical" => "background:var(--orange-100);color:var(--orange-700)",
            "Gas delivery" => "background:#FDE3DC;color:#C8502E",
            "Painting" => "background:#FFF4C2;color:#9A7A00",
            _ => "background:var(--indigo-100);color:var(--indigo)",
        };
    }

    public string ImageUrl()
    {
        return Fx.Url(Image_URL);
    }

    public string FromLabel()
    {
        var active = Services.Where(s => s.Is_Active).ToList();
        if (active.Count == 0)
        {
            return "Coming soon";
        }
        var min = active.OrderBy(s => s.Price).First();
        if (min.Unit == "cylinder")
        {
            return "SAR " + Fx.Amt(min.Price) + " / cylinder";
        }

        return "from SAR " + Fx.Amt(min.Price);
    }
}
