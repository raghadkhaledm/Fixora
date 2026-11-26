using System.ComponentModel.DataAnnotations;
using Fixora.Data;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class ChooseServiceRequest : FixoraAttributes, IValidatableObject
{
    [FromForm(Name = "service_id")]
    [Display(Name = "Service")]
    [Required(ErrorMessage = "Choose a service to see your upfront price.")]
    public int? ServiceId { get; set; }

    [FromForm(Name = "quantity")]
    [Display(Name = "Quantity")]
    [Required(ErrorMessage = "Choose a quantity between 1 and 10.")]
    public int? Quantity { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var db = (AppDbContext)validationContext.GetService(typeof(AppDbContext))!;
        var s = ServiceId.HasValue ? db.Services.Find(ServiceId.Value) : null;

        if (ServiceId.HasValue && (s == null || !s.Is_Active))
        {
            yield return new ValidationResult("Choose a service to see your upfront price.", new[] { "service_id" });
        }
        var max = s?.MaxQty() ?? 10;
        if (Quantity.HasValue && (Quantity.Value < 1 || Quantity.Value > max))
        {
            yield return new ValidationResult("Choose a quantity between 1 and " + max + ".", new[] { "quantity" });
        }
    }
}
