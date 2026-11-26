using Fixora.Data;
using Fixora.Models;
using Fixora.Support.Authentication;
using Fixora.Support.Http;
using Fixora.Support.Middleware;
using Fixora.Support.Services;
using Fixora.Support.Services.Payments;
using Fixora.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.Controllers.OwnerArea;

[Authorize(AuthenticationSchemes = "homeowner")]
[EnsurePhoneVerified("homeowner")]
public class PaymentController : Controller
{
    private readonly AppDbContext _db;
    private readonly PaymentService _payments;
    private readonly CashOtpService _otp;

    public PaymentController(AppDbContext db, PaymentService payments, CashOtpService otp)
    {
        _db = db;
        _payments = payments;
        _otp = otp;
    }

    private Invoice? Mine(int id)
    {
        var inv = _db.Invoices.Find(id);
        if (inv == null)
        {
            return null;
        }
        _db.Load(inv, "booking.items.service.type", "booking.technician", "booking.homeowner", "booking.review", "booking.dispute");
        if (inv.Booking!.Home_owner_ID != Auth.Guard("homeowner").Id())
        {
            return null;
        }

        return inv;
    }

    [HttpGet("/invoices/{invoice:int}/pay", Name = "invoices.pay")]
    public IActionResult Method(int invoice)
    {
        var inv = Mine(invoice);
        if (inv == null)
        {
            return NotFound();
        }
        if (inv.IsPaid())
        {
            return RedirectToRoute("invoices.receipt", new { invoice = inv.Invoice_ID });
        }

        return FxView("owner.pay.method", new
        {
            inv,
            owner = Auth.Guard("homeowner").User<HomeOwner>(),
            declined = SessionStore.Flashed<Dictionary<string, string>>("pay_declined"),
        });
    }

    [HttpPost("/invoices/{invoice:int}/pay", Name = "invoices.pay.post")]
    public IActionResult Pay([FromForm] PayRequest request, int invoice)
    {
        var inv = Mine(invoice);
        if (inv == null)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }
        if (inv.IsPaid())
        {
            return RedirectToRoute("invoices.receipt", new { invoice = inv.Invoice_ID });
        }

        var o = Auth.Guard("homeowner").User<HomeOwner>()!;
        var method = request.Method;
        if (method == "cash")
        {
            return ChooseCash(inv.Invoice_ID);
        }
        if (method == "stc")
        {
            return RedirectToRoute("invoices.stc", new { invoice = inv.Invoice_ID });
        }

        PaymentResult res;
        string label;
        if (method == "saved")
        {
            var saved = PaymentService.SavedCard(o.Credit_Card);
            label = saved[1];
            res = _payments.PayOnline(inv, saved[0], label, new CardInfo { Kind = "saved", Cvv = request.SavedCvv });
        }
        else
        {
            var n = request.CardNumber!;
            var brand = PaymentService.Brand(n);
            label = brand + " •••• " + n.Substring(n.Length - 4);
            res = _payments.PayOnline(inv, brand == "Mada" ? "Mada" : "Card", label, new CardInfo { Kind = "card", Number = n, Cvv = request.CardCvv });
            if (res.Ok && request.SaveCard)
            {
                o.Credit_Card = label + " · " + request.CardExpiry!.Replace(" ", "");
                _db.SaveChanges();
            }
        }

        if (!res.Ok)
        {
            Flash("pay_declined", new Dictionary<string, string> { ["label"] = label, ["reason"] = res.Reason ?? "" });

            return RedirectToRoute("invoices.pay", new { invoice = inv.Invoice_ID });
        }
        Flash("just_paid", res.Earned);

