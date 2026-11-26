using System.Globalization;
using Fixora.Data;
using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Collections;
using Fixora.Support.Middleware;
using Fixora.Support.Pagination;
using Fixora.Support.Services;
using Fixora.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fixora.Controllers.TechArea;

[Authorize(AuthenticationSchemes = "technician")]
[EnsurePhoneVerified("technician")]
[EnsureTechnicianVerified]
public class JobController : Controller
{
    private readonly AppDbContext _db;
    private readonly Support.Services.BookingService _bookings;

    public JobController(AppDbContext db, Support.Services.BookingService bookings)
    {
        _db = db;
        _bookings = bookings;
    }

    private Technician Tech()
    {
        return Auth.Guard("technician").User<Technician>()!;
    }

    private Booking? Mine(int id)
    {
        var b = _db.Bookings.Find(id);
        if (b == null || b.Technician_ID != Tech().Technician_ID)
        {
            return null;
        }
        _db.Load(b, "items.service.type", "homeowner", "invoice", "review");

        return b;
    }

    [HttpPost("/tech/availability", Name = "tech.availability")]
    public IActionResult Availability()
    {
        var t = Tech();
        t.Is_Available = Boolean("available");
        _db.SaveChanges();
        Toast(
            t.Is_Available ? "You are available for express requests." : "You are offline. Express requests are paused.",
            t.Is_Available ? "bi-toggle-on" : "bi-toggle-off");

        return Back();
    }

    private static IQueryable<Booking> TabFilter(string tab, IQueryable<Booking> q)
    {
        return tab switch
        {
            "upcoming" => q.Where(b => b.Status == "Accepted" && b.Job_Progress_Status == "Accepted"),
            "progress" => q.Where(b => b.Status == "Accepted" && (b.Job_Progress_Status == "On the way" || b.Job_Progress_Status == "In progress")),
            "completed" => q.Where(b => b.Status == "Completed"),
            _ => q.Where(b => b.Status == "Pending"),
        };
    }

    [HttpGet("/tech/jobs", Name = "tech.jobs")]
    public IActionResult Index()
    {
        var t = Tech();
        var tabs = new Dictionary<string, string[]>
        {
            ["new"] = new[] { "New requests" },
            ["upcoming"] = new[] { "Upcoming" },
            ["progress"] = new[] { "In progress" },
            ["completed"] = new[] { "Completed" },
        };
        var tabQuery = Query("tab");
        var tab = tabQuery != null && tabs.ContainsKey(tabQuery) ? tabQuery : "new";
        var baseQuery = _db.Bookings.Where(b => b.Technician_ID == t.Technician_ID);
        var counts = new CountMap();
        foreach (var k in tabs.Keys)
        {
            counts[k] = TabFilter(k, baseQuery).Count();
        }

        var q = TabFilter(tab, baseQuery
            .Include(b => b.Items).ThenInclude(i => i.Service).ThenInclude(s => s!.Type)
            .Include(b => b.Homeowner)
            .Include(b => b.Invoice));
        q = tab == "completed"
            ? q.OrderByDescending(b => b.Completed_At)
            : (tab == "upcoming" ? q.OrderBy(b => b.Scheduled_Date_Time) : q.OrderByDescending(b => b.Created_At));

        return FxView("tech.jobs.index", new
        {
            tabs,
            tab,
            counts,
            jobs = PaginatedList<Booking>.Create(q, 8),
        });
    }

    [HttpGet("/tech/jobs/{booking:int}", Name = "tech.job")]
    public IActionResult Show(int booking)
    {
        var b = Mine(booking);
        if (b == null)
        {
            return NotFound();
        }

        return FxView("tech.jobs.show", new { b, next = _bookings.NextStep(b) });
    }

