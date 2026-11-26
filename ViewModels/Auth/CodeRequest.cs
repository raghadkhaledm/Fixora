using System.ComponentModel.DataAnnotations;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class CodeRequest : FixoraAttributes
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
}
