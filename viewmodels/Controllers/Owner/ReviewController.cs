using Fixora.Data;
using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Middleware;
using Fixora.Support.Services;
using Fixora.Support.Views;
using Fixora.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.Controllers.OwnerArea;

[Authorize(AuthenticationSchemes = "homeowner")]
[EnsurePhoneVerified("homeowner")]
public class ReviewController : Controller
{
    private readonly AppDbContext _db;
    private readonly ReviewService _reviews;

    public ReviewController(AppDbContext db, ReviewService reviews)
    {
        _db = db;
        _reviews = reviews;
    }

    private Booking? Mine(int id)
    {
        var b = _db.Bookings.Find(id);
        if (b == null || b.Home_owner_ID != Auth.Guard("homeowner").Id() || b.Status != "Completed")
        {
            return null;
        }
        _db.Load(b, "technician", "review", "items.service.type", "invoice");

        return b;
    }

    [HttpGet("/bookings/{booking:int}/review", Name = "reviews.edit")]
    public IActionResult Edit(int booking)
    {
        var b = Mine(booking);
        if (b == null)
        {
            return NotFound();
        }

        var fallback = b.Review?.Rating_Score ?? (int.TryParse(Query("rating"), out var q) ? q : 0);
        var rating = int.TryParse(Blade.Old("rating_score"), out var old) ? old : fallback;

        return FxView("owner.review", new { b, rating });
    }

    [HttpPost("/bookings/{booking:int}/review", Name = "reviews.save")]
    public IActionResult Save([FromForm] ReviewRequest request, int booking)
    {
        var b = Mine(booking);
        if (b == null)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var r = b.Review;
        var isNew = r == null;
        if (r == null)
        {
            r = new Review
            {
                Booking_ID = b.Booking_ID,
                Home_owner_ID = b.Home_owner_ID,
                Technician_ID = b.Technician_ID,
                Created_At = Clock.Now(),
            };
            _db.Reviews.Add(r);
        }
        r.Rating_Score = request.RatingScore!.Value;
        r.Comment = !string.IsNullOrEmpty(request.Comment) ? request.Comment.Trim() : null;
        _db.SaveChanges();
        _reviews.Recompute(b.Technician_ID);
        Toast(isNew ? $"Thanks! Your review of {b.Technician!.First_Name} is live." : "Your review was updated.", "bi-star-fill");

        return RedirectToRoute("reviews.edit", new { booking = b.Booking_ID });
    }

    [HttpDelete("/bookings/{booking:int}/review", Name = "reviews.destroy")]
    public IActionResult Destroy(int booking)
    {
        var b = Mine(booking);
        if (b == null)
        {
            return NotFound();
        }
        if (b.Review != null)
        {
            _db.Reviews.Remove(b.Review);
            _db.SaveChanges();
            _reviews.Recompute(b.Technician_ID);
        }
        Toast("Your review was deleted.", "bi-trash3");

        return RedirectToRoute("bookings.show", new { booking = b.Booking_ID });
    }
}
