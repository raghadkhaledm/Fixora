using System.ComponentModel.DataAnnotations;
using Fixora.Models;
using Fixora.Support.Authentication;
using Fixora.Support.Rules;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class ChangePhoneRequest : FixoraAttributes, IValidatableObject
{
    private string? _phone;

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
        var owner = Auth.Guard("homeowner").User<HomeOwner>();
        var tech = owner != null ? null : Auth.Guard("technician").User<Technician>();

        return owner != null
            ? PhoneRules(validationContext, Phone, "phone", "Home_Owner", owner.GetKey())
            : PhoneRules(validationContext, Phone, "phone", "Technician", tech?.GetKey());
    }
}
