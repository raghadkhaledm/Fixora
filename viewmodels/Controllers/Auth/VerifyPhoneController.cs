using Fixora.Data;
using Fixora.Models;
using Fixora.Models.Concerns;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Middleware;
using Fixora.Support.Services;
using Fixora.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fixora.Controllers.AuthArea;

public class VerifyPhoneController : Controller
{
    private readonly AppDbContext _db;
    private readonly VerificationService _verify;

    public VerifyPhoneController(AppDbContext db, VerificationService verify)
    {
        _db = db;
        _verify = verify;
    }

    private (FixoraAccount?, string?) Who()
    {
        var u = Auth.Guard("homeowner").User();
        if (u != null)
        {
            return (u, "homeowner");
        }
        u = Auth.Guard("technician").User();
        if (u != null)
        {
            return (u, "technician");
        }

        return (null, null);
    }

    [Authorize(AuthenticationSchemes = "homeowner,technician")]
    [HttpGet("/verify-phone", Name = "verify.show")]
    public IActionResult Show()
    {
        var (u, g) = Who();
        if (u == null)
        {
            return RedirectToRoute("login");
        }
        if (u.PhoneVerifiedAt.HasValue)
        {
            return RedirectToRoute(g == "technician" ? "tech.jobs" : "home");
        }
        if (!_verify.Has(u, g!))
        {
            _verify.Send(u, g!);
        }

        return FxView("auth.verify-phone", new
        {
            user = u,
            guard = g,
            resendIn = _verify.ResendIn(u, g!),
            changing = Boolean("change"),
        });
    }

    [Authorize(AuthenticationSchemes = "homeowner,technician")]
    [HttpPost("/verify-phone", Name = "verify.check")]
    public IActionResult Verify([FromForm] CodeRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var (u, g) = Who();
        if (u == null)
        {
            return RedirectToRoute("login");
        }

        var r = _verify.Check(u, g!, request.Code!);
        if (r != "ok")
        {
            var msg = r switch
            {
                "expired" or "missing" => "This code has expired. Send a new one.",
                "locked" => "Too many wrong codes. Send a new one.",
                _ => "This code doesn't match. Check the SMS and try again.",
            };
            FlashErrors(("code", msg));

            return Back();
        }

        u.PhoneVerifiedAt = Clock.Now();
        _db.SaveChanges();

        return g == "technician" ? RedirectToRoute("tech.pending") : RedirectToRoute("welcome");
    }

    [Authorize(AuthenticationSchemes = "homeowner,technician")]
    [EnableRateLimiting("verify.resend")]
    [HttpPost("/verify-phone/resend", Name = "verify.resend")]
    public IActionResult Resend()
    {
        var (u, g) = Who();
        if (u == null)
        {
            return RedirectToRoute("login");
        }
        var wait = _verify.ResendIn(u, g!);
        if (wait > 0)
        {
            Toast($"You can send a new code in {wait} seconds.", "bi-stopwatch");

            return Back();
        }

        _verify.Send(u, g!);
        Toast("We sent a new code to " + u.PhoneFull + ".", "bi-chat-dots");

        return Back();
    }

    [Authorize(AuthenticationSchemes = "homeowner,technician")]
    [HttpPost("/verify-phone/change", Name = "verify.change")]
    public IActionResult ChangeNumber([FromForm] ChangePhoneRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var (u, g) = Who();
        if (u == null || u.PhoneVerifiedAt.HasValue)
        {
            return RedirectToRoute("login");
        }

        u.Phone_Number = request.Phone!;
        _db.SaveChanges();
        _verify.Send(u, g!);

        return RedirectToRoute("verify.show");
    }

    [Authorize(AuthenticationSchemes = "homeowner")]
    [EnsurePhoneVerified("homeowner")]
    [HttpGet("/welcome", Name = "welcome")]
    public IActionResult Welcome()
    {
        var u = Auth.Guard("homeowner").User<HomeOwner>();

        return FxView("auth.account-created", new { user = u });
    }
}