    [HttpPost("/tech/jobs/{booking:int}/accept", Name = "tech.accept")]
    public IActionResult Accept(int booking)
    {
        var b = Mine(booking);
        if (b == null)
        {
            return NotFound();
        }
        if (b.Status != "Pending")
        {
            Toast("This request is no longer waiting for you.", "bi-exclamation-circle");

            return Back();
        }

        if (!b.IsExpress())
        {
            var from = b.Scheduled_Date_Time.AddMinutes(-119);
            var to = b.Scheduled_Date_Time.AddMinutes(119);
            var clash = _db.Bookings.Any(x => x.Technician_ID == b.Technician_ID && x.Status == "Accepted" && x.Booking_ID != b.Booking_ID
                && x.Scheduled_Date_Time >= from && x.Scheduled_Date_Time <= to);
            if (clash)
            {
                Toast("You already accepted another job in this slot. Decline this one instead.", "bi-calendar-x");

                return Back();
            }
        }

        _bookings.Accept(b);
        Toast($"You accepted {b.Ref}. {b.Homeowner!.First_Name} has been notified.", "bi-check2-circle");

        return RedirectToRoute("tech.job", new { booking = b.Booking_ID });
    }

    [HttpPost("/tech/jobs/{booking:int}/decline", Name = "tech.decline")]
    public IActionResult Decline([FromForm] ReasonRequest request, int booking)
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
        if (b.Status != "Pending")
        {
            Toast("This request is no longer waiting for you.", "bi-exclamation-circle");

            return Back();
        }

        _bookings.Decline(b, request.DeclineReason!.Trim());
        Toast($"You declined {b.Ref}. The homeowner can send it to another technician.", "bi-x-circle");

        return RedirectToRoute("tech.jobs");
    }

    [HttpPost("/tech/jobs/{booking:int}/price", Name = "tech.price")]
    public IActionResult Price([FromForm] RoomPriceRequest request, int booking)
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

        if (!_bookings.SetRoomPrice(b, request.AgreedPrice!.Value))
        {
            Toast("The room price can only change before you accept.", "bi-exclamation-circle");

            return Back();
        }
        Toast("Agreed price per room saved.", "bi-check2-circle");

        return Back();
    }

    [HttpPost("/tech/jobs/{booking:int}/progress", Name = "tech.progress")]
    public IActionResult Progress(int booking)
    {
        var b = Mine(booking);
        if (b == null)
        {
            return NotFound();
        }

        var step = Input("step") ?? "";
        if (!_bookings.Progress(b, step))
        {
            Toast("That step is not available right now.", "bi-exclamation-circle");

            return Back();
        }

        var messages = new Dictionary<string, string>
        {
            ["on_the_way"] = "You are on the way. The homeowner has been notified.",
            ["arrived"] = "Arrival recorded.",
            ["start"] = "Job started.",
            ["complete"] = "Job completed. Create the invoice now.",
        };
        var msg = messages[step];
        if (step == "complete")
        {
            Toast(msg, "bi-receipt");

            return RedirectToRoute("tech.invoice.create", new { booking = b.Booking_ID });
        }
        Toast(msg, "bi-check2-circle");

        return Back();
    }

    [HttpGet("/tech/schedule", Name = "tech.schedule")]
    public IActionResult Schedule([FromServices] SlotService slots)
    {
        var t = Tech();
        var dateInput = Query("date");
        var day = !string.IsNullOrEmpty(dateInput) && DateTime.TryParse(dateInput, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.Date
            : Clock.Now().Date;
        var month = new DateTime(day.Year, day.Month, 1);
        var from = month.AddDays(-7);
        var to = month.AddMonths(1).AddSeconds(-1).AddDays(7);
        var jobs = _db.Bookings
            .Include(b => b.Items).ThenInclude(i => i.Service)
            .Include(b => b.Homeowner)
            .Where(b => b.Technician_ID == t.Technician_ID && (b.Status == "Pending" || b.Status == "Accepted" || b.Status == "Completed"))
            .Where(b => b.Scheduled_Date_Time >= from && b.Scheduled_Date_Time <= to)
            .OrderBy(b => b.Scheduled_Date_Time)
            .ToList();
        var byDay = jobs.GroupBy(b => b.Scheduled_Date_Time.Format("Y-m-d")).ToDictionary(g => g.Key, g => g.ToList());
        var key = day.Format("Y-m-d");

        return FxView("tech.schedule", new
        {
            day,
            month,
            byDay,
            slots,
            dayJobs = byDay.TryGetValue(key, out var list) ? list : new List<Booking>(),
        });
    }
}
