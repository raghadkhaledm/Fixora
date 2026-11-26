using System.Globalization;
using Fixora.Data;
using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Middleware;
using Fixora.Support.Services;
using Fixora.Support.Views;
using Fixora.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Fixora.Controllers.OwnerArea;

[Authorize(AuthenticationSchemes = "homeowner")]
[EnsurePhoneVerified("homeowner")]
public class BookingFlowController : Controller
{
    private readonly AppDbContext _db;
    private readonly BookingDraft _draft;
    private readonly TechnicianSearch _search;
    private readonly SlotService _slots;

    public BookingFlowController(AppDbContext db, BookingDraft draft, TechnicianSearch search, SlotService slots)
    {
        _db = db;
        _draft = draft;
        _search = search;
        _slots = slots;
    }

    private HomeOwner Owner()
    {
        return Auth.Guard("homeowner").User<HomeOwner>()!;
    }

    [HttpGet("/book", Name = "book.start")]
    public IActionResult Start()
    {
        var o = Owner();
        var serviceId = int.TryParse(Input("service"), out var sid) ? sid : 0;
        var s = !string.IsNullOrEmpty(Input("service")) ? _db.Services.FirstOrDefault(x => x.Is_Active && x.Service_ID == serviceId) : null;
        if (s != null)
        {
            _draft.Clear();
            var values = new Dictionary<string, object?> { ["service_id"] = s.Service_ID, ["quantity"] = 1 };
            var mode = Input("mode");
            if (mode == "Express" || mode == "Scheduled")
            {
                values["booking_type"] = mode;
            }
            var district = Input("district");
            if (!string.IsNullOrEmpty(district) && district == o.District)
            {
                values["address_choice"] = "saved";
            }
            else if (!string.IsNullOrEmpty(district) && FixoraConfig.Current.Cities.TryGetValue(o.City, out var known) && known.ContainsKey(district))
            {
                values["address_choice"] = "other";
                values["service_district"] = district;
            }
            _draft.Put(values);

            return RedirectToRoute("book.service", new { type = s.Service_Type_ID });
        }

        var requestedMode = Input("mode");
        if (requestedMode == "Express" || requestedMode == "Scheduled")
        {
            _draft.Put(new Dictionary<string, object?> { ["booking_type"] = requestedMode });
        }

        var typeList = _db.ServiceTypes.Include(t => t.Services.Where(x => x.Is_Active).OrderBy(x => x.Service_ID)).OrderBy(t => t.Service_Type_ID).ToList();
        var since = Clock.Now().AddDays(-30);
        var again = _db.Bookings
            .Include(b => b.Items).ThenInclude(i => i.Service).ThenInclude(x => x!.Type)
            .Include(b => b.Technician)
            .Include(b => b.Review)
            .Where(b => b.Home_owner_ID == o.Home_owner_ID && b.Status == "Completed" && b.Completed_At >= since)
            .OrderByDescending(b => b.Completed_At)
            .Take(3)
            .ToList();

        return FxView("owner.book.types", new
        {
            types = typeList,
            again,
            summary = Summary.FromDraft(_draft, o),
        });
    }

    [HttpGet("/book/again/{booking:int}", Name = "book.again")]
    public IActionResult Again(int booking)
    {
        var b = _db.Bookings.Find(booking);
        var o = Owner();
        if (b == null || b.Home_owner_ID != o.Home_owner_ID)
        {
            return NotFound();
        }
        var line = _db.BookingServices.Include(i => i.Service).Where(i => i.Booking_ID == b.Booking_ID).OrderBy(i => i.Booking_Service_ID).FirstOrDefault();
        if (line == null || !line.Service!.Is_Active)
        {
            return NotFound();
        }

        _draft.Clear();
        var same = (b.Service_Address ?? "").StartsWith(o.Address) && b.Service_District == o.District;
        _draft.Put(new Dictionary<string, object?>
        {
            ["service_id"] = line.Service_ID,
            ["quantity"] = line.Quantity,
            ["address_choice"] = same ? "saved" : "other",
            ["service_address"] = same ? null : b.StreetPart(),
            ["service_district"] = same ? null : b.Service_District,
        });

        return RedirectToRoute("book.details");
    }

