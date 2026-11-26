using System.ComponentModel.DataAnnotations;
using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Http;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class ChangePasswordRequest : FixoraAttributes, IValidatableObject
{
    [FromForm(Name = "current_password")]
    [Display(Name = "Current password")]
    public string? CurrentPassword { get; set; }

    [FromForm(Name = "password")]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    private static string GuardName()
    {
        return Current.RouteIs("tech.*") ? "technician" : "homeowner";
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var guard = GuardName();
        var u = Auth.Guard(guard).User();
        const string wrongCurrent = "This isn't your current password.";

        if (string.IsNullOrEmpty(CurrentPassword) || u == null || !Hash.Check(CurrentPassword, u.Password))
        {
            yield return new ValidationResult(wrongCurrent, new[] { "current_password" });
        }

        foreach (var r in PasswordRules(Password, u?.First_Name, u?.Phone_Number))
        {
            yield return r;
        }

        if (!string.IsNullOrEmpty(Password) && Password == CurrentPassword)
        {
            yield return new ValidationResult("Choose a password different from the current one.", new[] { "password" });
        }
    }
}
