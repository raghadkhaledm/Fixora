using System.ComponentModel.DataAnnotations;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class StoreInvoiceRequest : FixoraAttributes, IValidatableObject
{
    private List<MaterialInput>? _materials;

    [FromForm(Name = "job_title")]
    [Display(Name = "Job title")]
    [Required(ErrorMessage = "Give the job a title (5–120 characters), e.g. \"Leak repair under kitchen sink\".")]
    [StringLength(120, MinimumLength = 5, ErrorMessage = "Give the job a title (5–120 characters), e.g. \"Leak repair under kitchen sink\".")]
    public string? JobTitle { get; set; }

    [FromForm(Name = "work_notes")]
    [Display(Name = "Work notes")]
    [MaxLength(250, ErrorMessage = "Keep the notes under 250 characters.")]
    public string? WorkNotes { get; set; }

    [FromForm(Name = "materials")]
    [Display(Name = "Materials")]
    public List<MaterialInput>? Materials
    {
        get => _materials;
        set => _materials = value?
            .Where(r => (r.Name ?? "").Trim() != "" || (r.Amount?.ToString() ?? "").Trim() != "")
            .ToList();
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Materials != null && Materials.Count > 15)
        {
            yield return new ValidationResult("Add up to 15 materials.", new[] { "materials" });
        }
        for (var i = 0; i < (Materials?.Count ?? 0); i++)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(Materials![i], new ValidationContext(Materials[i]), results, true);
            foreach (var r in results)
            {
                yield return new ValidationResult(r.ErrorMessage, r.MemberNames.Select(m => "materials[" + i + "]." + m.ToLowerInvariant()));
            }
        }
    }
}
