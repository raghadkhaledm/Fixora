using System.ComponentModel.DataAnnotations;
using Fixora.Data;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Http;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class RoomPriceRequest : FixoraAttributes, IValidatableObject
{
    [FromForm(Name = "agreed_price")]
    [Display(Name = "Agreed price per room")]
    [Required(ErrorMessage = "Agree a room price.")]
    public double? AgreedPrice { get; set; }

    public bool Authorize(AppDbContext db)
    {
        int.TryParse(Current.Context.Request.RouteValues["booking"]?.ToString(), out var id);
        var technicianId = db.Bookings.Where(b => b.Booking_ID == id).Select(b => (int?)b.Technician_ID).FirstOrDefault();
        if (technicianId == null || technicianId != Auth.Guard("technician").Id())
        {
            throw new FileNotFoundException();
        }

        return true;
    }

    private static double Base(AppDbContext db)
    {
        int.TryParse(Current.Context.Request.RouteValues["booking"]?.ToString(), out var id);

        return db.BookingServices.Where(i => i.Booking_ID == id).OrderBy(i => i.Booking_Service_ID).Select(i => i.Service!.Price).FirstOrDefault();
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var db = (AppDbContext)validationContext.GetService(typeof(AppDbContext))!;
        Authorize(db);
        var baseline = Base(db);
        var min = Php.Round(baseline * 0.75, 2);
        var max = Php.Round(baseline * 1.25, 2);

        if (!AgreedPrice.HasValue || AgreedPrice.Value < min || AgreedPrice.Value > max)
        {
            yield return new ValidationResult(
                "Agree a room price between SAR " + Php.NumberFormat(baseline * 0.75, 2) + " and SAR " + Php.NumberFormat(baseline * 1.25, 2) + ".",
                new[] { "agreed_price" });
        }
    }
}
