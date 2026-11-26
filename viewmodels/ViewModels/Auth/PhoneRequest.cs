using System.ComponentModel.DataAnnotations;
using Fixora.Support.Rules;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class PhoneRequest : FixoraAttributes
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
}
