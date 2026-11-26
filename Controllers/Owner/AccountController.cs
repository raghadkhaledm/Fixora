using Fixora.Data;
using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Http;
using Fixora.Support.Middleware;
using Fixora.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.Controllers.OwnerArea;

[Authorize(AuthenticationSchemes = "homeowner")]
[EnsurePhoneVerified("homeowner")]
public class AccountController : Controller
{
    private readonly AppDbContext _db;

    public AccountController(AppDbContext db)
    {
        _db = db;
    }

    private HomeOwner Owner()
    {
        return Auth.Guard("homeowner").User<HomeOwner>()!;
    }

    [HttpGet("/account", Name = "account")]
    public IActionResult Show()
    {
        return FxView("owner.account", new { o = Owner() });
    }

    [HttpPost("/account", Name = "account.update")]
    public IActionResult Update([FromForm] UpdateHomeownerRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var o = Owner();
        var phoneChanged = request.Phone != o.Phone_Number;
        var moved = request.District != o.District || request.City != o.City;
        o.First_Name = request.FirstName!.Trim();
        o.Last_Name = request.LastName!.Trim();
        o.Email = request.Email!;
        o.Phone_Number = request.Phone!;
        o.Address = request.Address!.Trim();
        o.City = request.City!;
        o.District = request.District!;
        if (moved)
        {
            var latLng = FixoraConfig.Current.Cities[o.City][o.District];
            o.Latitude = latLng[0];
            o.Longitude = latLng[1];
        }
        if (phoneChanged)
        {
            o.Phone_Verified_At = null;
        }
        _db.SaveChanges();

        if (phoneChanged)
        {
            Toast("Verify your new number to keep booking.", "bi-phone");

            return RedirectToRoute("verify.show");
        }
        Toast("Your household details were saved.", "bi-check2-circle");

        return RedirectToRoute("account");
    }

    [HttpPost("/account/password", Name = "account.password")]
    public IActionResult Password([FromForm] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var o = Owner();
        o.Password = Support.Hash.Make(request.Password!);
        _db.SaveChanges();
        Toast("Password changed.", "bi-shield-check");

        return RedirectToRoute("account");
    }

    [HttpPost("/account/card/remove", Name = "account.card.remove")]
    public IActionResult RemoveCard()
    {
        var o = Owner();
        o.Credit_Card = null;
        _db.SaveChanges();
        Toast("Saved card removed.", "bi-credit-card");

        return RedirectToRoute("account");
    }

    [HttpPost("/account/delete", Name = "account.delete")]
    public IActionResult Destroy([FromForm] DeleteAccountRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var o = Owner();
        var open = _db.Bookings.Any(b => b.Home_owner_ID == o.Home_owner_ID && (b.Status == "Pending" || b.Status == "Accepted"));
        var disputes = _db.DisputeTickets.Any(d => (d.Ticket_Status == "Open" || d.Ticket_Status == "Under Review") && d.Booking!.Home_owner_ID == o.Home_owner_ID);
        if (open || disputes)
        {
            Toast("Finish or cancel your open bookings and disputes before deleting the account.", "bi-exclamation-circle");

            return Back();
        }

        var id = o.Home_owner_ID;
        o.First_Name = "Deleted";
        o.Last_Name = "user";
        o.Email = "deleted-" + id + "@fixora.invalid";
        o.Phone_Number = "050-000-" + Php.StrPadLeft((id % 10000).ToString(), 4, "0");
        o.Password = Support.Hash.Make(Php.Random(40));
        o.Address = "Deleted";
        o.Credit_Card = null;
        _db.SaveChanges();
        Auth.Guard("homeowner").Logout();
        SessionStore.Invalidate();
        SessionStore.RegenerateToken();

        return RedirectToRoute("landing");
    }
}
