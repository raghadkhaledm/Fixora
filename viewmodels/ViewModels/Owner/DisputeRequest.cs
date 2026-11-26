using System.ComponentModel.DataAnnotations;
using Fixora.Data;
using Fixora.Support;
using Fixora.Support.Http;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class DisputeRequest : FixoraAttributes, IValidatableObject
{
    [FromForm(Name = "problem_type")]
    [Display(Name = "What went wrong")]
    [Required(ErrorMessage = "Choose what went wrong.")]
    public string? ProblemType { get; set; }

    [FromForm(Name = "issue_description")]
    [Display(Name = "Description")]
    [Required(ErrorMessage = "Describe the issue in at least 20 characters.")]
    [StringLength(500, MinimumLength = 20, ErrorMessage = "Describe the issue in at least 20 characters.")]
    public string? IssueDescription { get; set; }

    [FromForm(Name = "refund_amount")]
    [Display(Name = "Requested refund")]
    public double? RefundAmount { get; set; }

    [FromForm(Name = "photo")]
    [Display(Name = "Photo")]
    public IFormFile? Photo { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var db = (AppDbContext)validationContext.GetService(typeof(AppDbContext))!;
        var total = int.TryParse(Current.Context.Request.RouteValues["invoice"]?.ToString(), out var id)
            ? db.Invoices.Where(i => i.Invoice_ID == id).Select(i => i.Total_Amount).FirstOrDefault()
            : 0;

        if (string.IsNullOrEmpty(ProblemType) || !FixoraConfig.Current.ProblemTypes.Contains(ProblemType))
        {
            yield return new ValidationResult("Choose what went wrong.", new[] { "problem_type" });
        }
        if (RefundAmount.HasValue && (RefundAmount.Value < 0 || RefundAmount.Value > total))
        {
            yield return new ValidationResult("Up to SAR " + Php.NumberFormat(total, 2), new[] { "refund_amount" });
        }
        if (Photo != null)
        {
            var ext = Path.GetExtension(Photo.FileName).ToLowerInvariant();
            if ((ext != ".jpg" && ext != ".jpeg" && ext != ".png") || Photo.Length > 10240 * 1024)
            {
                yield return new ValidationResult("Use a JPG or PNG photo under 10 MB.", new[] { "photo" });
            }
        }
    }
}
