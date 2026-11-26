using Fixora.Data;
using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Http;
using Fixora.Support.Middleware;
using Fixora.Support.Services;
using Fixora.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fixora.Controllers.TechArea;

[Authorize(AuthenticationSchemes = "technician")]
[EnsurePhoneVerified("technician")]
[EnsureTechnicianVerified]
public class CollectController : Controller
{
    private readonly AppDbContext _db;
    private readonly CashOtpService _otp;

    public CollectController(AppDbContext db, CashOtpService otp)
    {
        _db = db;
        _otp = otp;
    }

    private Booking? Mine(int id)
    {
        var b = _db.Bookings.Find(id);
        if (b == null || b.Technician_ID != Auth.Guard("technician").Id())
        {
            return null;
        }
        _db.Load(b, "items.service.type", "homeowner", "invoice", "technician");

        return b;
    }

    [HttpGet("/tech/jobs/{booking:int}/collect", Name = "tech.collect")]
    public IActionResult Show(int booking)
    {
        var b = Mine(booking);
        if (b == null)
        {
            return NotFound();
        }

        var inv = b.Invoice;
        if (inv == null)
        {
            return RedirectToRoute("tech.job", new { booking = b.Booking_ID });
        }
        if (inv.IsPaid())
        {
            var w = Week(b);

            return FxView("tech.collect-done", new { b, inv, earn = w.earn, fees = w.fees, weekJobs = w.weekJobs, weekEarned = w.weekEarned });
        }

        return FxView("tech.collect", new
        {
            b,
            inv,
            state = SessionStore.Flashed<string>("collect_state"),
            left = SessionStore.Flashed<int?>("collect_left"),
        });
    }

    [HttpPost("/tech/jobs/{booking:int}/collect", Name = "tech.collect.post")]
    public IActionResult Confirm([FromForm] CodeRequest request, int booking)
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

        var inv = b.Invoice;
        if (inv == null)
        {
            return NotFound();
        }

        int? left = null;
        var r = _otp.Confirm(inv, request.Code!, ref left);
        if (r == "ok")
        {
            Toast($"Cash received · {inv.Ref} is Paid.", "bi-check2-circle");

            return RedirectToRoute("tech.collect", new { booking = b.Booking_ID });
        }

        var msg = r switch
        {
            "wrong" => $"This code doesn't match. {left} " + (left == 1 ? "attempt" : "attempts") + " left before the code is locked.",
            "none" => "The homeowner has not chosen cash yet. Ask them to choose \"Pay in cash\" in the app.",
            _ => "This code is locked. Ask the homeowner to send a new code.",
        };
        FlashInput();
        Flash("collect_state", r);
        Flash("collect_left", left);
        FlashErrors(("code", msg));

        return Back();
    }

    [HttpPost("/tech/jobs/{booking:int}/collect/new", Name = "tech.collect.new")]
    public IActionResult NewCode(int booking)
    {
        var b = Mine(booking);
        var inv = b?.Invoice;
        if (b == null || inv == null || inv.IsPaid() || inv.Payment_Method != "Cash")
        {
            return NotFound();
        }

        _otp.Issue(inv, true);
        Toast($"A new code was sent to {b.Homeowner!.First_Name} by SMS.", "bi-chat-dots");

        return RedirectToRoute("tech.collect", new { booking = b.Booking_ID });
    }

    private (double earn, double fees, int weekJobs, double weekEarned) Week(Booking b)
    {
        var inv = b.Invoice!;
        var start = Clock.Now().StartOfWeek(DayOfWeek.Saturday);
        var paid = _db.Invoices
            .Where(i => i.Booking!.Technician_ID == b.Technician_ID && i.Paid_At != null && i.Paid_At >= start)
            .ToList();

        return (
            inv.Labor_Cost + inv.Material_Cost,
            inv.Service_Commission_Fee + inv.Express_Fee,
            paid.Count,
            paid.Sum(i => i.Labor_Cost + i.Material_Cost));
    }
}
