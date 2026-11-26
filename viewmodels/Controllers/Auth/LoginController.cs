using Fixora.Data;
using Fixora.Models;
using Fixora.Models.Concerns;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Http;
using Fixora.Support.Middleware;
using Fixora.Support.Services;
using Fixora.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.Controllers.AuthArea;

public class LoginController : Controller
{
    private readonly AppDbContext _db;
    private readonly AccountLookup _lookup;
    private readonly CodeService _codes;

    public LoginController(AppDbContext db, AccountLookup lookup, CodeService codes)
    {
        _db = db;
        _lookup = lookup;
        _codes = codes;
    }

    [AllowAnonymous]
    [RedirectIfAuthenticated("homeowner", "technician")]
    [HttpGet("/login", Name = "login")]
    public IActionResult Show()
    {
        return FxView("auth.login");
    }

    [AllowAnonymous]
    [RedirectIfAuthenticated("homeowner", "technician")]
    [HttpPost("/login", Name = "login.post")]
    public IActionResult Login([FromForm] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var max = FixoraConfig.Current.LoginMaxAttempts;
        var key = _lookup.ThrottleKey(request.Login!, Ip());
        if (RateLimiter.TooManyAttempts(key, max))
        {
            return Paused(key);
        }

        var (user, guard) = _lookup.Find(request.Login!);
        if (user == null || !Support.Hash.Check(request.Password, user.Password))
        {
            RateLimiter.Hit(key, 60 * FixoraConfig.Current.LoginPauseMinutes);
            if (RateLimiter.TooManyAttempts(key, max))
            {
                return Paused(key);
            }
            var left = max - RateLimiter.Attempts(key);
            FlashInput("login", "keep");
            Flash("login_failed", left);
            FlashErrors(("password", "Password doesn't match this account."));

            return Back();
        }

        RateLimiter.Clear(key);

        return SignIn(this, user, guard!, Boolean("keep"));
    }

    private IActionResult Paused(string key)
    {
        var m = Math.Max(1, (int)Math.Ceiling(RateLimiter.AvailableIn(key) / 60.0));
        FlashInput("login", "keep");
        Flash("login_paused", m);
        FlashErrors(("password", $"Login is paused for {m} minutes."));

        return Back();
    }

    public static IActionResult SignIn(Microsoft.AspNetCore.Mvc.Controller controller, FixoraAccount user, string guard, bool keep = false)
    {
        foreach (var g in new[] { "homeowner", "technician", "admin" })
        {
            if (g != guard)
            {
                Auth.Guard(g).Logout();
            }
        }
        Auth.Guard(guard).Login(user, keep);
        SessionStore.Regenerate();
        if (keep)
        {
            controller.Response.Cookies.Append(KeepLoggedIn.COOKIE, "1", new CookieOptions { Expires = DateTimeOffset.UtcNow.AddMinutes(60 * 24 * 30), HttpOnly = true });
        }

        if (guard == "technician")
        {
            if (!user.PhoneVerifiedAt.HasValue)
            {
                return controller.RedirectToRoute("verify.show");
            }

            return ((Technician)user).Verification_Status == "Verified"
                ? RedirectIntended(controller, UrlHelperFactoryAccessor.RouteUrl("tech.jobs"))
                : controller.RedirectToRoute("tech.pending");
        }

        if (guard == "homeowner" && !user.PhoneVerifiedAt.HasValue)
        {
            return controller.RedirectToRoute("verify.show");
        }

        return RedirectIntended(controller, Home.Current());
    }

    private static IActionResult RedirectIntended(Microsoft.AspNetCore.Mvc.Controller controller, string? fallback)
    {
        var target = SessionStore.Pull<string>("url.intended");

        return controller.Redirect(!string.IsNullOrEmpty(target) ? target : (fallback ?? "/"));
    }
    [HttpPost("/logout", Name = "logout")]
    public IActionResult Logout()
    {
        foreach (var g in new[] { "homeowner", "technician" })
        {
            Auth.Guard(g).Logout();
        }
        SessionStore.Invalidate();
        SessionStore.RegenerateToken();
        Response.Cookies.Delete(KeepLoggedIn.COOKIE);
        Toast("You have logged out of Fixora.", "bi-box-arrow-right");

        return RedirectToRoute("login");
    }

    [AllowAnonymous]
    [RedirectIfAuthenticated("homeowner", "technician")]
    [HttpGet("/login/sms", Name = "login.sms")]
    public IActionResult SmsForm()
    {
        return FxView("auth.login-sms");
    }

    [AllowAnonymous]
    [RedirectIfAuthenticated("homeowner", "technician")]
    [HttpPost("/login/sms", Name = "login.sms.send")]
    public IActionResult SmsSend([FromForm] PhoneRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var phone = request.Phone!;
        FixoraAccount? user = _db.HomeOwners.FirstOrDefault(h => h.Phone_Number == phone)
            ?? (FixoraAccount?)_db.Technicians.FirstOrDefault(t => t.Phone_Number == phone);
        if (user == null || (user.Email ?? "").EndsWith("@fixora.invalid"))
        {
            FlashInput();
            FlashErrors(("phone", "We couldn't find a Fixora account with this number."));

            return Back();
        }

        var key = "smslogin:" + phone;
        var wait = _codes.ResendIn(key);
        if (wait > 0 && HasInput("resend"))
        {
            Toast($"You can send a new code in {wait} seconds.", "bi-stopwatch");

            return RedirectToRoute("login.sms.code");
        }

        _codes.Issue(key, phone, "Your Fixora log in code is {code}. It expires in 5 minutes.", FixoraConfig.Current.LoginCodeMinutes);
        SessionStore.Put("fx_sms_login", phone);

        return RedirectToRoute("login.sms.code");
    }

    [AllowAnonymous]
    [RedirectIfAuthenticated("homeowner", "technician")]
    [HttpGet("/login/sms/code", Name = "login.sms.code")]
    public IActionResult SmsCode()
    {
        var phone = SessionStore.Get<string>("fx_sms_login");
        if (string.IsNullOrEmpty(phone))
        {
            return RedirectToRoute("login.sms");
        }

        return FxView("auth.login-sms-code", new
        {
            phone,
            resendIn = _codes.ResendIn("smslogin:" + phone),
        });
    }

    [AllowAnonymous]
    [RedirectIfAuthenticated("homeowner", "technician")]
    [HttpPost("/login/sms/code", Name = "login.sms.verify")]
    public IActionResult SmsVerify([FromForm] CodeRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var phone = SessionStore.Get<string>("fx_sms_login");
        if (string.IsNullOrEmpty(phone))
        {
            return RedirectToRoute("login.sms");
        }

        var r = _codes.Check("smslogin:" + phone, request.Code!);
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

        FixoraAccount? user = _db.HomeOwners.FirstOrDefault(h => h.Phone_Number == phone);
        var guard = "homeowner";
        if (user == null)
        {
            user = _db.Technicians.First(t => t.Phone_Number == phone);
            guard = "technician";
        }

        SessionStore.Forget("fx_sms_login");
        if (!user.PhoneVerifiedAt.HasValue)
        {
            user.PhoneVerifiedAt = Clock.Now();
            _db.SaveChanges();
        }

        return SignIn(this, user, guard);
    }
}
