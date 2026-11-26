using Fixora.Data;
using Fixora.Models;
using Fixora.Support.Pagination;
using Fixora.Support.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fixora.Controllers.AdminArea;

[Authorize(AuthenticationSchemes = "admin")]
public class ReviewController : Controller
{
    private readonly AppDbContext _db;
    private readonly ReviewService _reviews;

    public ReviewController(AppDbContext db, ReviewService reviews)
    {
        _db = db;
        _reviews = reviews;
    }

    [HttpGet("/admin/reviews", Name = "admin.reviews")]
    public IActionResult Index()
    {
        var q = _db.Reviews
            .Include(r => r.Homeowner)
            .Include(r => r.Technician)
            .Include(r => r.Booking).ThenInclude(b => b!.Items).ThenInclude(i => i.Service)
            .AsQueryable();
        var rating = Query("rating");
        if (!string.IsNullOrEmpty(rating) && rating != "0")
        {
            var score = int.TryParse(rating, out var n) ? n : 0;
            q = q.Where(r => r.Rating_Score == score);
        }

        return FxView("admin.reviews", new { reviews = PaginatedList<Review>.Create(q.OrderByDescending(r => r.Created_At), 12) });
    }

    [HttpDelete("/admin/reviews/{review:int}", Name = "admin.reviews.destroy")]
    public IActionResult Destroy(int review)
    {
        var r = _db.Reviews.Find(review);
        if (r == null)
        {
            return NotFound();
        }

        var tid = r.Technician_ID;
        _db.Reviews.Remove(r);
        _db.SaveChanges();
        _reviews.Recompute(tid);
        Toast("Review deleted. The technician's rating was recalculated.", "bi-trash3");

        return Back();
    }
}
