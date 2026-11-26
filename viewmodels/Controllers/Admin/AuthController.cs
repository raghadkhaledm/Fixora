using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Http;
using Fixora.Support.Middleware;
using Fixora.Support.Services;
using Fixora.ViewModels;
using Fixora.Controllers.AuthArea;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.Controllers.AdminArea;

public class AuthController : Controller
{
    private readonly AccountLookup _lookup;

    public AuthController(AccountLookup lookup)
    {
        _lookup = lookup;
    }

    [AllowAnonymous]
    [RedirectIfAuthenticated("admin")]
    [HttpGet("/admin/login", Name = "admin.login")]
    public IActionResult Show()
    {
        return FxView("auth.login", new { admin = true });
    }

    [AllowAnonymous]
    [RedirectIfAuthenticated("admin")]
    [HttpPost("/admin/login", Name = "admin.login.post")]
    public IActionResult Login([FromForm] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var max = FixoraConfig.Current.LoginMaxAttempts;
        var key = _lookup.ThrottleKey(request.Login!, Ip(), "admin");
        if (RateLimiter.TooManyAttempts(key, max))
        {
            var m = Math.Max(1, (int)Math.Ceiling(RateLimiter.AvailableIn(key) / 60.0));
            FlashInput("login");
            Flash("login_paused", m);
            FlashErrors(("password", $"Login is paused for {m} minutes."));

            return Back();
        }

        var (user, _) = _lookup.Find(request.Login!, true);
        if (user == null || !Support.Hash.Check(request.Password, user.Password))
        {
            RateLimiter.Hit(key, 60 * FixoraConfig.Current.LoginPauseMinutes);
            if (RateLimiter.TooManyAttempts(key, max))
            {
                var m = Math.Max(1, (int)Math.Ceiling(RateLimiter.AvailableIn(key) / 60.0));
                FlashInput("login");
                Flash("login_paused", m);
                FlashErrors(("password", $"Login is paused for {m} minutes."));

                return Back();
            }
            FlashInput("login");
            Flash("login_failed", max - RateLimiter.Attempts(key));
            FlashErrors(("password", "Password doesn't match this account."));

            return Back();
        }

        RateLimiter.Clear(key);
        Uploads.CleanTmp();

        return LoginController.SignIn(this, user, "admin", Boolean("keep"));
    }

    [Authorize(AuthenticationSchemes = "admin")]
    [HttpPost("/admin/logout", Name = "admin.logout")]
    public IActionResult Logout()
    {
        Auth.Guard("admin").Logout();
        SessionStore.Invalidate();
        SessionStore.RegenerateToken();
        Toast("You have logged out of Fixora admin.", "bi-box-arrow-right");

        return RedirectToRoute("admin.login");
    }
}
