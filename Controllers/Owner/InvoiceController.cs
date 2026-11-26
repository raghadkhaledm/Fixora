using Fixora.Data;
using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Collections;
using Fixora.Support.Middleware;
using Fixora.Support.Pagination;
using Fixora.Support.Services;
using Fixora.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fixora.Controllers.OwnerArea;

[Authorize(AuthenticationSchemes = "homeowner")]
[EnsurePhoneVerified("homeowner")]
public class InvoiceController : Controller
{
    private readonly AppDbContext _db;
    private readonly LoyaltyService _loyalty;

    public InvoiceController(AppDbContext db, LoyaltyService loyalty)
    {
        _db = db;
        _loyalty = loyalty;
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

    [HttpGet("/invoices", Name = "invoices.index")]
    public IActionResult Index()
    {
        var o = Auth.Guard("homeowner").User<HomeOwner>()!;
        var tabQuery = Query("tab");
        var tab = tabQuery is "unpaid" or "paid" ? tabQuery : "all";
        var baseQuery = _db.Invoices.Where(i => i.Booking!.Home_owner_ID == o.Home_owner_ID);
        var q = baseQuery
            .Include(i => i.Booking).ThenInclude(b => b!.Items).ThenInclude(x => x.Service).ThenInclude(s => s!.Type)
            .Include(i => i.Booking).ThenInclude(b => b!.Technician)
            .AsQueryable();
        if (tab == "unpaid")
        {
            q = q.Where(i => i.Payment_Status == "Unpaid");
        }
        else if (tab == "paid")
        {
            q = q.Where(i => i.Payment_Status != "Unpaid");
        }

        return FxView("owner.invoices.index", new
        {
            invoices = PaginatedList<Invoice>.Create(q.OrderByDescending(i => i.Issued_At), 8),
            tab,
            counts = new CountMap(new Dictionary<string, int>
            {
                ["all"] = baseQuery.Count(),
                ["unpaid"] = baseQuery.Count(i => i.Payment_Status == "Unpaid"),
                ["paid"] = baseQuery.Count(i => i.Payment_Status != "Unpaid"),
            }),
            due = baseQuery.Where(i => i.Payment_Status == "Unpaid").Sum(i => (double?)i.Total_Amount) ?? 0,
        });
    }

    [HttpGet("/invoices/{invoice:int}", Name = "invoices.show")]
    public IActionResult Show(int invoice)
    {
        var inv = Mine(invoice);
        if (inv == null)
        {
            return NotFound();
        }

        var o = Auth.Guard("homeowner").User<HomeOwner>();
        var canDispute = inv.IsPaid() && inv.Booking!.Dispute == null && inv.Paid_At!.Value > Clock.Now().AddDays(-FixoraConfig.Current.DisputeDays);
        var earnedRows = _db.LoyaltyTransactions.Where(l => l.Booking_ID == inv.Booking_ID && l.Transaction_Type == "Earned");

        return FxView(inv.IsPaid() ? "owner.invoices.paid" : "owner.invoices.unpaid", new
        {
            inv,
            owner = o,
            canDispute,
            earned = earnedRows.Sum(l => (int?)l.Points_Earned) ?? 0,
            earnedAt = earnedRows.Select(l => (DateTime?)l.Transaction_Date).FirstOrDefault(),
        });
    }

    [HttpGet("/invoices/{invoice:int}/print", Name = "invoices.print")]
    public IActionResult Print(int invoice)
    {
        var inv = Mine(invoice);
        if (inv == null)
        {
            return NotFound();
        }

        return FxView("owner.invoices.print", new { inv });
    }

    [HttpPost("/invoices/{invoice:int}/redeem", Name = "invoices.redeem")]
    public IActionResult Redeem([FromForm] RedeemRequest request, int invoice)
    {
        var inv = Mine(invoice);
        if (inv == null)
        {
            return NotFound();
        }
        if (inv.IsPaid())
        {
            return Forbid();
        }
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var err = _loyalty.SetDiscount(inv, request.RedeemPoints!.Value);
        if (err != null)
        {
            FlashErrors(("redeem_points", err));

            return Back();
        }

        var p = request.RedeemPoints.Value;
        Toast(p > 0 ? Php.NumberFormat(p, 0) + " points applied: SAR " + FixoraConfig.Current.Tiers[p] + " off this invoice." : "Points discount removed.", "bi-gift");

        return Back();
    }
}
