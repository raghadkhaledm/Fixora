using System.ComponentModel.DataAnnotations;
using Fixora.Support.Http;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class ResetPasswordRequest : FixoraAttributes, IValidatableObject
{
    private string? _code;

    [FromForm(Name = "code")]
    [Display(Name = "Code")]
    [Required(ErrorMessage = CodeMessage)]
    [RegularExpression(@"^\d{6}$", ErrorMessage = CodeMessage)]
    public string? Code
    {
        get => _code;
        set => _code = DigitsOnly(value);
    }

    [FromForm(Name = "password")]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var r = SessionStore.Get<Dictionary<string, string>>("fx_reset") ?? new Dictionary<string, string>();
        r.TryGetValue("first", out var first);
        r.TryGetValue("phone", out var phone);

        return PasswordRules(Password, first, phone);
    }
}
