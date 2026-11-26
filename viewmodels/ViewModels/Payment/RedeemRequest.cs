using System.ComponentModel.DataAnnotations;
using Fixora.Data;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Http;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class RedeemRequest : FixoraAttributes, IValidatableObject
{
    [FromForm(Name = "redeem_points")]
    [Display(Name = "Points to redeem")]
    [Required(ErrorMessage = "You don't have enough points for this reward.")]
    public int? RedeemPoints { get; set; }

    public bool Authorize(AppDbContext db)
    {
        int.TryParse(Current.Context.Request.RouteValues["invoice"]?.ToString(), out var id);
        var homeOwnerId = db.Invoices.Where(i => i.Invoice_ID == id).Select(i => (int?)i.Booking!.Home_owner_ID).FirstOrDefault();
        if (homeOwnerId == null || homeOwnerId != Auth.Guard("homeowner").Id())
        {
            throw new FileNotFoundException();
        }

        return true;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var db = (AppDbContext)validationContext.GetService(typeof(AppDbContext))!;
        Authorize(db);

        var allowed = new List<int> { 0 };
        allowed.AddRange(FixoraConfig.Current.Tiers.Keys);
        if (RedeemPoints.HasValue && !allowed.Contains(RedeemPoints.Value))
        {
            yield return new ValidationResult("You don't have enough points for this reward.", new[] { "redeem_points" });
        }
    }
}
