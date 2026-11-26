using System.ComponentModel.DataAnnotations;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class VerifyTechnicianRequest : FixoraAttributes, IValidatableObject
{
    [FromForm(Name = "decision")]
    [Display(Name = "Decision")]
    [Required(ErrorMessage = "Choose Approve or Reject.")]
    [RegularExpression("^(Verified|Rejected)$", ErrorMessage = "Choose Approve or Reject.")]
    public string? Decision { get; set; }

    [FromForm(Name = "verification_note")]
    [Display(Name = "Reason")]
    [MaxLength(300, ErrorMessage = "Tell the technician what to fix (10–300 characters).")]
    public string? VerificationNote { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var note = VerificationNote ?? "";
        if (Decision == "Rejected" && (note.Length < 10 || note.Length > 300))
        {
            yield return new ValidationResult("Tell the technician what to fix (10–300 characters).", new[] { "verification_note" });
        }
        else if (note != "" && note.Length < 10)
        {
            yield return new ValidationResult("Tell the technician what to fix (10–300 characters).", new[] { "verification_note" });
        }
    }
}
