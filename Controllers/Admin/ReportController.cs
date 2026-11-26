using System.Globalization;
using Fixora.Support.Services;
using Fixora.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.Controllers.AdminArea;

[Authorize(AuthenticationSchemes = "admin")]
public class ReportController : Controller
{
    [HttpGet("/admin/reports", Name = "admin.reports")]
    public IActionResult Index([FromQuery] ReportRequest request, [FromServices] ReportService reports)
    {
        if (!ModelState.IsValid)
        {
            return ValidationRedirect(ReportRequest.RedirectRoute);
        }

        var type = request.Type!;
        var from = DateTime.ParseExact(request.From!, "yyyy-MM-dd", CultureInfo.InvariantCulture).Date;
        var to = DateTime.ParseExact(request.To!, "yyyy-MM-dd", CultureInfo.InvariantCulture).Date;

        object data = type switch
        {
            "operations" => reports.Operations(from, to),
            "users" => reports.Users(from, to),
            _ => reports.Revenue(from, to),
        };

        return FxView("admin.reports", new { type, from, to, data });
    }
}
