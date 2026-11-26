using Fixora.Data;
using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Http;
using Fixora.Support.Middleware;
using Fixora.Support.Services;
using Fixora.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fixora.Controllers.AuthArea;

public class RegisterController : Controller
{
    private readonly AppDbContext _db;
    private readonly VerificationService _verify;

    public RegisterController(AppDbContext db, VerificationService verify)
    {
        _db = db;
        _verify = verify;
    }

    [AllowAnonymous]
    [RedirectIfAuthenticated("homeowner", "technician")]
    [HttpGet("/register", Name = "register")]
    public IActionResult ChooseType()
    {
        return FxView("auth.register-type");
    }

    [AllowAnonymous]
    [RedirectIfAuthenticated("homeowner", "technician")]
    [HttpPost("/register", Name = "register.type")]
    public IActionResult PostType([FromForm] AccountTypeRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        return RedirectToRoute(request.Type == "technician" ? "register.technician" : "register.homeowner");
    }

    [AllowAnonymous]
    [RedirectIfAuthenticated("homeowner", "technician")]
    [HttpGet("/register/homeowner", Name = "register.homeowner")]
    public IActionResult HomeownerForm()
    {
        return FxView("auth.register-homeowner");
    }

    [AllowAnonymous]
    [RedirectIfAuthenticated("homeowner", "technician")]
    [HttpPost("/register/homeowner", Name = "register.homeowner.store")]
    public IActionResult HomeownerStore([FromForm] RegisterHomeownerRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var latLng = FixoraConfig.Current.Cities[request.City!][request.District!];
        var o = new HomeOwner
        {
            First_Name = request.FirstName!.Trim(),
            Last_Name = request.LastName!.Trim(),
            Email = request.Email!,
            Phone_Number = request.Phone!,
            Password = Support.Hash.Make(request.Password!),
            Address = request.Address!.Trim(),
            City = request.City!,
            District = request.District!,
            Latitude = latLng[0],
            Longitude = latLng[1],
            Loyalty_Points = 0,
            Created_At = Clock.Now(),
        };
        _db.HomeOwners.Add(o);
        _db.SaveChanges();

        LoginController.SignIn(this, o, "homeowner");
        _verify.Send(o, "homeowner");

        return RedirectToRoute("verify.show");
    }

    [AllowAnonymous]
    [RedirectIfAuthenticated("homeowner", "technician")]
    [HttpGet("/register/technician", Name = "register.technician")]
    public IActionResult TechnicianForm()
    {
        return FxView("auth.register-technician-details", new
        {
            types = _db.ServiceTypes.OrderBy(t => t.Service_Type_ID).ToList(),
            draft = SessionStore.Get<Dictionary<string, string>>("tech_draft") ?? new Dictionary<string, string>(),
        });
    }

    [AllowAnonymous]
    [RedirectIfAuthenticated("homeowner", "technician")]
    [HttpPost("/register/technician", Name = "register.technician.details")]
    public IActionResult TechnicianDetails([FromForm] TechnicianDetailsRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        SessionStore.Put("tech_draft", new Dictionary<string, string>
        {
            ["first_name"] = request.FirstName!.Trim(),
            ["last_name"] = request.LastName!.Trim(),
            ["email"] = request.Email!,
            ["phone"] = request.Phone!,
            ["password"] = Support.Hash.Make(request.Password!),
            ["city"] = request.City!,
            ["trade"] = request.Trade!.Value.ToString(),
        });

        return RedirectToRoute("register.technician.documents");
    }

    [AllowAnonymous]
    [RedirectIfAuthenticated("homeowner", "technician")]
    [HttpGet("/register/technician/documents", Name = "register.technician.documents")]
    public IActionResult DocumentsForm()
    {
        var draft = SessionStore.Get<Dictionary<string, string>>("tech_draft");
        if (draft == null)
        {
            return RedirectToRoute("register.technician");
        }

        var tradeId = int.Parse(draft["trade"]);
        var type = _db.ServiceTypes.Include(t => t.Services).First(t => t.Service_Type_ID == tradeId);

        return FxView("auth.register-technician-documents", new { draft, type });
    }

    [AllowAnonymous]
    [RedirectIfAuthenticated("homeowner", "technician")]
    [HttpPost("/register/technician/documents", Name = "register.technician.documents.store")]
    public IActionResult DocumentsStore([FromForm] TechnicianDocumentsRequest request)
    {
        var draft = SessionStore.Get<Dictionary<string, string>>("tech_draft");
        if (draft == null)
        {
            return RedirectToRoute("register.technician");
        }

        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var zones = request.Zones!.Distinct().ToList();
        var latLng = FixoraConfig.Current.Cities[draft["city"]][zones[0]];
        Technician t;
        using (var tx = _db.Database.BeginTransaction())
        {
            t = new Technician
            {
                First_Name = draft["first_name"],
                Last_Name = draft["last_name"],
                Phone_Number = draft["phone"],
                Password = draft["password"],
                Email = draft["email"],
                National_ID = request.NationalId!,
                Vocational_License = "pending",
                National_ID_File = "pending",
                Verification_Status = "Pending",
                Hourly_Rate = request.HourlyRate!.Value,
                Coverage_Zone = string.Join(", ", zones),
                Is_Available = false,
                City = draft["city"],
                Latitude = latLng[0],
                Longitude = latLng[1],
                Created_At = Clock.Now(),
            };
            _db.Technicians.Add(t);
            _db.SaveChanges();
            _db.SyncServices(t, request.Services!);
            tx.Commit();
        }

        var folder = "technicians/" + t.Technician_ID;
        t.National_ID_File = Fx.SaveUpload(request.NationalIdFile!, folder, "national-id");
        t.Vocational_License = Fx.SaveUpload(request.LicenseFile!, folder, "license");
        _db.SaveChanges();
        SessionStore.Forget("tech_draft");

        LoginController.SignIn(this, t, "technician");
        _verify.Send(t, "technician");

        return RedirectToRoute("verify.show");
    }
}
