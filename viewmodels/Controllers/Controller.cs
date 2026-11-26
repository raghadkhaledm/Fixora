using System.Text.Json;
using Fixora.Support.Http;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.Controllers;

public abstract class Controller : Microsoft.AspNetCore.Mvc.Controller
{
    protected IActionResult FxView(string name, object? data = null)
    {
        return View("~/Views/" + name.Replace('.', '/') + ".cshtml", data);
    }

    protected IActionResult FxPartial(string name, object? data = null)
    {
        return PartialView("~/Views/" + name.Replace('.', '/') + ".cshtml", data);
    }

    protected void Flash(string key, object? value)
    {
        SessionStore.Flash(key, value);
    }

    protected void Toast(string text, string icon)
    {
        SessionStore.Flash("toast", new Dictionary<string, string> { ["text"] = text, ["icon"] = icon });
    }

    protected void FlashErrors(params (string Key, string Message)[] errors)
    {
        var merged = SessionStore.Flashed<Dictionary<string, List<string>>>("errors") ?? new Dictionary<string, List<string>>();
        foreach (var (key, message) in errors)
        {
            if (!merged.ContainsKey(key))
            {
                merged[key] = new List<string>();
            }
            merged[key].Add(message);
        }
        SessionStore.Flash("errors", merged);
    }

    protected void FlashInput(params string[] only)
    {
        var old = new Dictionary<string, string>();
        if (Request.HasFormContentType)
        {
            foreach (var kv in Request.Form)
            {
                if (kv.Key.Contains("password") || kv.Key.StartsWith("__") || kv.Key == "card_cvv" || kv.Key == "saved_cvv")
                {
                    continue;
                }
                if (only.Length == 0 || only.Contains(kv.Key))
                {
                    old[kv.Key] = kv.Value.ToString();
                }
            }
        }
        foreach (var kv in Request.Query)
        {
            if (only.Length == 0 || only.Contains(kv.Key))
            {
                old[kv.Key] = kv.Value.ToString();
            }
        }
        SessionStore.Flash("old", old);
    }

    protected IActionResult Back()
    {
        var referer = Request.Headers["Referer"].ToString();

        return Redirect(string.IsNullOrEmpty(referer) ? "/" : referer);
    }

    protected IActionResult ValidationRedirect(string? route = null)
    {
        var errors = ModelState
            .Where(kv => kv.Value != null && kv.Value.Errors.Count > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToList());
        SessionStore.Flash("errors", errors);
        FlashInput();

        return route != null ? RedirectToRoute(route) : Back();
    }

    protected IActionResult Intended(string? fallback)
    {
        var target = SessionStore.Pull<string>("url.intended");

        return Redirect(!string.IsNullOrEmpty(target) ? target : (fallback ?? "/"));
    }

    protected string? Input(string key)
    {
        if (Request.HasFormContentType && Request.Form.TryGetValue(key, out var v))
        {
            return v.ToString();
        }

        return Request.Query.TryGetValue(key, out var q) ? q.ToString() : null;
    }

    protected string? Query(string key, string? fallback = null)
    {
        return Request.Query.TryGetValue(key, out var q) ? q.ToString() : fallback;
    }

    protected bool HasInput(string key)
    {
        return (Request.HasFormContentType && Request.Form.ContainsKey(key)) || Request.Query.ContainsKey(key);
    }

    protected bool Boolean(string key)
    {
        var v = Input(key);

        return v == "1" || v == "true" || v == "on" || v == "yes" || v == "true,false";
    }

    protected string Ip()
    {
        return Current.Ip();
    }

    protected IActionResult JsonResponse(object data)
    {
        return new JsonResult(data, new JsonSerializerOptions { PropertyNamingPolicy = null });
    }
}