    [HttpGet("/book/{type:int}", Name = "book.service")]
    public IActionResult Service(int type)
    {
        var t = _db.ServiceTypes.Include(x => x.Services.Where(s => s.Is_Active).OrderBy(s => s.Service_ID)).FirstOrDefault(x => x.Service_Type_ID == type);
        if (t == null)
        {
            return NotFound();
        }

        var current = _draft.Service();
        var same = current != null && current.Service_Type_ID == t.Service_Type_ID;
        var firstId = t.Services.FirstOrDefault()?.Service_ID ?? 0;
        var selected = int.TryParse(Blade.Old("service_id"), out var oldSelected) ? oldSelected : (same ? current!.Service_ID : firstId);
        var qty = int.TryParse(Blade.Old("quantity"), out var oldQty) ? oldQty : (same ? _draft.Quantity() : 1);
        var sel = t.Services.FirstOrDefault(s => s.Service_ID == selected);
        if (sel != null)
        {
            sel.Type = t;
        }

        return FxView("owner.book.service", new
        {
            type = t,
            types = _db.ServiceTypes.OrderBy(x => x.Service_Type_ID).ToList(),
            selected,
            qty,
            summary = Summary.FromDraft(_draft, Owner(), sel != null ? new SummaryExtra { Service = sel, Qty = qty } : null),
        });
    }

    [HttpPost("/book/service", Name = "book.service.save")]
    public IActionResult SaveService([FromForm] ChooseServiceRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var s = _db.Services.First(x => x.Service_ID == request.ServiceId!.Value);
        var prev = _draft.Service();
        _draft.Put(new Dictionary<string, object?> { ["service_id"] = s.Service_ID, ["quantity"] = request.Quantity });
        if (prev == null || prev.Service_ID != s.Service_ID)
        {
            _draft.Forget("technician_id");
        }

        return RedirectToRoute("book.details");
    }

    [HttpGet("/book/details", Name = "book.details")]
    public IActionResult Details()
    {
        var service = _draft.Service();
        if (service == null)
        {
            return RedirectToRoute("book.start");
        }

        if (Query("switch") == "Scheduled")
        {
            _draft.Put(new Dictionary<string, object?> { ["booking_type"] = "Scheduled", ["date"] = null, ["slot"] = null });
            _draft.Forget("technician_id");

            return RedirectToRoute("book.details");
        }

        var o = Owner();
        var choice = Blade.Old("address_choice") ?? _draft.Get("address_choice");
        var district = o.District;
        if (choice == "other")
        {
            var chosen = Blade.Old("service_district") ?? _draft.Get("service_district");
            district = !string.IsNullOrEmpty(chosen) ? chosen : o.District;
        }

        return FxView("owner.book.details", new
        {
            service,
            owner = o,
            d = _draft.AllPlain(),
            photos = _draft.Photos(),
            full = _slots.FullDays(service, district, o.City),
            min = _slots.First(),
            max = _slots.Last(),
            summary = Summary.FromDraft(_draft, o, new SummaryExtra
            {
                BadPhotos = Blade.Errors.Has("photos") ? 1 : 0,
                Type = Blade.Old("booking_type"),
            }),
        });
    }

    [HttpPost("/book/details", Name = "book.details.save")]
    public IActionResult SaveDetails([FromForm] BookingDetailsRequest request)
    {
        request.PrepareForValidation(_draft);
        ModelState.Clear();
        TryValidateModel(request);
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var prevType = _draft.Get("booking_type");
        _draft.Put(new Dictionary<string, object?>
        {
            ["booking_type"] = request.BookingType,
            ["date"] = request.Date,
            ["slot"] = request.Slot,
            ["address_choice"] = request.AddressChoice,
            ["service_address"] = request.ServiceAddress,
            ["service_district"] = request.ServiceDistrict,
        });
        if (prevType != request.BookingType || Input("address_choice") == "other")
        {
            _draft.Forget("technician_id");
        }

        return RedirectToRoute("book.technicians");
    }

    [HttpPost("/book/photos/remove", Name = "book.photos.remove")]
    public IActionResult RemovePhoto()
    {
        var photos = _draft.Photos();
        var i = int.TryParse(Input("index"), out var index) ? index : 0;
        if (i >= 0 && i < photos.Count)
        {
            var path = AppPaths.PublicPath(photos[i].Path);
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
            photos.RemoveAt(i);
            _draft.Put(new Dictionary<string, object?> { ["photos"] = photos });
        }

        return RedirectToRoute("book.details");
    }

