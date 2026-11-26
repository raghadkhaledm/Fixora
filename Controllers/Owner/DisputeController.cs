using Fixora.Data;
using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Middleware;
using Fixora.Support.Services;
using Fixora.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.Controllers.OwnerArea;

[Authorize(AuthenticationSchemes = "homeowner")]
[EnsurePhoneVerified("homeowner")]
public class DisputeController : Controller
{
    private readonly AppDbContext _db;
    private readonly DisputeService _disputes;

    public DisputeController(AppDbContext db, DisputeService disputes)
    {
        _db = db;
        _disputes = disputes;
    }

    [HttpPost("/invoices/{invoice:int}/dispute", Name = "invoices.dispute")]
    public IActionResult Store([FromForm] DisputeRequest request, int invoice)
    {
        var inv = _db.Invoices.Find(invoice);
        if (inv == null)
        {
            return NotFound();
        }
        _db.Load(inv, "booking");
        if (inv.Booking!.Home_owner_ID != Auth.Guard("homeowner").Id())
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        if (!_disputes.CanOpen(inv))
        {
            Toast("A report can be opened once per booking, within " + FixoraConfig.Current.DisputeDays + " days of payment.", "bi-exclamation-circle");

            return Back();
        }

        var d = new DisputeTicket
        {
            Booking_ID = inv.Booking_ID,
            Issue_Description = request.IssueDescription!.Trim(),
            Ticket_Status = "Open",
            Refund_Amount = request.RefundAmount.HasValue ? Php.Round(request.RefundAmount.Value, 2) : null,
            Created_At = Clock.Now(),
            Problem_Type = request.ProblemType!,
        };
        _db.DisputeTickets.Add(d);
        _db.SaveChanges();
        if (request.Photo != null)
        {
            d.Photo_Path = Fx.SaveUpload(request.Photo, "disputes/" + d.Ticket_ID);
            _db.SaveChanges();
        }
        Toast($"Ticket {d.Ref} opened. A Fixora admin reviews it within 24 hours.", "bi-flag");

        return RedirectToRoute("disputes.show", new { ticket = d.Ticket_ID });
    }

    [HttpGet("/disputes/{ticket:int}", Name = "disputes.show")]
    public IActionResult Show(int ticket)
    {
        var t = _db.DisputeTickets.Find(ticket);
        if (t == null)
        {
            return NotFound();
        }
        _db.Load(t, "booking.invoice", "booking.technician", "booking.items.service", "admin");
        if (t.Booking!.Home_owner_ID != Auth.Guard("homeowner").Id())
        {
            return NotFound();
        }

        return FxView("owner.disputes.show", new { d = t });
    }
}
