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
public class InvoiceController : Controller
{
    private readonly AppDbContext _db;

    public InvoiceController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("/admin/invoices", Name = "admin.invoices")]
    public IActionResult Index()
    {
        var statusQuery = Query("status");
        var status = statusQuery is "Unpaid" or "Paid" or "Refunded" or "Partially Refunded" ? statusQuery : null;
        var q = _db.Invoices
            .Include(i => i.Booking).ThenInclude(b => b!.Homeowner)
            .Include(i => i.Booking).ThenInclude(b => b!.Technician)
            .Include(i => i.Booking).ThenInclude(b => b!.Items).ThenInclude(x => x.Service)
            .AsQueryable();
        if (status != null)
        {
            q = q.Where(i => i.Payment_Status == status);
        }
        var counts = new CountMap(_db.Invoices.GroupBy(i => i.Payment_Status).Select(g => new { g.Key, N = g.Count() }).ToDictionary(x => x.Key, x => x.N));

        return FxView("admin.invoices", new
        {
            invoices = PaginatedList<Invoice>.Create(q.OrderByDescending(i => i.Issued_At), 12),
            status,
            counts,
            sum = new
            {
                due = _db.Invoices.Where(i => i.Payment_Status == "Unpaid").Sum(i => (double?)i.Total_Amount) ?? 0,
                paid = _db.Invoices.Where(i => i.Payment_Status != "Unpaid").Sum(i => (double?)i.Total_Amount) ?? 0,
            },
        });
    }

    [HttpPost("/admin/invoices/{invoice:int}/status", Name = "admin.invoices.status")]
    public IActionResult Status([FromForm] InvoiceStatusRequest request, int invoice, [FromServices] LoyaltyService loyalty)
    {
        var inv = _db.Invoices.Find(invoice);
        if (inv == null)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            Toast(ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage, "bi-exclamation-circle");

            return ValidationRedirect();
        }

        _db.Load(inv, "booking.homeowner");
        var to = request.PaymentStatus;
        if (to == "Paid" && inv.Payment_Status == "Unpaid")
        {
            inv.Payment_Status = "Paid";
            inv.Payment_Method = request.PaymentMethod;
            inv.Paid_At = Clock.Now();
            if (inv.Payment_Method != "Cash")
            {
                inv.Transaction_Ref = "ADM-" + Clock.Now().Format("ymdHis") + "-" + inv.Invoice_ID;
                inv.Card_Label = inv.Payment_Method;
            }
            _db.SaveChanges();
            loyalty.OnPaid(inv);
        }
        else if (to == "Refunded" && (inv.Payment_Status == "Paid" || inv.Payment_Status == "Partially Refunded"))
        {
            inv.Payment_Status = "Refunded";
            _db.SaveChanges();
            loyalty.Reverse(inv, inv.Total_Amount);
        }
        else
        {
            Toast($"{inv.Ref} can't move from {inv.Payment_Status} to {to}.", "bi-exclamation-circle");

            return Back();
        }

        Toast($"{inv.Ref} is now {inv.Payment_Status}.", "bi-receipt");

        return Back();
    }
}
