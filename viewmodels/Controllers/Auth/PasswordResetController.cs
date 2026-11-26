using Fixora.Data;
using Fixora.Models.Concerns;
using Fixora.Support;
using Fixora.Support.Http;
using Fixora.Support.Middleware;
using Fixora.Support.Services;
using Fixora.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.Controllers.AuthArea;

public class PasswordResetController : Controller
{
    private readonly AppDbContext _db;
    private readonly AccountLookup _lookup;
    private readonly CodeService _codes;

    public PasswordResetController(AppDbContext db, AccountLookup lookup, CodeService codes)
    {
        _db = db;
        _lookup = lookup;
        _codes = codes;
    }

    private static bool IsAdmin()
    {
        return Current.RouteIs("admin.*");
    }

    [AllowAnonymous]
    [RedirectIfAuthenticated("homeowner", "technician")]
    [HttpGet("/forgot-password", Name = "password.request")]
    [HttpGet("/admin/forgot-password", Name = "admin.password.request")]
    public IActionResult Form()
    {
        return FxView("auth.forgot", new { admin = IsAdmin() });
    }

    [AllowAnonymous]
    [RedirectIfAuthenticated("homeowner", "technician")]
    [HttpPost("/forgot-password", Name = "password.email")]
    [HttpPost("/admin/forgot-password", Name = "admin.password.email")]
    public IActionResult Send([FromForm] ForgotPasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var admin = IsAdmin();
        var (u, guard) = _lookup.Find(request.Login!, admin);
        if (u == null)
        {
            FlashInput();
            FlashErrors(("login", "We couldn't find a Fixora account with this email or phone number."));

            return Back();
        }

        var key = "reset:" + guard + ":" + u.GetKey();
        var wait = _codes.ResendIn(key);
        var existing = SessionStore.Get<Dictionary<string, string>>("fx_reset");
        if (wait > 0 && existing != null && existing.TryGetValue("key", out var existingKey) && existingKey == key)
        {
            Toast($"You can send a new code in {wait} seconds.", "bi-stopwatch");

            return RedirectToRoute(admin ? "admin.password.reset" : "password.reset");
        }

        _codes.Issue(key, u.Phone_Number, "Your Fixora password reset code is {code}. It expires in 10 minutes.", 10);
        SessionStore.Put("fx_reset", new Dictionary<string, string>
        {
            ["key"] = key,
            ["guard"] = guard!,
            ["id"] = u.GetKey().ToString(),
            ["first"] = u.First_Name,
            ["phone"] = u.Phone_Number,
            ["login"] = request.Login!,
        });

        return RedirectToRoute(admin ? "admin.password.reset" : "password.reset");
    }

    [AllowAnonymous]
    [RedirectIfAuthenticated("homeowner", "technician")]
    [HttpGet("/reset-password", Name = "password.reset")]
    [HttpGet("/admin/reset-password", Name = "admin.password.reset")]
    public IActionResult ResetForm()
    {
        var r = SessionStore.Get<Dictionary<string, string>>("fx_reset");
        if (r == null)
        {
            return RedirectToRoute(IsAdmin() ? "admin.password.request" : "password.request");
        }

        return FxView("auth.reset", new { admin = IsAdmin(), reset = r, resendIn = _codes.ResendIn(r["key"]) });
    }

    [AllowAnonymous]
    [RedirectIfAuthenticated("homeowner", "technician")]
    [HttpPost("/reset-password", Name = "password.update")]
    [HttpPost("/admin/reset-password", Name = "admin.password.update")]
    public IActionResult Reset([FromForm] ResetPasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var admin = IsAdmin();
        var r = SessionStore.Get<Dictionary<string, string>>("fx_reset");
        if (r == null)
        {
            return RedirectToRoute(admin ? "admin.password.request" : "password.request");
        }

        var res = _codes.Check(r["key"], request.Code!);
        if (res != "ok")
        {
            var msg = res switch
            {
                "expired" or "missing" => "This code has expired. Send a new one.",
                "locked" => "Too many wrong codes. Send a new one.",
                _ => "This code doesn't match. Check the SMS and try again.",
            };
            FlashErrors(("code", msg));

            return Back();
        }

        var id = int.Parse(r["id"]);
        FixoraAccount u = r["guard"] switch
        {
            "homeowner" => _db.HomeOwners.First(h => h.Home_owner_ID == id),
            "technician" => _db.Technicians.First(t => t.Technician_ID == id),
            _ => _db.Admins.First(a => a.Admin_ID == id),
        };
        u.Password = Support.Hash.Make(request.Password!);
        _db.SaveChanges();
        SessionStore.Forget("fx_reset");
        Flash("old", new Dictionary<string, string> { ["login"] = r["login"] });
        Toast("Your password was changed. Log in with the new one.", "bi-shield-check");

        return RedirectToRoute(admin ? "admin.login" : "login");
    }
}
