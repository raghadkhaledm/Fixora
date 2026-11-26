using Fixora.Data;
using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fixora.Controllers;

public class LandingController : Controller
{
    private readonly AppDbContext _db;

    public LandingController(AppDbContext db)
    {
        _db = db;
    }

    [AllowAnonymous]
    [HttpGet("/", Name = "landing")]
    public IActionResult Index()
    {
        var types = _db.ServiceTypes
            .Include(t => t.Services.Where(s => s.Is_Active).OrderBy(s => s.Service_ID))
            .OrderBy(t => t.Service_Type_ID)
            .ToList();

        var tech = _db.Technicians
            .Include(t => t.Services).ThenInclude(s => s.Type)
            .Where(t => t.Verification_Status == "Verified" && t.Is_Available)
            .Select(t => new
            {
                Tech = t,
                ReviewsCount = t.Reviews.Count(),
                JobsCount = t.Bookings.Count(b => b.Status == "Completed"),
            })
            .OrderByDescending(x => x.Tech.Average_Rating)
            .ThenByDescending(x => x.ReviewsCount)
            .ThenByDescending(x => x.JobsCount)
            .Select(x => x.Tech)
            .FirstOrDefault();

        if (tech != null)
        {
            _db.WithCounts(tech);
        }

        var arrived = _db.Bookings
            .Where(b => b.Booking_Type == "Express" && b.Arrived_At != null)
            .ToList()
            .Select(b => b.Created_At.DiffInMinutes(b.Arrived_At!.Value))
            .ToList();
        var expressAvg = arrived.Count > 0 && arrived.Average() != 0 ? (int)Php.Round(arrived.Average()) : 35;

        return FxView("landing", new
        {
            types,
            tech,
            stats = new
            {
                technicians = _db.Technicians.Count(t => t.Verification_Status == "Verified"),
                jobs = _db.Invoices.Count(),
                rating = Php.Round(_db.Reviews.Average(r => (double?)r.Rating_Score) ?? 0, 1),
            },
            owner = Auth.Guard("homeowner").User<HomeOwner>(),
            expressAvg,
        });
    }
}
