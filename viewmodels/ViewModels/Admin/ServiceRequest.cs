using System.ComponentModel.DataAnnotations;
using Fixora.Data;
using Fixora.Support;
using Fixora.Support.Http;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class ServiceRequest : FixoraAttributes, IValidatableObject
{
    [FromForm(Name = "service_type_id")]
    [Display(Name = "Category")]
    [Required(ErrorMessage = "Choose the category.")]
    public int? ServiceTypeId { get; set; }

    [FromForm(Name = "service_name")]
    [Display(Name = "Service name")]
    [Required(ErrorMessage = "Name the service (3–80 characters).")]
    [StringLength(80, MinimumLength = 3, ErrorMessage = "Name the service (3–80 characters).")]
    public string? ServiceName { get; set; }

    [FromForm(Name = "service_category")]
    [Display(Name = "Category")]
    [Required(ErrorMessage = "Choose Repair, Installation, Inspection, Delivery, Assembly or Painting.")]
    public string? ServiceCategory { get; set; }

    [FromForm(Name = "price")]
    [Display(Name = "Price")]
    [Required(ErrorMessage = "Set a price between SAR 10 and SAR 20,000.")]
    [Range(10, 20000, ErrorMessage = "Set a price between SAR 10 and SAR 20,000.")]
    public double? Price { get; set; }

    [FromForm(Name = "unit")]
    [Display(Name = "Unit")]
    [Required(ErrorMessage = "Choose job, hour, cylinder or room.")]
    public string? Unit { get; set; }

    [FromForm(Name = "service_commission_fee")]
    [Display(Name = "Platform fee")]
    [Required(ErrorMessage = "Set a platform fee between SAR 0 and SAR 500.")]
    [Range(0, 500, ErrorMessage = "Set a platform fee between SAR 0 and SAR 500.")]
    public double? ServiceCommissionFee { get; set; }

    [FromForm(Name = "description")]
    [Display(Name = "Description")]
    [Required(ErrorMessage = "Describe the service in 10–250 characters.")]
    [StringLength(250, MinimumLength = 10, ErrorMessage = "Describe the service in 10–250 characters.")]
    public string? Description { get; set; }

    [FromForm(Name = "is_active")]
    public bool IsActive { get; set; }

    public bool FlexibleRate => Unit == "hr";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var db = (AppDbContext)validationContext.GetService(typeof(AppDbContext))!;
        int? id = int.TryParse(Current.Context.Request.RouteValues["service"]?.ToString(), out var sid) ? sid : null;

        if (ServiceTypeId.HasValue && !db.ServiceTypes.Any(t => t.Service_Type_ID == ServiceTypeId.Value))
        {
            yield return new ValidationResult("Choose the category.", new[] { "service_type_id" });
        }
        if (!string.IsNullOrEmpty(ServiceName) && ServiceTypeId.HasValue
            && db.Services.Any(s => s.Service_Name == ServiceName && s.Service_Type_ID == ServiceTypeId.Value && s.Service_ID != id))
        {
            yield return new ValidationResult("This category already has a service with this name.", new[] { "service_name" });
        }
        if (string.IsNullOrEmpty(ServiceCategory) || !FixoraConfig.Current.Categories.Contains(ServiceCategory))
        {
            yield return new ValidationResult("Choose Repair, Installation, Inspection, Delivery, Assembly or Painting.", new[] { "service_category" });
        }
        if (string.IsNullOrEmpty(Unit) || !FixoraConfig.Current.Units.Contains(Unit))
        {
            yield return new ValidationResult("Choose job, hour, cylinder or room.", new[] { "unit" });
        }
    }
}
