using System.ComponentModel.DataAnnotations;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class ForgotPasswordRequest : FixoraAttributes
{
    [FromForm(Name = "login")]
    [Display(Name = "Email or phone number")]
    [Required(ErrorMessage = "Enter your email or phone number.")]
    [MaxLength(150, ErrorMessage = "We couldn't find a Fixora account with this email or phone number.")]
    public string? Login { get; set; }
}
