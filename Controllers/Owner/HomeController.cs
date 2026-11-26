using Fixora.Data;
using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fixora.Controllers.OwnerArea;

[Authorize(AuthenticationSchemes = "homeowner")]
[EnsurePhoneVerified("homeowner")]
public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("/home", Name = "home")]
    public IActionResult Index()
    {
        var o = Auth.Guard("homeowner").User<HomeOwner>()!;
        var since = Clock.Now().AddDays(-30);

        return FxView("owner.home", new
        {
            o,
            active = _db.Bookings
                .Include(b => b.Items).ThenInclude(i => i.Service).ThenInclude(s => s!.Type)
                .Include(b => b.Technician)
                .Where(b => b.Home_owner_ID == o.Home_owner_ID && (b.Status == "Pending" || b.Status == "Accepted" || b.Status == "Rejected"))
                .OrderByDescending(b => b.Created_At)
                .ToList(),
            unpaid = _db.Invoices
                .Include(i => i.Booking).ThenInclude(b => b!.Items).ThenInclude(x => x.Service).ThenInclude(s => s!.Type)
                .Include(i => i.Booking).ThenInclude(b => b!.Technician)
                .Where(i => i.Booking!.Home_owner_ID == o.Home_owner_ID && i.Payment_Status == "Unpaid")
                .OrderByDescending(i => i.Issued_At)
                .ToList(),
            again = _db.Bookings
                .Include(b => b.Items).ThenInclude(i => i.Service).ThenInclude(s => s!.Type)
                .Include(b => b.Technician)
                .Include(b => b.Review)
                .Where(b => b.Home_owner_ID == o.Home_owner_ID && b.Status == "Completed" && b.Completed_At >= since)
                .OrderByDescending(b => b.Completed_At)
                .Take(3)
                .ToList(),
            types = _db.ServiceTypes
                .Include(t => t.Services.Where(s => s.Is_Active).OrderBy(s => s.Service_ID))
                .OrderBy(t => t.Service_Type_ID)
                .ToList(),
            completed = _db.Bookings.Count(b => b.Home_owner_ID == o.Home_owner_ID && b.Status == "Completed"),
        });
    }
}
