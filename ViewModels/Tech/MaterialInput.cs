using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class MaterialInput
{
    [FromForm(Name = "name")]
    [Display(Name = "Material")]
    [Required(ErrorMessage = "Name the material (2–80 characters).")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Name the material (2–80 characters).")]
    public string? Name { get; set; }

    [FromForm(Name = "qty")]
    [Display(Name = "Quantity")]
    [Required(ErrorMessage = "Quantity 1–20.")]
    [Range(1, 20, ErrorMessage = "Quantity 1–20.")]
    public int? Qty { get; set; }

    [FromForm(Name = "amount")]
    [Display(Name = "Amount")]
    [Required(ErrorMessage = "Amount SAR 0.50–5,000.")]
    [Range(0.5, 5000, ErrorMessage = "Amount SAR 0.50–5,000.")]
    public double? Amount { get; set; }
}
