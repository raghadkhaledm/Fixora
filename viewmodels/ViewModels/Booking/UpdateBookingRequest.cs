using System.ComponentModel.DataAnnotations;
using Fixora.Data;
using Fixora.Support.Authentication;
using Fixora.Support.Http;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class UpdateBookingRequest : FixoraAttributes, IValidatableObject
{
    [FromForm(Name = "service_address")]
    [Display(Name = "Service address")]
    [Required(ErrorMessage = "Enter the service address (5–250 characters).")]
    [StringLength(250, MinimumLength = 5, ErrorMessage = "Enter the service address (5–250 characters).")]
    public string? ServiceAddress { get; set; }

    [FromForm(Name = "photos")]
    [Display(Name = "Photos")]
    public List<IFormFile>? Photos { get; set; }

    [FromForm(Name = "remove")]
    public List<string>? Remove { get; set; }

    public bool Authorize(AppDbContext db)
    {
        int.TryParse(Current.Context.Request.RouteValues["booking"]?.ToString(), out var id);
        var b = db.Bookings.Find(id);
        if (b == null || b.Home_owner_ID != Auth.Guard("homeowner").Id())
        {
            throw new FileNotFoundException();
        }
        if (b.Status != "Pending")
        {
            throw new UnauthorizedAccessException();
        }

        return true;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var db = (AppDbContext)validationContext.GetService(typeof(AppDbContext))!;
        Authorize(db);

        if (Photos != null && Photos.Count > 3)
        {
            yield return new ValidationResult("Add up to 3 photos.", new[] { "photos" });
        }
        foreach (var f in Photos ?? new List<IFormFile>())
        {
            var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
            if ((ext != ".jpg" && ext != ".jpeg" && ext != ".png") || f.Length > 10240 * 1024)
            {
                yield return new ValidationResult("Use a JPG or PNG photo under 10 MB.", new[] { "photos" });
                break;
            }
        }
    }
}
