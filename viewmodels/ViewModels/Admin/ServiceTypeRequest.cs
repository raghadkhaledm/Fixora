using System.ComponentModel.DataAnnotations;
using Fixora.Data;
using Fixora.Support.Http;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class ServiceTypeRequest : FixoraAttributes, IValidatableObject
{
    [FromForm(Name = "type_name")]
    [Display(Name = "Category name")]
    [Required(ErrorMessage = "Name the category (3–40 characters).")]
    [StringLength(40, MinimumLength = 3, ErrorMessage = "Name the category (3–40 characters).")]
    public string? TypeName { get; set; }

    [FromForm(Name = "description")]
    [Display(Name = "Description")]
    [Required(ErrorMessage = "Describe the category in 5–200 characters.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "Describe the category in 5–200 characters.")]
    public string? Description { get; set; }

    [FromForm(Name = "icon_class")]
    [Display(Name = "Icon")]
    [Required(ErrorMessage = "Use a Bootstrap Icons class such as bi-droplet-half.")]
    [MaxLength(40, ErrorMessage = "Use a Bootstrap Icons class such as bi-droplet-half.")]
    [RegularExpression("^bi-[a-z0-9-]+$", ErrorMessage = "Use a Bootstrap Icons class such as bi-droplet-half.")]
    public string? IconClass { get; set; }

    [FromForm(Name = "image_url")]
    [Display(Name = "Image")]
    [MaxLength(400, ErrorMessage = "Paste an image link (https://…) or upload a photo.")]
    public string? ImageUrl { get; set; }

    [FromForm(Name = "image")]
    [Display(Name = "Image")]
    public IFormFile? Image { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var db = (AppDbContext)validationContext.GetService(typeof(AppDbContext))!;
        int? id = int.TryParse(Current.Context.Request.RouteValues["type"]?.ToString(), out var tid) ? tid : null;

        if (!string.IsNullOrEmpty(TypeName) && db.ServiceTypes.Any(t => t.Type_Name == TypeName && t.Service_Type_ID != id))
        {
            yield return new ValidationResult("A category with this name already exists.", new[] { "type_name" });
        }
        if (string.IsNullOrEmpty(ImageUrl))
        {
            if (id == null && Image == null)
            {
                yield return new ValidationResult("Paste an image link (https://…) or upload a photo.", new[] { "image_url" });
            }
        }
        else if (!Uri.TryCreate(ImageUrl, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            yield return new ValidationResult("Paste an image link (https://…) or upload a photo.", new[] { "image_url" });
        }
        if (Image != null)
        {
            var ext = Path.GetExtension(Image.FileName).ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" || Image.Length > 5120 * 1024)
            {
                yield return new ValidationResult("Upload a JPG or PNG photo up to 5 MB.", new[] { "image" });
            }
        }
    }
}
