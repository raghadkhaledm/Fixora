using System.ComponentModel.DataAnnotations;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class InvoiceStatusRequest : FixoraAttributes, IValidatableObject
{
    [FromForm(Name = "payment_status")]
    [Display(Name = "Payment status")]
    [Required(ErrorMessage = "Choose a status.")]
    [RegularExpression("^(Paid|Refunded)$", ErrorMessage = "Choose a status.")]
    public string? PaymentStatus { get; set; }

    [FromForm(Name = "payment_method")]
    [Display(Name = "Payment method")]
    [RegularExpression("^(Cash|Mada|Card|STC Pay)$", ErrorMessage = "Choose how it was paid.")]
    public string? PaymentMethod { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PaymentStatus == "Paid" && string.IsNullOrEmpty(PaymentMethod))
        {
            yield return new ValidationResult("Choose how it was paid.", new[] { "payment_method" });
        }
    }
}
