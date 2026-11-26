using Fixora.Data;
using Fixora.Models;
using Fixora.Support.Authentication;
using Fixora.Support.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fixora.Controllers.OwnerArea;

[Authorize(AuthenticationSchemes = "homeowner")]
[EnsurePhoneVerified("homeowner")]
public class RewardsController : Controller
{
    private readonly AppDbContext _db;

    public RewardsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("/rewards", Name = "rewards")]
    public IActionResult Index()
    {
        var o = Auth.Guard("homeowner").User<HomeOwner>()!;

        return FxView("owner.rewards", new
        {
            o,
            ledger = _db.LoyaltyTransactions
                .Include(l => l.Booking).ThenInclude(b => b!.Items).ThenInclude(i => i.Service).ThenInclude(s => s!.Type)
                .Where(l => l.Home_owner_ID == o.Home_owner_ID)
                .OrderByDescending(l => l.Transaction_Date)
                .ToList(),
            unpaid = _db.Invoices
                .Where(i => i.Booking!.Home_owner_ID == o.Home_owner_ID && i.Payment_Status == "Unpaid")
                .OrderByDescending(i => i.Issued_At)
                .FirstOrDefault(),
        });
    }
}
