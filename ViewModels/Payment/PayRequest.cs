using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using Fixora.Models;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace Fixora.ViewModels;

public class PayRequest : FixoraAttributes, IValidatableObject
{
    private string? _cardNumber;
    private string? _cardCvv;
    private string? _savedCvv;
    private string? _cardName;

    [FromForm(Name = "method")]
    [Display(Name = "Payment method")]
    [Required(ErrorMessage = "Choose a payment method.")]
    public string? Method { get; set; }

    [FromForm(Name = "saved_cvv")]
    [Display(Name = "CVV")]
    public string? SavedCvv
    {
        get => _savedCvv;
        set => _savedCvv = DigitsOnly(value);
    }

    [FromForm(Name = "card_number")]
    [Display(Name = "Card number")]
    public string? CardNumber
    {
        get => _cardNumber;
        set => _cardNumber = DigitsOnly(value);
    }

    [FromForm(Name = "card_expiry")]
    [Display(Name = "Expiry")]
    public string? CardExpiry { get; set; }

    [FromForm(Name = "card_cvv")]
    [Display(Name = "CVV")]
    public string? CardCvv
    {
        get => _cardCvv;
        set => _cardCvv = DigitsOnly(value);
    }

    [FromForm(Name = "card_name")]
    [Display(Name = "Name on card")]
    public string? CardName
    {
        get => _cardName;
        set => _cardName = Regex.Replace(value ?? "", @"\s+", " ").Trim().ToUpperInvariant();
    }

    [FromForm(Name = "save_card")]
    [Display(Name = "Save this card")]
    public bool SaveCard { get; set; }

    public static bool Luhn(string n)
    {
        var sum = 0;
        var alt = false;
        for (var i = n.Length - 1; i >= 0; i--)
        {
            var d = n[i] - '0';
            if (alt)
            {
                d *= 2;
                if (d > 9)
                {
                    d -= 9;
                }
            }
            sum += d;
            alt = !alt;
        }

        return sum % 10 == 0;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var o = Auth.Guard("homeowner").User<HomeOwner>()!;
        var methods = new List<string>();
        if (!string.IsNullOrEmpty(o.Credit_Card))
        {
            methods.Add("saved");
        }
        methods.AddRange(new[] { "stc", "card", "cash" });

        if (string.IsNullOrEmpty(Method) || !methods.Contains(Method))
        {
            yield return new ValidationResult("Choose a payment method.", new[] { "method" });
        }

        if (Method == "saved" && !Regex.IsMatch(SavedCvv ?? "", @"^\d{3}$"))
        {
            yield return new ValidationResult("Enter the 3 digits on the back of the card.", new[] { "saved_cvv" });
        }

        if (Method != "card")
        {
            yield break;
        }

        var n = CardNumber ?? "";
        if (n == "" || n.Length != 16)
        {
            yield return new ValidationResult("Card number is incomplete. It should have 16 digits.", new[] { "card_number" });
        }
        else if (!Luhn(n))
        {
            yield return new ValidationResult("This card number is not valid.", new[] { "card_number" });
        }

        var e = CardExpiry ?? "";
        if (e == "")
        {
            yield return new ValidationResult("Enter the expiry as MM / YY.", new[] { "card_expiry" });
        }
        else
        {
            var m = Regex.Match(e, @"^(0[1-9]|1[0-2])\s*/\s*(\d{2})$");
            if (!m.Success)
            {
                yield return new ValidationResult("Enter the expiry as MM / YY.", new[] { "card_expiry" });
            }
            else
            {
                var end = new DateTime(2000 + int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), 1).AddMonths(1).AddSeconds(-1);
                if (end < Clock.Now())
                {
                    yield return new ValidationResult("This card has expired.", new[] { "card_expiry" });
                }
            }
        }

        if (!Regex.IsMatch(CardCvv ?? "", @"^\d{3}$"))
        {
            yield return new ValidationResult("Enter the 3 digits on the back of the card.", new[] { "card_cvv" });
        }

        var name = CardName ?? "";
        if (name.Length < 2 || name.Length > 60 || !Regex.IsMatch(name, @"^[A-Z][A-Z \-']+$"))
        {
            yield return new ValidationResult("Enter the name as it appears on the card.", new[] { "card_name" });
        }
    }
}
