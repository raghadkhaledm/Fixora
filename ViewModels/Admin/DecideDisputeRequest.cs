using System.ComponentModel.DataAnnotations;
using Fixora.Data;
using Fixora.Support;
using Fixora.Support.Http;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fixora.ViewModels;

public class DecideDisputeRequest : FixoraAttributes, IValidatableObject
{
    [FromForm(Name = "status")]
    [Display(Name = "Status")]
    [Required(ErrorMessage = "Choose Resolve or Reject.")]
    [RegularExpression("^(Resolved|Rejected)$", ErrorMessage = "Choose Resolve or Reject.")]
    public string? Status { get; set; }

    [FromForm(Name = "refund_amount")]
    [Display(Name = "Requested refund")]
    public double? RefundAmount { get; set; }

    [FromForm(Name = "resolution_note")]
    [Display(Name = "Note")]
    [Required(ErrorMessage = "Explain the decision to the homeowner (10–500 characters).")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Explain the decision to the homeowner (10–500 characters).")]
    public string? ResolutionNote { get; set; }

    private static double Total(ValidationContext ctx)
    {
        var db = (AppDbContext?)ctx.GetService(typeof(AppDbContext));
        var raw = Current.Context.Request.RouteValues["ticket"]?.ToString();
        if (db == null || !int.TryParse(raw, out var id))
        {
            return 0;
        }

        return db.DisputeTickets.Where(t => t.Ticket_ID == id).Select(t => t.Booking!.Invoice!.Total_Amount).FirstOrDefault();
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var total = Total(validationContext);
        if (Status == "Resolved" && RefundAmount.HasValue && (RefundAmount.Value < 0 || RefundAmount.Value > total))
        {
            yield return new ValidationResult("Up to SAR " + Php.NumberFormat(total, 2), new[] { "refund_amount" });
        }
    }
}
