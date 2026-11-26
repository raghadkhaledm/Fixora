using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Fixora.Support;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class ReportRequest : FixoraAttributes, IValidatableObject
{
    public const string RedirectRoute = "admin.reports";

    private string? _type;
    private string? _from;
    private string? _to;

    [FromQuery(Name = "type")]
    [Display(Name = "Type")]
    public string? Type
    {
        get => _type is "revenue" or "operations" or "users" ? _type : "revenue";
        set => _type = value;
    }

    [FromQuery(Name = "from")]
    [Display(Name = "From")]
    public string? From
    {
        get => _from ?? new DateTime(Clock.Now().Year, Clock.Now().Month, 1).AddMonths(-5).Format("Y-m-d");
        set => _from = value;
    }

    [FromQuery(Name = "to")]
    [Display(Name = "To")]
    public string? To
    {
        get => _to ?? Clock.Now().Format("Y-m-d");
        set => _to = value;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        const string format = "yyyy-MM-dd";
        var fromOk = DateTime.TryParseExact(From, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var from);
        var toOk = DateTime.TryParseExact(To, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var to);

        if (!fromOk)
        {
            yield return new ValidationResult("Choose the first day of the period.", new[] { "from" });
        }
        if (!toOk)
        {
            yield return new ValidationResult("Choose the last day of the period.", new[] { "to" });
        }
        if (fromOk && toOk)
        {
            if (to < from)
            {
                yield return new ValidationResult("The end date must be on or after the start date.", new[] { "to" });
            }
            else if ((to - from).TotalSeconds > 366 * 86400)
            {
                yield return new ValidationResult("Choose a period of at most 366 days.", new[] { "to" });
            }
        }
    }
}
