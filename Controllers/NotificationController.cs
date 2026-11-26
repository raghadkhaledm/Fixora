using Fixora.Support;
using Fixora.Support.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.Controllers;

public class NotificationController : Controller
{
    [AllowAnonymous]
    [HttpPost("/notifications/seen/{role:regex(^(homeowner|technician|admin)$)}", Name = "notifications.seen")]
    public IActionResult Seen(string role)
    {
        SessionStore.Put("fx_seen_" + role, Clock.Now().Format("Y-m-d H:i:s"));

        return JsonResponse(new { ok = true });
    }
}
