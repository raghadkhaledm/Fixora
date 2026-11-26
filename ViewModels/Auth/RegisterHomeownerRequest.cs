using System.ComponentModel.DataAnnotations;
using Fixora.Support.Rules;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class RegisterHomeownerRequest : FixoraAttributes, IValidatableObject
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

    [FromForm(Name = "password")]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    [FromForm(Name = "address")]
    [Display(Name = "Home address")]
    [Required(ErrorMessage = AddressMessage)]
    [StringLength(200, MinimumLength = 5, ErrorMessage = AddressMessage)]
    public string? Address { get; set; }

    [FromForm(Name = "city")]
    [Display(Name = "City")]
    public string? City { get; set; }

    [FromForm(Name = "district")]
    [Display(Name = "District")]
    public string? District { get; set; }

    [FromForm(Name = "terms")]
    [Display(Name = "Terms of Service")]
    [Range(typeof(bool), "true", "true", ErrorMessage = TermsMessage)]
    public bool Terms { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        return EmailRules(validationContext, Email)
            .Concat(PhoneRules(validationContext, Phone))
            .Concat(PasswordRules(Password, FirstName, Phone))
            .Concat(CityRule(City))
            .Concat(DistrictRule(City, District));
    }
}
