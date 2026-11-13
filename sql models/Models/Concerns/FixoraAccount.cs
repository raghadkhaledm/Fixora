using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Fixora.Support;

namespace Fixora.Models.Concerns;

public abstract class FixoraAccount : FixoraTable
{
    [Required]
    [MaxLength(50)]
    public string First_Name { get; set; } = "";

    [Required]
    [MaxLength(50)]
    public string Last_Name { get; set; } = "";

    [Required]
    [MaxLength(12)]
    [Column(TypeName = "varchar(12)")]
    public string Phone_Number { get; set; } = "";

    [Required]
    [MaxLength(255)]
    [Column(TypeName = "varchar(255)")]
    [JsonIgnore]
    public string Password { get; set; } = "";

    [Required]
    [MaxLength(150)]
    [Column(TypeName = "varchar(150)")]
    public string Email { get; set; } = "";

    public abstract int GetKey();

    [NotMapped]
    public virtual DateTime? PhoneVerifiedAt
    {
        get => null;
        set { }
    }

    public string GetAuthPassword()
    {
        return Password;
    }

    [NotMapped]
    public string Name => (First_Name + " " + Last_Name).Trim();

    [NotMapped]
    public string ShortName => First_Name + " " + Php.MbSubstr(Last_Name, 0, 1) + ".";

    [NotMapped]
    public string Initials
    {
        get
        {
            var last = Regex.Replace(Last_Name ?? "", "^Al-", "", RegexOptions.IgnoreCase);

            return Php.MbStrtoupper(Php.MbSubstr(First_Name, 0, 1) + Php.MbSubstr(last, 0, 1));
        }
    }

    [NotMapped]
    public string PhoneLocal => Phone.Local(Phone_Number);

    [NotMapped]
    public string PhoneFull => "+966 " + Phone.Local(Phone_Number);

    [NotMapped]
    public string PhoneMasked => Phone.Masked(Phone_Number);

    public string[] AvaColors()
    {
        var isTech = this is Technician;
        var c = isTech ? FixoraConfig.Current.AvaTech : FixoraConfig.Current.AvaOwner;
        var key = GetKey() == 0 ? 1 : GetKey();

        return c[(key - (isTech ? 0 : 1)) % c.Count];
    }

    public string AvaStyleOnBand()
    {
        return AvaStyle().Replace("background:var(--indigo);", "background:var(--blue);");
    }

    public string AvaStyle()
    {
        var c = AvaColors();

        return "background:" + c[0] + ";color:" + c[1];
    }
}
