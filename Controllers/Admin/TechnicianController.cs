using Fixora.Controllers.TechArea;
using Fixora.Data;
using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Collections;
using Fixora.Support.Pagination;
using Fixora.Support.Services;
using Fixora.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fixora.Controllers.AdminArea;

[Authorize(AuthenticationSchemes = "admin")]
public class TechnicianController : Controller
{
    private readonly AppDbContext _db;
    private readonly SmsService _sms;

    public TechnicianController(AppDbContext db, SmsService sms)
    {
        _db = db;
        _sms = sms;
    }

    [HttpGet("/admin/technicians", Name = "admin.technicians")]
    public IActionResult Index()
    {
        var statusQuery = Query("status");
        var status = statusQuery is "Pending" or "Verified" or "Rejected" ? statusQuery : null;
        var q = _db.Technicians
            .Include(t => t.Services).ThenInclude(s => s.Type)
            .Where(t => !t.Email.EndsWith("@fixora.invalid"));
        if (status != null)
        {
            q = q.Where(t => t.Verification_Status == status);
        }
        var s = (Query("q") ?? "").Trim();
        if (s != "")
        {
            var normalized = Phone.Normalize(s);
            q = q.Where(w => EF.Functions.Like(w.First_Name, "%" + s + "%")
                || EF.Functions.Like(w.Last_Name, "%" + s + "%")
                || EF.Functions.Like(w.Email, "%" + s + "%")
                || EF.Functions.Like(w.Phone_Number, "%" + normalized + "%"));
        }

        var counts = new CountMap(_db.Technicians
            .Where(t => !t.Email.EndsWith("@fixora.invalid"))
            .GroupBy(t => t.Verification_Status)
            .Select(g => new { g.Key, N = g.Count() })
            .ToDictionary(x => x.Key, x => x.N));

        var ordered = q
            .OrderBy(t => t.Verification_Status == "Pending" ? 0 : (t.Verification_Status == "Rejected" ? 1 : 2))
            .ThenByDescending(t => t.Created_At);
        var techs = PaginatedList<Technician>.Create(ordered, 10);
        _db.WithCounts(techs.Items()).ToList();

        return FxView("admin.technicians.index", new { techs, status, counts });
    }

    [HttpGet("/admin/technicians/{technician:int}", Name = "admin.technicians.show")]
    public IActionResult Show(int technician)
    {
        var t = _db.Technicians.Include(x => x.Services).ThenInclude(s => s.Type).FirstOrDefault(x => x.Technician_ID == technician);
        if (t == null)
        {
            return NotFound();
        }
        _db.WithCounts(t);

        return FxView("admin.technicians.show", new
        {
            t,
            jobs = _db.Bookings
                .Include(b => b.Homeowner)
                .Include(b => b.Items).ThenInclude(i => i.Service)
                .Include(b => b.Invoice)
                .Where(b => b.Technician_ID == t.Technician_ID)
                .OrderByDescending(b => b.Created_At)
                .Take(8)
                .ToList(),
        });
    }

    [HttpPost("/admin/technicians/{technician:int}/verify", Name = "admin.technicians.verify")]
    public IActionResult Verify([FromForm] VerifyTechnicianRequest request, int technician)
    {
        var t = _db.Technicians.Find(technician);
        if (t == null)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        if (request.Decision == "Verified" && !t.Phone_Verified_At.HasValue)
        {
            Toast($"{t.Name} hasn't verified the phone number yet. Approve after the SMS check.", "bi-phone");

            return Back();
        }

        string msg;
        if (request.Decision == "Verified")
        {
            t.Verification_Status = "Verified";
            t.Verified_At = Clock.Now();
            t.Verification_Note = null;
            msg = $"{t.Name} is verified and now visible in " + string.Join(", ", t.Zones) + ".";
            _sms.SendQuiet(t.Phone_Number, "Fixora: Your profile was approved. Turn on \"Available\" to receive express jobs.");
        }
        else
        {
            t.Verification_Status = "Rejected";
            t.Verification_Note = request.VerificationNote!.Trim();
            t.Is_Available = false;
            msg = $"{t.Name} was rejected. The note was sent by SMS.";
            _sms.SendQuiet(t.Phone_Number, "Fixora: Your documents need another look: " + t.Verification_Note);
        }
        _db.SaveChanges();
        Toast(msg, "bi-person-check");

        return RedirectToRoute("admin.technicians.show", new { technician = t.Technician_ID });
    }

    [HttpDelete("/admin/technicians/{technician:int}", Name = "admin.technicians.destroy")]
    public IActionResult Destroy(int technician)
    {
        var t = _db.Technicians.Find(technician);
        if (t == null)
        {
            return NotFound();
        }

        var open = _db.Bookings.Any(b => b.Technician_ID == t.Technician_ID && (b.Status == "Pending" || b.Status == "Accepted"));
        var disputes = _db.DisputeTickets.Any(d => (d.Ticket_Status == "Open" || d.Ticket_Status == "Under Review") && d.Booking!.Technician_ID == t.Technician_ID);
        if (open || disputes)
        {
            Toast("This technician has open jobs or disputes. Close them first.", "bi-exclamation-circle");

            return Back();
        }

        var name = t.Name;
        ProfileController.AnonymiseTechnician(_db, t);
        Toast($"{name}'s account was deleted (anonymised).", "bi-trash3");

        return RedirectToRoute("admin.technicians");
    }

    [HttpGet("/admin/homeowners", Name = "admin.homeowners")]
    public IActionResult Homeowners()
    {
        var q = _db.HomeOwners.Where(h => !h.Email.EndsWith("@fixora.invalid"));
        var s = (Query("q") ?? "").Trim();
        if (s != "")
        {
            q = q.Where(w => EF.Functions.Like(w.First_Name, "%" + s + "%")
                || EF.Functions.Like(w.Last_Name, "%" + s + "%")
                || EF.Functions.Like(w.Email, "%" + s + "%"));
        }
        var owners = PaginatedList<HomeOwner>.Create(q.OrderByDescending(h => h.Created_At), 12);
        _db.WithCounts(owners.Items()).ToList();

        return FxView("admin.homeowners", new { owners });
    }
}
