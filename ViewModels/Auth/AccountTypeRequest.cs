using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class AccountTypeRequest
{
    [FromForm(Name = "type")]
    [Display(Name = "Account type")]
    [Required(ErrorMessage = "Choose how you will use Fixora.")]
    [RegularExpression("^(homeowner|technician)$", ErrorMessage = "Choose how you will use Fixora.")]
    public string? Type { get; set; }
}
