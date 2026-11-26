using System.ComponentModel.DataAnnotations;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class DeleteAccountRequest : FixoraAttributes
{
    [FromForm(Name = "confirm")]
    [Display(Name = "Confirmation")]
    [Required(ErrorMessage = "Type DELETE to confirm.")]
    [RegularExpression("^DELETE$", ErrorMessage = "Type DELETE to confirm.")]
    public string? Confirm { get; set; }
}
