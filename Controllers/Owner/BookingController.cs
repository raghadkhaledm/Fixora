using System.Text.RegularExpressions;
using Fixora.Data;
using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Collections;
using Fixora.Support.Middleware;
using Fixora.Support.Pagination;
using Fixora.Support.Services;
using Fixora.Support.Views;
using Fixora.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fixora.Controllers.OwnerArea;

[Authorize(AuthenticationSchemes = "homeowner")]
[EnsurePhoneVerified("homeowner")]
public class BookingController : Controller
{
    private readonly AppDbContext _db;
    private readonly Support.Services.BookingService _bookings;
    private readonly TechnicianSearch _search;

    public BookingController(AppDbContext db, Support.Services.BookingService bookings, TechnicianSearch search)
    {
        _db = db;
        _bookings = bookings;
        _search = search;
    }

    private HomeOwner Owner()
    {
        return Auth.Guard("homeowner").User<HomeOwner>()!;
    }

    private Booking? Mine(int id)
    {
        var b = _db.Bookings.Find(id);
        if (b == null || b.Home_owner_ID != Owner().Home_owner_ID)
        {
            return null;
        }
        _db.Load(b, "items.service.type", "technician", "invoice", "review", "dispute", "homeowner");

        return b;
    }

    private (double, double) Coordinates(Booking b, HomeOwner o)
    {
        if (b.Service_District == o.District && (b.Service_Address ?? "").StartsWith(o.Address))
        {
            return (o.Latitude, o.Longitude);
        }
        if (FixoraConfig.Current.Cities.TryGetValue(o.City, out var districts) && districts.TryGetValue(b.Service_District, out var c))
        {
            return (c[0], c[1]);
        }

        return (o.Latitude, o.Longitude);
    }

    [HttpGet("/bookings", Name = "bookings.index")]
    public IActionResult Index()
    {
        var tabQuery = Query("tab");
        var tab = tabQuery is "active" or "completed" or "cancelled" ? tabQuery : "all";
        var ownerId = Owner().Home_owner_ID;
        var baseQuery = _db.Bookings.Where(b => b.Home_owner_ID == ownerId);
        var counts = new CountMap(new Dictionary<string, int>
        {
            ["all"] = baseQuery.Count(),
            ["active"] = baseQuery.Count(b => b.Status == "Pending" || b.Status == "Accepted" || b.Status == "Rejected"),
            ["completed"] = baseQuery.Count(b => b.Status == "Completed"),
            ["cancelled"] = baseQuery.Count(b => b.Status == "Cancelled"),
        });

        var q = baseQuery
            .Include(b => b.Items).ThenInclude(i => i.Service).ThenInclude(s => s!.Type)
            .Include(b => b.Technician)
            .Include(b => b.Invoice)
            .AsQueryable();
        switch (tab)
        {
            case "active":
                q = q.Where(b => b.Status == "Pending" || b.Status == "Accepted" || b.Status == "Rejected");
                break;
            case "completed":
                q = q.Where(b => b.Status == "Completed");
                break;
            case "cancelled":
                q = q.Where(b => b.Status == "Cancelled");
                break;
        }

        var s = (Query("q") ?? "").Trim();
        if (s != "")
        {
            var digits = Regex.Replace(Regex.Replace(s, @"^FX-\d{2}-", "", RegexOptions.IgnoreCase), @"\D", "");
            var id = int.TryParse(digits, out var n) ? n : 0;
            q = q.Where(b => (id != 0 && b.Booking_ID == id)
                || b.Items.Any(i => EF.Functions.Like(i.Service!.Service_Name, "%" + s + "%")));
        }

        return FxView("owner.bookings.index", new
        {
            bookings = PaginatedList<Booking>.Create(q.OrderByDescending(b => b.Created_At), 8),
            tab,
            counts,
        });
    }

