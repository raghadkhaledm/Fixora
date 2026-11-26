using Fixora.Data;
using Fixora.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fixora.Controllers.AdminArea;

[Authorize(AuthenticationSchemes = "admin")]
public class DashboardController : Controller
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("/admin", Name = "admin.dashboard")]
    public IActionResult Index()
    {
        var now = Clock.Now();
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var paidMonth = _db.Invoices.Where(i => i.Paid_At != null && i.Paid_At >= monthStart).ToList();

        return FxView("admin.dashboard", new
        {
            kpi = new
            {
                today = _db.Bookings.Count(b => b.Created_At >= now.Date),
                active = _db.Technicians.Count(t => t.Verification_Status == "Verified" && t.Is_Available),
                verified = _db.Technicians.Count(t => t.Verification_Status == "Verified"),
                pending = _db.Technicians.Count(t => t.Verification_Status == "Pending"),
                disputes = _db.DisputeTickets.Count(d => d.Ticket_Status == "Open" || d.Ticket_Status == "Under Review"),
                revenue = paidMonth.Sum(i => i.Service_Commission_Fee + i.Express_Fee),
                gmv = paidMonth.Sum(i => i.Total_Amount),
            },
            pendingTechs = _db.Technicians
                .Include(t => t.Services).ThenInclude(s => s.Type)
                .Where(t => t.Verification_Status == "Pending")
                .OrderBy(t => t.Created_At)
                .Take(5)
                .ToList(),
            disputes = _db.DisputeTickets
                .Include(d => d.Booking).ThenInclude(b => b!.Homeowner)
                .Include(d => d.Booking).ThenInclude(b => b!.Technician)
                .Where(d => d.Ticket_Status == "Open" || d.Ticket_Status == "Under Review")
                .OrderBy(d => d.Created_At)
                .Take(5)
                .ToList(),
            recent = _db.Bookings
                .Include(b => b.Homeowner)
                .Include(b => b.Technician)
                .Include(b => b.Items).ThenInclude(i => i.Service)
                .OrderByDescending(b => b.Created_At)
                .Take(6)
                .ToList(),
        });
    }
}