    [HttpGet("/book/slots", Name = "book.slots")]
    public IActionResult Slots()
    {
        var service = _draft.Service();
        var o = Owner();
        var date = Input("date");
        if (string.IsNullOrEmpty(date) || !DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return UnprocessableEntity(new { message = "The date field is required.", errors = new { date = new[] { "The date field is required." } } });
        }

        var requested = Input("district");
        var district = !string.IsNullOrEmpty(requested) && FixoraConfig.Current.Cities.TryGetValue(o.City, out var known) && known.ContainsKey(requested)
            ? requested
            : o.District;
        var list = service != null ? _slots.ForDay(day, service, district, o.City) : new List<SlotInfo>();

        return JsonResponse(new
        {
            title = day.Format("D j M") + " · free slots",
            slots = list.Select(s => new { start = s.Start, label = s.Label, free = s.Free }),
        });
    }

    private SearchCriteria Criteria()
    {
        var o = Owner();
        var (_, district, city, lat, lng) = _draft.Address(o);
        var sortInput = Input("sort");
        var sort = sortInput != null && TechnicianSearch.SORTS.ContainsKey(sortInput) ? sortInput : "nearest";
        var ratingInput = Input("rating");
        var kmInput = Input("km");

        return new SearchCriteria
        {
            Service = _draft.Service()!,
            District = district,
            City = city,
            Lat = lat,
            Lng = lng,
            Type = _draft.Get("booking_type") ?? "Express",
            When = _draft.When(),
            Available = !HasInput("f") || Boolean("available"),
            Rating = ratingInput is "4.5" or "4.8" or "4.9" ? double.Parse(ratingInput, CultureInfo.InvariantCulture) : 0,
            Km = kmInput is "2" or "5" or "10" ? double.Parse(kmInput, CultureInfo.InvariantCulture) : 0,
            Sort = sort,
        };
    }

    [HttpGet("/book/technicians", Name = "book.technicians")]
    public IActionResult Technicians()
    {
        if (_draft.Service() == null || !_draft.HasDetails())
        {
            return RedirectToRoute("book.details");
        }

        var c = Criteria();
        var values = new RouteValueDictionary(Request.Query.ToDictionary(kv => kv.Key, kv => (object?)kv.Value.ToString()));

        return FxView("owner.book.technicians", new
        {
            service = c.Service,
            c,
            summary = Summary.FromDraft(_draft, Owner()),
            listUrl = Blade.Route("book.technicians.list", values),
        });
    }

    [HttpGet("/book/technicians/list", Name = "book.technicians.list")]
    public IActionResult TechnicianList()
    {
        if (_draft.Service() == null || !_draft.HasDetails())
        {
            return NoContent();
        }

        var c = Criteria();
        var res = _search.Find(c);

        return FxPartial("owner.book.partials.results", new
        {
            c,
            res,
            service = c.Service,
            price = c.Service.Price * _draft.Quantity(),
            query = Request.Query.ToDictionary(kv => kv.Key, kv => kv.Value.ToString()),
        });
    }