    [HttpGet("/bookings/{booking:int}", Name = "bookings.show")]
    public IActionResult Show(int booking)
    {
        var b = Mine(booking);
        if (b == null)
        {
            return NotFound();
        }

        var data = new Dictionary<string, object?>
        {
            ["b"] = b,
            ["summary"] = Summary.FromBooking(b, b.Status != "Rejected"),
        };
        if (b.Status == "Rejected")
        {
            var o = Owner();
            var (lat, lng) = Coordinates(b, o);
            var sortQuery = Query("sort");
            var sort = sortQuery != null && TechnicianSearch.SORTS.ContainsKey(sortQuery) ? sortQuery : "nearest";
            data["res"] = _search.Find(new SearchCriteria
            {
                Service = b.Service()!,
                District = b.Service_District,
                City = o.City,
                Lat = lat,
                Lng = lng,
                Type = b.Booking_Type,
                When = b.IsExpress() ? null : b.Scheduled_Date_Time,
                Available = true,
                Rating = 0,
                Km = 0,
                Sort = sort,
                Exclude = new List<int> { b.Technician_ID },
                Booking = b.Booking_ID,
            });
            data["sort"] = sort;
        }

        var state = b.Status switch
        {
            "Pending" => "pending",
            "Rejected" => "rejected",
            "Accepted" => "active",
            "Cancelled" => "cancelled",
            _ => "completed",
        };

        return FxView("owner.bookings." + state, new DynamicBag(data));
    }

    [HttpGet("/bookings/{booking:int}/sending", Name = "bookings.sending")]
    public IActionResult Sending(int booking)
    {
        var b = Mine(booking);
        if (b == null)
        {
            return NotFound();
        }
        if (b.Status != "Pending" || b.Created_At < Clock.Now().AddMinutes(-10))
        {
            return RedirectToRoute("bookings.show", new { booking = b.Booking_ID });
        }

        var o = Owner();
        var t = b.Technician!;
        t.Reviews_Count = _db.Reviews.Count(r => r.Technician_ID == t.Technician_ID);
        var (lat, lng) = Coordinates(b, o);
        var km = Fx.Km(lat, lng, t.Latitude, t.Longitude);
        var service = b.Service();
        var qty = b.Item()!.Quantity;
        var slots = HttpContext.RequestServices.GetRequiredService<SlotService>();

        return FxView("owner.book.review", new
        {
            v = new
            {
                sending = b,
                service,
                qty,
                express = b.IsExpress(),
                tech = t,
                km,
                eta = Fx.Eta(km),
                district = b.Service_District,
                detailsSub = b.IsExpress() ? "Express · " + b.Service_District : "Scheduled · " + b.Scheduled_Date_Time.Format("D j M"),
                whenLine = b.IsExpress()
                    ? "Express · today, " + b.Created_At.Format("D j M") + " · arrives ~" + Fx.Eta(km) + " min"
                    : "Scheduled · " + b.Scheduled_Date_Time.Format("D j M") + " · " + slots.Label(b.Scheduled_Date_Time.Format("H:i")),
                addressLine = b.Service_Address,
                photos = b.Photos(),
                labor = b.LaborTotal(),
                fee = b.FeeTotal(),
                expressFee = b.ExpressFee(),
                total = b.Estimate(),
                points = (int)Php.Floor(b.Estimate()),
                ownerPhone = o.PhoneMasked,
            },
        });
    }

    [HttpPost("/bookings/{booking:int}/resend", Name = "bookings.resend")]
    public IActionResult Resend(int booking)
    {
        var b = Mine(booking);
        if (b == null)
        {
            return NotFound();
        }
        if (b.Status != "Rejected")
        {
            return Forbid();
        }

        if (!int.TryParse(Input("technician_id"), out var technicianId))
        {
            FlashErrors(("technician_id", "The technician id field is required."));

            return Back();
        }

        var t = _db.Technicians.FirstOrDefault(x => x.Verification_Status == "Verified" && x.Technician_ID == technicianId);
        if (t == null)
        {
            return NotFound();
        }

        var o = Owner();
        var ok = t.Technician_ID != b.Technician_ID && _search.StillMatches(t, new SearchCriteria
        {
            Service = b.Service()!,
            District = b.Service_District,
            City = o.City,
            Lat = o.Latitude,
            Lng = o.Longitude,
            Type = b.Booking_Type,
            When = b.IsExpress() ? null : b.Scheduled_Date_Time,
            Sort = "nearest",
            Booking = b.Booking_ID,
        });
        if (!ok)
        {
            Toast($"{t.Name} is no longer available. Choose another technician.", "bi-person-x");

            return Back();
        }

        _bookings.Resend(b, t);
        Toast($"Request {b.Ref} sent to {t.Name}. No new booking was created.", "bi-send");

        return RedirectToRoute("bookings.show", new { booking = b.Booking_ID });
    }