        return RedirectToRoute("invoices.receipt", new { invoice = inv.Invoice_ID });
    }

    [HttpGet("/invoices/{invoice:int}/stc", Name = "invoices.stc")]
    public IActionResult Stc(int invoice)
    {
        var inv = Mine(invoice);
        if (inv == null)
        {
            return NotFound();
        }
        if (inv.IsPaid())
        {
            return RedirectToRoute("invoices.receipt", new { invoice = inv.Invoice_ID });
        }

        return FxView("owner.pay.stc", new { inv, owner = Auth.Guard("homeowner").User<HomeOwner>() });
    }

    [HttpPost("/invoices/{invoice:int}/stc", Name = "invoices.stc.confirm")]
    public IActionResult StcConfirm(int invoice)
    {
        var inv = Mine(invoice);
        if (inv == null)
        {
            return NotFound();
        }
        if (inv.IsPaid())
        {
            return RedirectToRoute("invoices.receipt", new { invoice = inv.Invoice_ID });
        }

        var o = Auth.Guard("homeowner").User<HomeOwner>()!;
        var res = _payments.PayOnline(inv, "STC Pay", "STC Pay · " + o.PhoneMasked, new CardInfo { Kind = "stc" });
        if (!res.Ok)
        {
            Flash("pay_declined", new Dictionary<string, string> { ["label"] = "STC Pay", ["reason"] = res.Reason ?? "" });

            return RedirectToRoute("invoices.pay", new { invoice = inv.Invoice_ID });
        }
        Flash("just_paid", res.Earned);

        return RedirectToRoute("invoices.receipt", new { invoice = inv.Invoice_ID });
    }

    [HttpPost("/invoices/{invoice:int}/cash", Name = "invoices.cash")]
    public IActionResult ChooseCash(int invoice)
    {
        var inv = Mine(invoice);
        if (inv == null)
        {
            return NotFound();
        }
        if (inv.IsPaid())
        {
            return RedirectToRoute("invoices.receipt", new { invoice = inv.Invoice_ID });
        }

        if (!(inv.Payment_Method == "Cash" && !string.IsNullOrEmpty(inv.Cash_OTP_Code) && !inv.OtpLocked()))
        {
            _otp.Issue(inv);
        }

        return RedirectToRoute("invoices.cash.show", new { invoice = inv.Invoice_ID });
    }

    [HttpGet("/invoices/{invoice:int}/cash", Name = "invoices.cash.show")]
    public IActionResult Cash(int invoice)
    {
        var inv = Mine(invoice);
        if (inv == null)
        {
            return NotFound();
        }
        if (inv.IsPaid())
        {
            return RedirectToRoute("invoices.receipt", new { invoice = inv.Invoice_ID });
        }
        if (inv.Payment_Method != "Cash" || string.IsNullOrEmpty(inv.Cash_OTP_Code))
        {
            return RedirectToRoute("invoices.pay", new { invoice = inv.Invoice_ID });
        }

        return FxView("owner.pay.cash", new { inv, owner = Auth.Guard("homeowner").User<HomeOwner>() });
    }

    [HttpPost("/invoices/{invoice:int}/cash/new", Name = "invoices.cash.new")]
    public IActionResult NewCode(int invoice)
    {
        var inv = Mine(invoice);
        if (inv == null)
        {
            return NotFound();
        }
        if (!inv.IsPaid())
        {
            _otp.Issue(inv);
        }

        return RedirectToRoute("invoices.cash.show", new { invoice = inv.Invoice_ID });
    }

    [HttpGet("/invoices/{invoice:int}/status", Name = "invoices.status")]
    public IActionResult Status(int invoice)
    {
        var inv = Mine(invoice);
        if (inv == null)
        {
            return NotFound();
        }

        return JsonResponse(new
        {
            paid = inv.IsPaid(),
            attempts = inv.OTP_Attempts,
            locked = inv.OtpLocked(),
        });
    }

    [HttpGet("/invoices/{invoice:int}/receipt", Name = "invoices.receipt")]
    public IActionResult Receipt(int invoice)
    {
        var inv = Mine(invoice);
        if (inv == null)
        {
            return NotFound();
        }
        if (!inv.IsPaid())
        {
            return RedirectToRoute("invoices.show", new { invoice = inv.Invoice_ID });
        }

        var earned = _db.LoyaltyTransactions
            .Where(l => l.Booking_ID == inv.Booking_ID && l.Transaction_Type == "Earned")
            .Sum(l => (int?)l.Points_Earned) ?? 0;

        return FxView("owner.pay.receipt", new { inv, owner = Auth.Guard("homeowner").User<HomeOwner>(), earned });
    }
}
