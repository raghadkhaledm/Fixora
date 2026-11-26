using System.ComponentModel.DataAnnotations;
using Fixora.Support.Authentication;
using Fixora.Support.Rules;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class UpdateTechnicianRequest : FixoraAttributes, IValidatableObject
{
    private string? _email;
    private string? _phone;

    [FromForm(Name = "first_name")]
    [Display(Name = "First name")]
    [Required(ErrorMessage = FirstNameMessage)]
    [StringLength(50, MinimumLength = 2, ErrorMessage = FirstNameMessage)]
    [RegularExpression(NamePattern, ErrorMessage = FirstNameMessage)]
    public string? FirstName { get; set; }

    [FromForm(Name = "last_name")]
    [Display(Name = "Last name")]
    [Required(ErrorMessage = LastNameMessage)]
    [StringLength(50, MinimumLength = 2, ErrorMessage = LastNameMessage)]
    [RegularExpression(NamePattern, ErrorMessage = LastNameMessage)]
    public string? LastName { get; set; }

    [FromForm(Name = "email")]
    [Display(Name = "Email")]
    [Required(ErrorMessage = EmailRequiredMessage)]
    [EmailAddress(ErrorMessage = EmailInvalidMessage)]
    [MaxLength(150, ErrorMessage = EmailInvalidMessage)]
    public string? Email
    {
        get => _email;
        set => _email = NormalizeEmail(value);
    }

    [FromForm(Name = "phone")]
    [Display(Name = "Phone number")]
    [Required(ErrorMessage = PhoneRequiredMessage)]
    [FixoraMobile]
    public string? Phone
    {
        get => _phone;
        set => _phone = NormalizePhone(value);
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var id = Auth.Guard("technician").Id();

        return EmailRules(validationContext, Email, "email", "Technician", id)
            .Concat(PhoneRules(validationContext, Phone, "phone", "Technician", id));
    }
}
