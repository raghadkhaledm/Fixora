using System.ComponentModel.DataAnnotations;
using Fixora.Data;
using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Http;
using Fixora.Support.Rules;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class TechnicianDocumentsRequest : FixoraAttributes, IValidatableObject
{
    private string? _nationalId;

    [FromForm(Name = "national_id")]
    [Display(Name = "National ID / Iqama number")]
    public string? NationalId
    {
        get => _nationalId;
        set => _nationalId = DigitsOnly(value);
    }

    [FromForm(Name = "national_id_file")]
    [Display(Name = "National ID")]
    public IFormFile? NationalIdFile { get; set; }

    [FromForm(Name = "license_file")]
    [Display(Name = "Vocational license")]
    public IFormFile? LicenseFile { get; set; }

    [FromForm(Name = "services")]
    [Display(Name = "Services you offer")]
    public List<int>? Services { get; set; }

    [FromForm(Name = "zones")]
    [Display(Name = "Coverage zones")]
    public List<string>? Zones { get; set; }

    [FromForm(Name = "hourly_rate")]
    [Display(Name = "Hourly rate")]
    public int? HourlyRate { get; set; }

    [FromForm(Name = "trade")]
    public int? Trade { get; set; }

    public (int TypeId, string City, Technician? Tech) Context(AppDbContext db)
    {
        var t = Auth.Guard("technician").User<Technician>();
        if (t != null)
        {
            var typeId = db.TechnicianServices.Where(ts => ts.Technician_ID == t.Technician_ID).Select(ts => ts.Service!.Service_Type_ID).FirstOrDefault();

            return (typeId != 0 ? typeId : (Trade ?? 0), t.City, t);
        }
        var d = SessionStore.Get<Dictionary<string, string>>("tech_draft") ?? new Dictionary<string, string>();
        d.TryGetValue("trade", out var trade);
        d.TryGetValue("city", out var city);

        return (int.TryParse(trade, out var tr) ? tr : 0, city ?? "", null);
    }

    private static readonly string[] ImageAndPdf = { ".jpg", ".jpeg", ".png", ".pdf" };

    private static bool FileOk(IFormFile f)
    {
        return ImageAndPdf.Contains(Path.GetExtension(f.FileName).ToLowerInvariant()) && f.Length <= 5120 * 1024;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var db = (AppDbContext)validationContext.GetService(typeof(AppDbContext))!;
        var (typeId, city, tech) = Context(db);
        var districts = FixoraConfig.Current.Cities.TryGetValue(city, out var d) ? d.Keys.ToList() : new List<string>();
        var serviceIds = db.Services.Where(s => s.Service_Type_ID == typeId && s.Is_Active).Select(s => s.Service_ID).ToList();
        var fileRequired = tech == null || tech.Verification_Status == "Rejected";

        const string idMessage = "Enter the 10-digit national ID or Iqama number.";
        if (string.IsNullOrEmpty(NationalId) || !System.Text.RegularExpressions.Regex.IsMatch(NationalId, @"^[12]\d{9}$"))
        {
            yield return new ValidationResult(idMessage, new[] { "national_id" });
        }
        else if (AccountUniqueness.NationalIdTaken(db, NationalId, tech?.Technician_ID))
        {
            yield return new ValidationResult("This ID number already has a Fixora account.", new[] { "national_id" });
        }

        if (NationalIdFile == null)
        {
            if (fileRequired)
            {
                yield return new ValidationResult("Upload your national ID (JPG, PNG or PDF up to 5 MB).", new[] { "national_id_file" });
            }
        }
        else if (!FileOk(NationalIdFile))
        {
            yield return new ValidationResult("Upload your national ID as JPG, PNG or PDF up to 5 MB.", new[] { "national_id_file" });
        }

        if (LicenseFile == null)
        {
            if (fileRequired)
            {
                yield return new ValidationResult("Upload your vocational license (PDF, JPG or PNG up to 5 MB).", new[] { "license_file" });
            }
        }
        else if (!FileOk(LicenseFile))
        {
            yield return new ValidationResult("Upload your vocational license as PDF, JPG or PNG up to 5 MB.", new[] { "license_file" });
        }

        if (Services == null || Services.Count < 1 || Services.Any(s => !serviceIds.Contains(s)))
        {
            yield return new ValidationResult("Choose at least one service.", new[] { "services" });
        }

        if (Zones == null || Zones.Count < 1)
        {
            yield return new ValidationResult("Choose the districts you cover.", new[] { "zones" });
        }
        else if (Zones.Count > 6)
        {
            yield return new ValidationResult("Choose up to 6 districts you cover.", new[] { "zones" });
        }
        else if (Zones.Any(z => !districts.Contains(z)))
        {
            yield return new ValidationResult("Choose the districts you cover.", new[] { "zones" });
        }

        if (!HourlyRate.HasValue || HourlyRate.Value < 30 || HourlyRate.Value > 500)
        {
            yield return new ValidationResult("Set an hourly rate between SAR 30 and SAR 500.", new[] { "hourly_rate" });
        }
    }
}
