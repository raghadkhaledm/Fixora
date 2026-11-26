using Fixora.Data;
using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Collections;
using Fixora.Support.Middleware;
using Fixora.Support.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fixora.Controllers.TechArea;

[Authorize(AuthenticationSchemes = "technician")]
[EnsurePhoneVerified("technician")]
[EnsureTechnicianVerified]
public class EarningsController : Controller
{
    private readonly AppDbContext _db;

    public EarningsController(AppDbContext db)
    {
        _db = db;
    }

    private Technician Tech()
    {
        return Auth.Guard("technician").User<Technician>()!;
    }

    private IQueryable<Invoice> Query()
    {
        var id = Tech().Technician_ID;

        return _db.Invoices
            .Include(i => i.Booking).ThenInclude(b => b!.Items).ThenInclude(x => x.Service)
            .Include(i => i.Booking).ThenInclude(b => b!.Homeowner)
            .Where(i => i.Booking!.Technician_ID == id);
    }

    [HttpGet("/tech/earnings", Name = "tech.earnings")]
    public IActionResult Index()
    {
        var paid = Query().Where(i => i.Paid_At != null).OrderByDescending(i => i.Paid_At).ToList();

        var weeks = paid
            .GroupBy(i => i.Paid_At!.Value.StartOfWeek(DayOfWeek.Saturday).Format("Y-m-d"))
            .Select(g =>
            {
                var cash = g.Where(i => i.Payment_Method == "Cash").ToList();
                var online = g.Where(i => i.Payment_Method != "Cash").ToList();

                return new WeekSummary
                {
                    Start = DateTime.Parse(g.Key),
                    Jobs = g.Count(),
                    Earned = g.Sum(i => i.Labor_Cost + i.Material_Cost),
                    Owe = cash.Sum(i => i.Service_Commission_Fee + i.Express_Fee),
                    Kept = online.Sum(i => i.Service_Commission_Fee + i.Express_Fee),
                    Refunds = g.Count(i => i.Payment_Status == "Refunded" || i.Payment_Status == "Partially Refunded"),
                };
            })
            .ToList();

        var thisStart = Clock.Now().StartOfWeek(DayOfWeek.Saturday);
        var thisWeek = weeks.FirstOrDefault(w => w.Start.IsSameDay(thisStart));

        return FxView("tech.earnings", new
        {
            weeks,
            thisWeek,
            total = paid.Sum(i => i.Labor_Cost + i.Material_Cost),
            owe = paid.Where(i => i.Payment_Method == "Cash").Sum(i => i.Service_Commission_Fee + i.Express_Fee),
            unpaid = Query().Count(i => i.Payment_Status == "Unpaid"),
        });
    }

    [HttpGet("/tech/invoices", Name = "tech.invoices")]
    public IActionResult Invoices()
    {
        return FxView("tech.invoices", new { invoices = PaginatedList<Invoice>.Create(Query().OrderByDescending(i => i.Issued_At), 10) });
    }

    [HttpGet("/tech/reviews", Name = "tech.reviews")]
    public IActionResult Reviews()
    {
        var t = Tech();
        var reviews = _db.Reviews
            .Include(r => r.Homeowner)
            .Include(r => r.Booking).ThenInclude(b => b!.Items).ThenInclude(i => i.Service)
            .Where(r => r.Technician_ID == t.Technician_ID)
            .OrderByDescending(r => r.Created_At);

        return FxView("tech.reviews", new
        {
            reviews = PaginatedList<Review>.Create(reviews, 10),
            avg = t.Average_Rating,
            dist = new IntCountMap(_db.Reviews
                .Where(r => r.Technician_ID == t.Technician_ID)
                .GroupBy(r => r.Rating_Score)
                .Select(g => new { g.Key, N = g.Count() })
                .ToDictionary(x => x.Key, x => x.N)),
        });
    }
}