    [HttpGet("/technicians/{technician:int}", Name = "technicians.show")]
    public IActionResult Profile(int technician)
    {
        var t = _db.Technicians.Include(x => x.Services).ThenInclude(s => s.Type).FirstOrDefault(x => x.Technician_ID == technician);
        if (t == null || t.Verification_Status != "Verified")
        {
            return NotFound();
        }
        _db.WithCounts(t);

        var o = Owner();
        Booking? booking = null;
        if (int.TryParse(Input("booking"), out var bookingId) && bookingId != 0)
        {
            booking = _db.Bookings.Include(b => b.Items).ThenInclude(i => i.Service).FirstOrDefault(b => b.Home_owner_ID == o.Home_owner_ID && b.Booking_ID == bookingId);
        }

        double lat, lng;
        Models.Service? service;
        int qty;
        bool express;
        if (booking != null)
        {
            if (booking.Service_District == o.District)
            {
                lat = o.Latitude;
                lng = o.Longitude;
            }
            else if (FixoraConfig.Current.Cities.TryGetValue(o.City, out var districts) && districts.TryGetValue(booking.Service_District, out var c))
            {
                lat = c[0];
                lng = c[1];
            }
            else
            {
                lat = o.Latitude;
                lng = o.Longitude;
            }
            service = booking.Service();
            qty = booking.Item()!.Quantity;
            express = booking.IsExpress();
        }
        else
        {
            var address = _draft.Address(o);
            lat = address.Item4;
            lng = address.Item5;
            service = _draft.Service();
            qty = _draft.Quantity();
            express = _draft.IsExpress();
        }

        var km = Fx.Km(lat, lng, t.Latitude, t.Longitude);
        var reviews = _db.Reviews
            .Include(r => r.Homeowner)
            .Include(r => r.Booking).ThenInclude(b => b!.Items).ThenInclude(i => i.Service)
            .Where(r => r.Technician_ID == t.Technician_ID)
            .OrderByDescending(r => r.Created_At)
            .Take(3)
            .ToList();

        return FxPartial("owner.book.partials.profile", new { t, km, reviews, service, qty, express, booking });
    }

    [HttpPost("/book/technician", Name = "book.technician")]
    public IActionResult ChooseTechnician()
    {
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

        if (!_search.StillMatches(t, Criteria()))
        {
            Toast($"{t.Name} is no longer available. Choose another technician.", "bi-person-x");

            return RedirectToRoute("book.technicians");
        }

        _draft.Put(new Dictionary<string, object?> { ["technician_id"] = t.Technician_ID });

        return RedirectToRoute("book.review");
    }

    [HttpGet("/book/review", Name = "book.review")]
    public IActionResult Review()
    {
        var t = _draft.Technician();
        if (_draft.Service() == null || !_draft.HasDetails())
        {
            return RedirectToRoute("book.details");
        }
        if (t == null)
        {
            return RedirectToRoute("book.technicians");
        }

        var o = Owner();
        var c = Criteria();
        t.Reviews_Count = _db.Reviews.Count(r => r.Technician_ID == t.Technician_ID);
        var km = Fx.Km(c.Lat, c.Lng, t.Latitude, t.Longitude);
        var service = _draft.Service()!;
        var qty = _draft.Quantity();
        var express = _draft.IsExpress();
        var (line, district, city, _, _) = _draft.Address(o);
        var labor = Php.Round(service.Price * qty, 2);
        var fee = service.Service_Commission_Fee;
        var exp = express ? FixoraConfig.Current.ExpressFee : 0.0;
        var when = _draft.When()!.Value;

        return FxView("owner.book.review", new
        {
            v = new
            {
                service,
                qty,
                express,
                tech = t,
                km,
                eta = Fx.Eta(km),
                district,
                detailsSub = express ? "Express · " + district : "Scheduled · " + when.Format("D j M"),
                whenLine = express
                    ? "Express · today, " + Clock.Now().Format("D j M") + " · arrives ~" + Fx.Eta(km) + " min"
                    : "Scheduled · " + when.Format("D j M") + " · " + _slots.Label(when.Format("H:i")),
                addressLine = line + ", " + district + ", " + city,
                photos = _draft.Photos().Select(p => p.Path).ToList(),
                labor,
                fee,
                expressFee = exp,
                total = Php.Round(labor + fee + exp, 2),
                points = (int)Php.Floor(labor + fee + exp),
                ownerPhone = o.PhoneMasked,
            },
        });
    }

    [HttpPost("/book", Name = "book.store")]
    public IActionResult Store([FromForm] ConfirmBookingRequest request, [FromServices] Support.Services.BookingService bookings)
    {
        var t = _draft.Technician();
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }
        if (t == null || _draft.Service() == null || !_draft.HasDetails())
        {
            return RedirectToRoute("book.start");
        }

        if (!_search.StillMatches(t, Criteria()))
        {
            _draft.Forget("technician_id");
            Toast($"{t.Name} is no longer available. Choose another technician.", "bi-person-x");

            return RedirectToRoute("book.technicians");
        }

        var b = bookings.Create(Owner(), _draft);
        _draft.Clear();

        return RedirectToRoute("bookings.sending", new { booking = b.Booking_ID });
    }
}
