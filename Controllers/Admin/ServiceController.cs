using Fixora.Data;
using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Collections;
using Fixora.Support.Http;
using Fixora.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fixora.Controllers.AdminArea;

[Authorize(AuthenticationSchemes = "admin")]
public class ServiceController : Controller
{
    private readonly AppDbContext _db;

    public ServiceController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("/admin/services", Name = "admin.services")]
    public IActionResult Index()
    {
        var types = _db.ServiceTypes
            .Include(t => t.Services.OrderByDescending(s => s.Is_Active).ThenBy(s => s.Service_ID))
            .OrderBy(t => t.Service_Type_ID)
            .ToList();
        var technicianCounts = _db.TechnicianServices
            .GroupBy(ts => ts.Service_ID)
            .Select(g => new { g.Key, N = g.Count() })
            .ToDictionary(x => x.Key, x => x.N);
        foreach (var service in types.SelectMany(t => t.Services))
        {
            service.Technicians_Count = technicianCounts.TryGetValue(service.Service_ID, out var n) ? n : 0;
        }

        return FxView("admin.services.index", new
        {
            types,
            used = new IntCountMap(_db.BookingServices.GroupBy(b => b.Service_ID).Select(g => new { g.Key, N = g.Count() }).ToDictionary(x => x.Key, x => x.N)),
        });
    }

    [HttpGet("/admin/services/create", Name = "admin.services.create")]
    public IActionResult Create()
    {
        var s = new Models.Service
        {
            Service_Commission_Fee = FixoraConfig.Current.PlatformFee,
            Unit = "job",
            Is_Active = true,
            Service_Type_ID = int.TryParse(Query("type"), out var t) ? t : 0,
        };

        return FxView("admin.services.form", new { s, types = _db.ServiceTypes.OrderBy(x => x.Service_Type_ID).ToList() });
    }

    [HttpPost("/admin/services", Name = "admin.services.store")]
    public IActionResult Store([FromForm] ServiceRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var s = new Models.Service();
        Fill(s, request);
        _db.Services.Add(s);
        _db.SaveChanges();
        Toast($"{s.Service_Name} was added at SAR " + Fx.Amt(s.Price) + s.UnitSuffix() + ".", "bi-plus-circle");

        return RedirectToRoute("admin.services");
    }

    [HttpGet("/admin/services/{service:int}/edit", Name = "admin.services.edit")]
    public IActionResult Edit(int service)
    {
        var s = _db.Services.Find(service);
        if (s == null)
        {
            return NotFound();
        }

        return FxView("admin.services.form", new { s, types = _db.ServiceTypes.OrderBy(x => x.Service_Type_ID).ToList() });
    }

    [HttpPut("/admin/services/{service:int}", Name = "admin.services.update")]
    public IActionResult Update([FromForm] ServiceRequest request, int service)
    {
        var s = _db.Services.Find(service);
        if (s == null)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        Fill(s, request);
        _db.SaveChanges();
        Toast($"{s.Service_Name} was updated.", "bi-check2-circle");

        return RedirectToRoute("admin.services");
    }

    [HttpDelete("/admin/services/{service:int}", Name = "admin.services.destroy")]
    public IActionResult Destroy(int service)
    {
        var s = _db.Services.Find(service);
        if (s == null)
        {
            return NotFound();
        }

        if (_db.BookingServices.Any(b => b.Service_ID == s.Service_ID))
        {
            s.Is_Active = false;
            _db.SaveChanges();
            Toast($"{s.Service_Name} is used in bookings, so it was hidden instead of deleted.", "bi-eye-slash");

            return Back();
        }

        _db.TechnicianServices.RemoveRange(_db.TechnicianServices.Where(ts => ts.Service_ID == s.Service_ID));
        var name = s.Service_Name;
        _db.Services.Remove(s);
        _db.SaveChanges();
        Toast($"{name} was deleted.", "bi-trash3");

        return Back();
    }

    private static void Fill(Models.Service s, ServiceRequest r)
    {
        s.Service_Type_ID = r.ServiceTypeId!.Value;
        s.Service_Name = r.ServiceName!.Trim();
        s.Service_Category = r.ServiceCategory!;
        s.Price = Php.Round(r.Price!.Value, 2);
        s.Flexible_Rate = r.Unit == "hr";
        s.Service_Commission_Fee = Php.Round(r.ServiceCommissionFee!.Value, 2);
        s.Description = r.Description!.Trim();
        s.Unit = r.Unit!;
        s.Is_Active = r.IsActive;
    }

    [HttpGet("/admin/types/create", Name = "admin.types.create")]
    public IActionResult CreateType()
    {
        return FxView("admin.services.type-form", new { t = new ServiceType { Icon_Class = "bi-tools" } });
    }

    [HttpPost("/admin/types", Name = "admin.types.store")]
    public IActionResult StoreType([FromForm] ServiceTypeRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        var t = new ServiceType();
        FillType(t, request, null);
        _db.ServiceTypes.Add(t);
        _db.SaveChanges();
        Toast($"Category {t.Type_Name} was added. Add its services next.", "bi-plus-circle");

        return RedirectToRoute("admin.services");
    }

    [HttpGet("/admin/types/{type:int}/edit", Name = "admin.types.edit")]
    public IActionResult EditType(int type)
    {
        var t = _db.ServiceTypes.Find(type);
        if (t == null)
        {
            return NotFound();
        }

        return FxView("admin.services.type-form", new { t });
    }

    [HttpPut("/admin/types/{type:int}", Name = "admin.types.update")]
    public IActionResult UpdateType([FromForm] ServiceTypeRequest request, int type)
    {
        var t = _db.ServiceTypes.Find(type);
        if (t == null)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            return ValidationRedirect();
        }

        FillType(t, request, t);
        _db.SaveChanges();
        Toast($"Category {t.Type_Name} was updated.", "bi-check2-circle");

        return RedirectToRoute("admin.services");
    }

    [HttpDelete("/admin/types/{type:int}", Name = "admin.types.destroy")]
    public IActionResult DestroyType(int type)
    {
        var t = _db.ServiceTypes.Find(type);
        if (t == null)
        {
            return NotFound();
        }

        if (_db.Services.Any(s => s.Service_Type_ID == t.Service_Type_ID))
        {
            Toast($"{t.Type_Name} still has services. Delete or move them first.", "bi-exclamation-circle");

            return Back();
        }

        Fx.DeleteUpload(t.Image_URL);
        _db.ServiceTypes.Remove(t);
        _db.SaveChanges();
        Toast("Category deleted.", "bi-trash3");

        return Back();
    }

    private static void FillType(ServiceType target, ServiceTypeRequest r, ServiceType? existing)
    {
        var image = existing?.Image_URL;
        if (r.Image != null)
        {
            Fx.DeleteUpload(image);
            image = Fx.SaveUpload(r.Image, "categories");
        }
        else if (!string.IsNullOrEmpty(r.ImageUrl))
        {
            image = r.ImageUrl;
        }

        target.Type_Name = r.TypeName!.Trim();
        target.Description = r.Description!.Trim();
        target.Icon_Class = r.IconClass!;
        target.Image_URL = image ?? "";
    }
}
