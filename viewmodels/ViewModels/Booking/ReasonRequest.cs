using System.ComponentModel.DataAnnotations;
using Fixora.Data;
using Fixora.Support.Http;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class ReasonRequest : FixoraAttributes, IValidatableObject
{
    [FromForm(Name = "decline_reason")]
    [Display(Name = "Reason")]
    public string? DeclineReason { get; set; }

    [FromForm(Name = "cancel_reason")]
    [Display(Name = "Reason")]
    public string? CancelReason { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var db = (AppDbContext)validationContext.GetService(typeof(AppDbContext))!;
        if (Current.RouteIs("tech.decline"))
        {
            var d = DeclineReason ?? "";
            if (d.Length < 5 || d.Length > 200)
            {
                yield return new ValidationResult("Tell the homeowner why you can't take this job.", new[] { "decline_reason" });
            }

            yield break;
        }

        var booking = int.TryParse(Current.Context.Request.RouteValues["booking"]?.ToString(), out var id) ? db.Bookings.Find(id) : null;
        var needed = booking != null && (booking.Status == "Accepted" || Current.RouteIs("admin.*"));
        var c = CancelReason ?? "";
        if ((needed && c.Length == 0) || (c.Length > 0 && (c.Length < 5 || c.Length > 200)))
        {
            yield return new ValidationResult("Tell us why you are cancelling (5–200 characters).", new[] { "cancel_reason" });
        }
    }
}