    [HttpPost("/bookings/{booking:int}/cancel", Name = "bookings.cancel")]
    public IActionResult Cancel([FromForm] ReasonRequest request, int booking)
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

        if (!b.CanCancelByOwner())
        {
            Toast("This booking can no longer be cancelled. The technician is already on the way.", "bi-exclamation-circle");

            return Back();
        }

        _bookings.Cancel(b, request.CancelReason);

        if (Boolean("switch") && b.Status == "Cancelled")
        {
            var draft = HttpContext.RequestServices.GetRequiredService<BookingDraft>();
            draft.Clear();
            var o = Owner();
            var photos = new List<DraftPhoto>();
            foreach (var p in b.Photos())
            {
                if (System.IO.File.Exists(AppPaths.PublicPath(p)))
                {
                    var dir = Uploads.TmpDir();
                    Directory.CreateDirectory(AppPaths.PublicPath(dir));
                    var copy = dir + "/" + Path.GetFileName(p);
                    System.IO.File.Copy(AppPaths.PublicPath(p), AppPaths.PublicPath(copy), true);
                    photos.Add(new DraftPhoto { Path = copy, Name = Path.GetFileName(p), Size = new FileInfo(AppPaths.PublicPath(copy)).Length });
                }
            }
            var same = (b.Service_Address ?? "").StartsWith(o.Address) && b.Service_District == o.District;
            draft.Put(new Dictionary<string, object?>
            {
                ["service_id"] = b.Item()!.Service_ID,
                ["quantity"] = b.Item()!.Quantity,
                ["booking_type"] = "Scheduled",
                ["address_choice"] = same ? "saved" : "other",
                ["service_address"] = same ? null : b.StreetPart(),
                ["service_district"] = same ? null : b.Service_District,
                ["photos"] = photos,
            });
            Toast($"{b.Ref} was cancelled. Pick a day and a slot for the scheduled visit.", "bi-calendar3");

            return RedirectToRoute("book.details");
        }

        Toast($"Booking {b.Ref} was cancelled.", "bi-calendar-x");

        return RedirectToRoute("bookings.show", new { booking = b.Booking_ID });
    }

    [HttpPost("/bookings/{booking:int}/edit", Name = "bookings.update")]
    public IActionResult Update([FromForm] UpdateBookingRequest request, int booking)
    {
        var b = Mine(booking);
        if (b == null)
        {
            return NotFound();
        }
        if (b.Status != "Pending")
        {
            return Forbid();
        }
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var photos = b.Photos();
        foreach (var p in request.Remove ?? new List<string>())
        {
            var k = photos.IndexOf(p);
            if (k >= 0)
            {
                Fx.DeleteUpload(p);
                photos.RemoveAt(k);
            }
        }
        foreach (var f in request.Photos ?? new List<Microsoft.AspNetCore.Http.IFormFile>())
        {
            if (photos.Count >= 3)
            {
                break;
            }
            photos.Add(Fx.SaveUpload(f, "bookings/" + b.Booking_ID));
        }

        b.Service_Address = Booking.FullAddress(request.ServiceAddress!, b.Service_District, b.Homeowner!.City);
        b.Issue_Attachment_Photo = photos.Count > 0 ? string.Join("|", photos) : null;
        _db.SaveChanges();
        Toast("Your request was updated.", "bi-check2-circle");

        return RedirectToRoute("bookings.show", new { booking = b.Booking_ID });
    }
}
