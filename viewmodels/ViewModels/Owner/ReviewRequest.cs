using System.ComponentModel.DataAnnotations;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class ReviewRequest : FixoraAttributes
{
    [FromForm(Name = "rating_score")]
    [Display(Name = "Rating")]
    [Required(ErrorMessage = "Choose a rating.")]
    [Range(1, 5, ErrorMessage = "Choose a rating.")]
    public int? RatingScore { get; set; }

    [FromForm(Name = "comment")]
    [Display(Name = "Review")]
    [MaxLength(600, ErrorMessage = "Keep the review under 600 characters.")]
    public string? Comment { get; set; }
}
