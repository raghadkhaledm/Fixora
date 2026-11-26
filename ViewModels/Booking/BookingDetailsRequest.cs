using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Services;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class BookingDetailsRequest : FixoraAttributes, IValidatableObject
{
    [FromForm(Name = "booking_type")]
    [Display(Name = "Booking type")]
    [Required(ErrorMessage = "Choose Express or Scheduled.")]
    [RegularExpression("^(Express|Scheduled)$", ErrorMessage = "Choose Express or Scheduled.")]
    public string? BookingType { get; set; }

    [FromForm(Name = "date")]
    [Display(Name = "Date")]
    public string? Date { get; set; }

    [FromForm(Name = "slot")]
    [Display(Name = "Time slot")]
    public string? Slot { get; set; }

    [FromForm(Name = "address_choice")]
    [Display(Name = "Service address")]
    public string? AddressChoice { get; set; }

    [FromForm(Name = "service_address")]
    [Display(Name = "Service address")]
    public string? ServiceAddress { get; set; }

    [FromForm(Name = "service_district")]
    [Display(Name = "District")]
    public string? ServiceDistrict { get; set; }

    [FromForm(Name = "photos")]
    [Display(Name = "Photos")]
    public List<IFormFile>? Photos { get; set; }

    public List<string> BadPhotos { get; set; } = new();

    public void PrepareForValidation(BookingDraft draft)
    {
        var kept = draft.Photos();
        foreach (var file in Photos ?? new List<IFormFile>())
        {
            if (file == null)
            {
                continue;
            }
            var ext = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
            var okType = (ext == "jpg" || ext == "jpeg" || ext == "png") && (file.ContentType == "image/jpeg" || file.ContentType == "image/png");
            if (file.Length == 0 || !okType || file.Length > 10 * 1024 * 1024)
            {
                BadPhotos.Add(file.FileName);
                continue;
            }
            if (kept.Count >= 3)
            {
                BadPhotos.Add(file.FileName);
                continue;
            }
            var dir = Uploads.TmpDir();
            Directory.CreateDirectory(AppPaths.PublicPath(dir));
            var name = Guid.NewGuid() + "." + (ext == "jpeg" ? "jpg" : ext);
            var size = file.Length;
            using (var stream = new FileStream(Path.Combine(AppPaths.PublicPath(dir), name), FileMode.Create))
            {
                file.CopyTo(stream);
            }
            kept.Add(new DraftPhoto { Path = dir + "/" + name, Name = file.FileName, Size = size });
        }
        draft.Put(new Dictionary<string, object?> { ["photos"] = kept });
        if (string.IsNullOrEmpty(AddressChoice) && (ServiceAddress ?? "").Trim() != "")
        {
            AddressChoice = "other";
        }
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var o = Auth.Guard("homeowner").User<HomeOwner>()!;
        var slots = (SlotService)validationContext.GetService(typeof(SlotService))!;
        var draft = (BookingDraft)validationContext.GetService(typeof(BookingDraft))!;
        var scheduled = BookingType == "Scheduled";
        var dateError = false;

        if (scheduled)
        {
            if (string.IsNullOrEmpty(Date))
            {
                dateError = true;
                yield return new ValidationResult("Pick a day for the visit.", new[] { "date" });
            }
            else if (!DateTime.TryParseExact(Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                     || day.Date <= Clock.Today() || day.Date > slots.Last())
            {
                dateError = true;
                yield return new ValidationResult("Pick a day from tomorrow up to 30 days ahead.", new[] { "date" });
            }

            if (string.IsNullOrEmpty(Slot) || !FixoraConfig.Current.Slots.Contains(Slot))
            {
                yield return new ValidationResult("Pick one of the free slots on this day.", new[] { "slot" });
            }
        }

        const string addressMessage = "Choose your saved address or enter one, so technicians in that zone can see the request.";
        if (AddressChoice != "saved" && AddressChoice != "other")
        {
            yield return new ValidationResult(addressMessage, new[] { "address_choice" });
        }

        if (AddressChoice == "other")
        {
            var address = ServiceAddress ?? "";
            if (address.Length < 5 || address.Length > 250)
            {
                yield return new ValidationResult(addressMessage, new[] { "service_address" });
            }
            var districts = FixoraConfig.Current.Cities.TryGetValue(o.City, out var d) ? d.Keys : Enumerable.Empty<string>();
            if (string.IsNullOrEmpty(ServiceDistrict) || !districts.Contains(ServiceDistrict))
            {
                yield return new ValidationResult("Choose the district of this address.", new[] { "service_district" });
            }
        }

        if (BadPhotos.Count > 0)
        {
            yield return new ValidationResult("\"" + BadPhotos[0] + "\" can't be uploaded. Use a JPG or PNG photo under 10 MB.", new[] { "photos" });
        }

        if (scheduled && !string.IsNullOrEmpty(Date) && !dateError && !string.IsNullOrEmpty(Slot) && FixoraConfig.Current.Slots.Contains(Slot))
        {
            var service = draft.Service();
            var district = AddressChoice == "other" ? ServiceDistrict : o.District;
            if (service != null && !string.IsNullOrEmpty(district))
            {
                var day = DateTime.Parse(Date);
                var list = slots.ForDay(day, service, district, o.City);
                var free = list.Where(s => s.Free).Select(s => s.Start).ToList();
                if (!free.Contains(Slot))
                {
                    yield return new ValidationResult(
                        free.Count > 0 ? "Pick one of the " + free.Count + " free slots on this day." : "This day is fully booked. Pick another day.",
                        new[] { "slot" });
                }
            }
        }
    }
}
