using System.ComponentModel.DataAnnotations;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class ConfirmBookingRequest : FixoraAttributes
{
    [FromForm(Name = "terms")]
    [Display(Name = "Terms of Service")]
    [Range(typeof(bool), "true", "true", ErrorMessage = "Accept the service terms and cancellation policy.")]
    public bool Terms { get; set; }
}
