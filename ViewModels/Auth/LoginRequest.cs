using System.ComponentModel.DataAnnotations;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class LoginRequest : FixoraAttributes
{
    [FromForm(Name = "login")]
    [Display(Name = "Email or phone number")]
    [Required(ErrorMessage = "Enter your email or phone number.")]
    [MaxLength(150)]
    public string? Login { get; set; }

    [FromForm(Name = "password")]
    [Display(Name = "Password")]
    [Required(ErrorMessage = "Enter your password.")]
    [MaxLength(64)]
    public string? Password { get; set; }
}
