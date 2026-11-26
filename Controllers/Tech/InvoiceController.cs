using Fixora.Data;
using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Middleware;
using Fixora.Support.Services;
using Fixora.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.Controllers.TechArea;

[Authorize(AuthenticationSchemes = "technician")]
[EnsurePhoneVerified("technician")]
[EnsureTechnicianVerified]
public class InvoiceController : Controller
{
    private readonly AppDbContext _db;
    private readonly InvoiceService _invoices;

    public InvoiceController(AppDbContext db, InvoiceService invoices)
    {
        _db = db;
        _invoices = invoices;
    }

    private Booking? Mine(int id)
    {
        var b = _db.Bookings.Find(id);
        if (b == null || b.Technician_ID != Auth.Guard("technician").Id())
        {
            return null;
        }
        _db.Load(b, "items.service.type", "homeowner", "invoice", "technician");

        return b;
    }

    [HttpGet("/tech/jobs/{booking:int}/invoice", Name = "tech.invoice.create")]
    public IActionResult Create(int booking)
    {
        var b = Mine(booking);
        if (b == null)
        {
            return NotFound();
        }
        if (b.Invoice != null)
        {
            Toast($"Invoice {b.Invoice.Ref} was already sent.", "bi-receipt");

            return RedirectToRoute("tech.job", new { booking = b.Booking_ID });
        }
        if (b.Status != "Completed")
        {
            Toast("Mark the job as completed before creating the invoice.", "bi-exclamation-circle");

            return RedirectToRoute("tech.job", new { booking = b.Booking_ID });
        }

        return FxView("tech.invoice", new { b });
    }

    [HttpPost("/tech/jobs/{booking:int}/invoice", Name = "tech.invoice.store")]
    public IActionResult Store([FromForm] StoreInvoiceRequest request, int booking)
    {
        var b = Mine(booking);
        if (b == null)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }
        if (b.Invoice != null || b.Status != "Completed")
        {
            return RedirectToRoute("tech.job", new { booking = b.Booking_ID });
        }

        var materials = (request.Materials ?? new List<MaterialInput>())
            .Select(m => new MaterialItem { Name = m.Name!, Qty = m.Qty!.Value, Amount = m.Amount!.Value })
            .ToList();
        var inv = _invoices.Issue(b, request.JobTitle!, request.WorkNotes, materials);
        Toast($"Invoice {inv.Ref} sent to {b.Homeowner!.First_Name} · SAR " + Php.NumberFormat(inv.Total_Amount, 2), "bi-send");

        return RedirectToRoute("tech.job", new { booking = b.Booking_ID });
    }
}
