using Fixora.Data;
using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Http;
using Fixora.Support.Middleware;
using Fixora.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fixora.Controllers.TechArea;

[Authorize(AuthenticationSchemes = "technician")]
[EnsurePhoneVerified("technician")]
public class ProfileController : Controller
{
    private readonly AppDbContext _db;

    public ProfileController(AppDbContext db)
    {
        _db = db;
    }

    private Technician Tech()
    {
        return Auth.Guard("technician").User<Technician>()!;
    }

    [HttpGet("/tech/pending", Name = "tech.pending")]
    public IActionResult Pending()
    {
        var t = Tech();
        _db.Load(t, "services.type");
        if (t.Verification_Status == "Verified")
        {
            return RedirectToRoute("tech.jobs");
        }

        return FxView("tech.pending", new { t });
    }

    [HttpGet("/tech/profile", Name = "tech.profile")]
    public IActionResult Show()
    {
        var t = Tech();
        _db.Load(t, "services.type");
        var type = t.ServiceType();
        if (type != null)
        {
            _db.Entry(type).Collection(x => x.Services).Load();
        }

        return FxView("tech.profile", new { t, type });
    }

    [HttpPost("/tech/profile", Name = "tech.profile.update")]
    public IActionResult Update([FromForm] UpdateTechnicianRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var t = Tech();
        var phoneChanged = request.Phone != t.Phone_Number;
        t.First_Name = request.FirstName!.Trim();
        t.Last_Name = request.LastName!.Trim();
        t.Email = request.Email!;
        t.Phone_Number = request.Phone!;
        if (phoneChanged)
        {
            t.Phone_Verified_At = null;
            t.Is_Available = false;
        }
        _db.SaveChanges();

        if (phoneChanged)
        {
            Toast("Verify your new number to keep receiving jobs.", "bi-phone");

            return RedirectToRoute("verify.show");
        }
        Toast("Your details were saved.", "bi-check2-circle");

        return RedirectToRoute("tech.profile");
    }

    [HttpPost("/tech/profile/documents", Name = "tech.profile.documents")]
    public IActionResult Documents([FromForm] TechnicianDocumentsRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var t = Tech();
        var zones = request.Zones!.Distinct().ToList();
        var latLng = FixoraConfig.Current.Cities[t.City][zones[0]];
        var folder = "technicians/" + t.Technician_ID;
        if (request.NationalIdFile != null)
        {
            var old = t.National_ID_File;
            t.National_ID_File = Fx.SaveUpload(request.NationalIdFile, folder, "national-id");
            Fx.DeleteUpload(old);
        }
        if (request.LicenseFile != null)
        {
            var old = t.Vocational_License;
            t.Vocational_License = Fx.SaveUpload(request.LicenseFile, folder, "license");
            Fx.DeleteUpload(old);
        }
        t.National_ID = request.NationalId!;
        t.Coverage_Zone = string.Join(", ", zones);
        t.Hourly_Rate = request.HourlyRate!.Value;
        t.Latitude = latLng[0];
        t.Longitude = latLng[1];
        var resubmitted = false;
        if (t.Verification_Status == "Rejected")
        {
            t.Verification_Status = "Pending";
            t.Verification_Note = null;
            t.Is_Available = false;
            resubmitted = true;
        }
        _db.SaveChanges();
        _db.SyncServices(t, request.Services!);

        if (resubmitted)
        {
            Toast("New documents submitted. A Fixora admin will review them.", "bi-upload");

            return RedirectToRoute("tech.pending");
        }
        Toast("Services, zones and rate saved.", "bi-check2-circle");

        return RedirectToRoute("tech.profile");
    }

    [HttpPost("/tech/profile/password", Name = "tech.profile.password")]
    public IActionResult Password([FromForm] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var t = Tech();
        t.Password = Support.Hash.Make(request.Password!);
        _db.SaveChanges();
        Toast("Password changed.", "bi-shield-check");

        return RedirectToRoute("tech.profile");
    }

    [HttpPost("/tech/profile/delete", Name = "tech.profile.delete")]
    public IActionResult Destroy([FromForm] DeleteAccountRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var t = Tech();
        var open = _db.Bookings.Any(b => b.Technician_ID == t.Technician_ID && (b.Status == "Pending" || b.Status == "Accepted"));
        var disputes = _db.DisputeTickets.Any(d => (d.Ticket_Status == "Open" || d.Ticket_Status == "Under Review") && d.Booking!.Technician_ID == t.Technician_ID);
        if (open || disputes)
        {
            Toast("Finish or cancel your open jobs and disputes before deleting the account.", "bi-exclamation-circle");

            return Back();
        }

        AnonymiseTechnician(_db, t);
        Auth.Guard("technician").Logout();
        SessionStore.Invalidate();
        SessionStore.RegenerateToken();

        return RedirectToRoute("landing");
    }

    public static void AnonymiseTechnician(AppDbContext db, Technician t)
    {
        var id = t.Technician_ID;
        t.First_Name = "Deleted";
        t.Last_Name = "user";
        t.Email = "deleted-t" + id + "@fixora.invalid";
        t.Phone_Number = "050-" + Php.StrPadLeft(((900000 + id) / 10000 % 1000).ToString(), 3, "0") + "-" + Php.StrPadLeft(((900000 + id) % 10000).ToString(), 4, "0");
        t.Password = Support.Hash.Make(Php.Random(40));
        t.Is_Available = false;
        t.Verification_Status = "Rejected";
        t.Verification_Note = "Account deleted.";
        t.Verified_At = t.Verified_At;
        db.SaveChanges();
    }
}
