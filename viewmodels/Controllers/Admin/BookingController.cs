using System.Text.RegularExpressions;
using Fixora.Data;
using Fixora.Models;
using Fixora.Support.Collections;
using Fixora.Support.Pagination;
using Fixora.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fixora.Controllers.AdminArea;

[Authorize(AuthenticationSchemes = "admin")]
public class BookingController : Controller
{
    private readonly AppDbContext _db;

    public BookingController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("/admin/bookings", Name = "admin.bookings")]
    public IActionResult Index()
    {
        var statusQuery = Query("status");
        var typeQuery = Query("type");
        var status = statusQuery is "Pending" or "Accepted" or "Rejected" or "Cancelled" or "Completed" ? statusQuery : null;
        var type = typeQuery is "Express" or "Scheduled" ? typeQuery : null;

        var q = _db.Bookings
            .Include(b => b.Homeowner)
            .Include(b => b.Technician)
            .Include(b => b.Items).ThenInclude(i => i.Service).ThenInclude(s => s!.Type)
            .Include(b => b.Invoice)
            .AsQueryable();
        if (status != null)
        {
            q = q.Where(b => b.Status == status);
        }
        if (type != null)
        {
            q = q.Where(b => b.Booking_Type == type);
        }
        var s = (Query("q") ?? "").Trim();
        if (s != "")
        {
            var digits = Regex.Replace(Regex.Replace(s, @"^FX-\d{2}-", "", RegexOptions.IgnoreCase), @"\D", "");
            var id = int.TryParse(digits, out var n) ? n : 0;
            q = q.Where(b => (id != 0 && b.Booking_ID == id)
                || EF.Functions.Like(b.Homeowner!.First_Name, "%" + s + "%")
                || EF.Functions.Like(b.Homeowner!.Last_Name, "%" + s + "%")
                || EF.Functions.Like(b.Technician!.First_Name, "%" + s + "%")
                || EF.Functions.Like(b.Technician!.Last_Name, "%" + s + "%"));
        }

        var counts = new CountMap(_db.Bookings.GroupBy(b => b.Status).Select(g => new { g.Key, N = g.Count() }).ToDictionary(x => x.Key, x => x.N));

        return FxView("admin.bookings.index", new
        {
            bookings = PaginatedList<Booking>.Create(q.OrderByDescending(b => b.Created_At), 12),
            status,
            type,
            counts,
        });
    }

    [HttpGet("/admin/bookings/{booking:int}", Name = "admin.bookings.show")]
    public IActionResult Show(int booking)
    {
        var b = _db.Bookings.Find(booking);
        if (b == null)
        {
            return NotFound();
        }
        _db.Load(b, "homeowner", "technician", "items.service.type", "invoice", "review", "dispute");

        return FxView("admin.bookings.show", new { b });
    }

    [HttpPost("/admin/bookings/{booking:int}/cancel", Name = "admin.bookings.cancel")]
    public IActionResult Cancel([FromForm] ReasonRequest request, int booking, [FromServices] Support.Services.BookingService bookings)
    {
        var b = _db.Bookings.Find(booking);
        if (b == null)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        if (b.Status == "Completed" || b.Status == "Cancelled")
        {
            Toast("Completed or cancelled bookings cannot be cancelled.", "bi-exclamation-circle");

            return Back();
        }

        _db.Load(b, "technician", "homeowner");
        bookings.Cancel(b, request.CancelReason!.Trim(), "admin");
        Toast($"{b.Ref} was cancelled. Both sides were notified.", "bi-calendar-x");

        return RedirectToRoute("admin.bookings.show", new { booking = b.Booking_ID });
    }
}
