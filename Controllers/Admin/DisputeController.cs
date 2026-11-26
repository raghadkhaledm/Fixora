using Fixora.Data;
using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Collections;
using Fixora.Support.Pagination;
using Fixora.Support.Services;
using Fixora.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fixora.Controllers.AdminArea;

[Authorize(AuthenticationSchemes = "admin")]
public class DisputeController : Controller
{
    private readonly AppDbContext _db;
    private readonly DisputeService _disputes;

    public DisputeController(AppDbContext db, DisputeService disputes)
    {
        _db = db;
        _disputes = disputes;
    }

    [HttpGet("/admin/disputes", Name = "admin.disputes")]
    public IActionResult Index()
    {
        var statusQuery = Query("status");
        var status = statusQuery is "Open" or "Under Review" or "Resolved" or "Rejected" ? statusQuery : null;
        var q = _db.DisputeTickets
            .Include(d => d.Booking).ThenInclude(b => b!.Homeowner)
            .Include(d => d.Booking).ThenInclude(b => b!.Technician)
            .Include(d => d.Booking).ThenInclude(b => b!.Invoice)
            .Include(d => d.Admin)
            .AsQueryable();
        if (status != null)
        {
            q = q.Where(d => d.Ticket_Status == status);
        }
        var counts = new CountMap(_db.DisputeTickets.GroupBy(d => d.Ticket_Status).Select(g => new { g.Key, N = g.Count() }).ToDictionary(x => x.Key, x => x.N));
        var ordered = q
            .OrderBy(d => d.Ticket_Status == "Open" ? 0 : (d.Ticket_Status == "Under Review" ? 1 : 2))
            .ThenByDescending(d => d.Created_At);

        return FxView("admin.disputes.index", new
        {
            tickets = PaginatedList<DisputeTicket>.Create(ordered, 12),
            status,
            counts,
        });
    }

    [HttpGet("/admin/disputes/{ticket:int}", Name = "admin.disputes.show")]
    public IActionResult Show(int ticket)
    {
        var t = _db.DisputeTickets.Find(ticket);
        if (t == null)
        {
            return NotFound();
        }
        _db.Load(t, "booking.homeowner", "booking.technician", "booking.invoice", "booking.items.service", "admin");
        var earned = _db.LoyaltyTransactions
            .Where(l => l.Booking_ID == t.Booking_ID && l.Transaction_Type == "Earned")
            .Sum(l => (int?)l.Points_Earned) ?? 0;

        return FxView("admin.disputes.show", new { d = t, earned });
    }

    [HttpPost("/admin/disputes/{ticket:int}/take", Name = "admin.disputes.take")]
    public IActionResult Take(int ticket)
    {
        var t = _db.DisputeTickets.Find(ticket);
        if (t == null)
        {
            return NotFound();
        }
        if (t.Ticket_Status != "Open")
        {
            Toast($"{t.Ref} is already {t.Ticket_Status}.", "bi-exclamation-circle");

            return Back();
        }

        _disputes.Take(t, Auth.Guard("admin").User<Admin>()!);
        Toast($"You took {t.Ref}. It is now Under Review.", "bi-person-check");

        return Back();
    }

    [HttpPost("/admin/disputes/{ticket:int}/decide", Name = "admin.disputes.decide")]
    public IActionResult Decide([FromForm] DecideDisputeRequest request, int ticket)
    {
        var t = _db.DisputeTickets.Find(ticket);
        if (t == null)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }
        if (t.IsClosed())
        {
            Toast($"{t.Ref} is already {t.Ticket_Status}.", "bi-exclamation-circle");

            return Back();
        }

        var outcome = _disputes.Decide(t, Auth.Guard("admin").User<Admin>()!, request.Status!, request.RefundAmount ?? 0.0, request.ResolutionNote!.Trim());
        string msg;
        if (request.Status == "Resolved")
        {
            msg = $"{t.Ref} resolved"
                + (outcome.Refund > 0
                    ? " · SAR " + Php.NumberFormat(outcome.Refund, 2) + " refunded" + (outcome.Reversed > 0 ? " · " + outcome.Reversed + " points reversed" : "")
                    : " without a refund")
                + ".";
        }
        else
        {
            msg = $"{t.Ref} rejected.";
        }
        Toast(msg, "bi-flag");

        return RedirectToRoute("admin.disputes.show", new { ticket = t.Ticket_ID });
    }
}
