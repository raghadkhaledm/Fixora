using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Fixora.Data;
using Fixora.Support;
using Fixora.Support.Rules;

namespace Fixora.ViewModels.Concerns;

public abstract class FixoraAttributes
{
    public const string NamePattern = @"^[\p{L}\s'\-]+$";

    public const string FirstNameMessage = "Enter your first name (letters only, 2–50).";
    public const string LastNameMessage = "Enter your last name (letters only, 2–50).";
    public const string EmailRequiredMessage = "Enter your email.";
    public const string EmailInvalidMessage = "Enter a valid email address.";
    public const string EmailTakenMessage = "This email already has a Fixora account.";
    public const string PhoneRequiredMessage = "Enter your phone number.";
    public const string PhoneTakenMessage = "This number already has a Fixora account.";
    public const string PasswordWeakMessage = "Too weak. Use at least 8 characters with a number and a symbol.";
    public const string PasswordMaxMessage = "Use at most 64 characters.";
    public const string PasswordSpacesMessage = "Don't use spaces in the password.";
    public const string TermsMessage = "Accept the Terms of Service and Privacy Policy.";
    public const string AddressMessage = "Enter your home address (5–200 characters).";
    public const string CityMessage = "Choose Riyadh, Jeddah or Dammam.";
    public const string DistrictMessage = "Choose your district in the selected city.";
    public const string CodeMessage = "This code doesn't match. Check the SMS and try again.";

    public static readonly Dictionary<string, string> Attributes = new()
    {
        ["first_name"] = "First name",
        ["last_name"] = "Last name",
        ["email"] = "Email",
        ["login"] = "Email or phone number",
        ["phone"] = "Phone number",
        ["password"] = "Password",
        ["current_password"] = "Current password",
        ["terms"] = "Terms of Service",
        ["address"] = "Home address",
        ["city"] = "City",
        ["district"] = "District",
        ["code"] = "Code",
        ["trade"] = "Trade",
        ["national_id"] = "National ID / Iqama number",
        ["national_id_file"] = "National ID",
        ["license_file"] = "Vocational license",
        ["services"] = "Services you offer",
        ["zones"] = "Coverage zones",
        ["hourly_rate"] = "Hourly rate",
        ["service_id"] = "Service",
        ["quantity"] = "Quantity",
        ["booking_type"] = "Booking type",
        ["date"] = "Date",
        ["slot"] = "Time slot",
        ["address_choice"] = "Service address",
        ["service_address"] = "Service address",
        ["service_district"] = "District",
        ["photos"] = "Photos",
        ["photos.*"] = "Photo",
        ["sort"] = "Sort",
        ["decline_reason"] = "Reason",
        ["cancel_reason"] = "Reason",
        ["method"] = "Payment method",
        ["card_number"] = "Card number",
        ["card_expiry"] = "Expiry",
        ["card_cvv"] = "CVV",
        ["card_name"] = "Name on card",
        ["save_card"] = "Save this card",
        ["redeem_points"] = "Points to redeem",
        ["job_title"] = "Job title",
        ["work_notes"] = "Work notes",
        ["materials"] = "Materials",
        ["materials.*.name"] = "Material",
        ["materials.*.qty"] = "Quantity",
        ["materials.*.amount"] = "Amount",
        ["problem_type"] = "What went wrong",
        ["issue_description"] = "Description",
        ["refund_amount"] = "Requested refund",
        ["photo"] = "Photo",
        ["rating_score"] = "Rating",
        ["comment"] = "Review",
        ["service_name"] = "Service name",
        ["service_category"] = "Category",
        ["price"] = "Price",
        ["unit"] = "Unit",
        ["service_commission_fee"] = "Platform fee",
        ["description"] = "Description",
        ["type_name"] = "Category name",
        ["icon_class"] = "Icon",
        ["image_url"] = "Image",
        ["verification_note"] = "Reason",
        ["decision"] = "Decision",
        ["resolution_note"] = "Note",
        ["status"] = "Status",
        ["from"] = "From",
        ["to"] = "To",
        ["payment_status"] = "Payment status",
        ["agreed_price"] = "Agreed price per room",
    };

    public static string? NormalizePhone(string? value)
    {
        return Phone.Normalize(value);
    }

    public static string NormalizeEmail(string? value)
    {
        return (value ?? "").Trim().ToLowerInvariant();
    }

    public static string DigitsOnly(string? value)
    {
        return Regex.Replace(value ?? "", @"\D", "");
    }

    protected static IEnumerable<ValidationResult> EmailRules(ValidationContext ctx, string? email, string field = "email", string? ignoreTable = null, int? ignoreId = null)
    {
        var db = (AppDbContext?)ctx.GetService(typeof(AppDbContext));
        if (db != null && !string.IsNullOrEmpty(email) && AccountUniqueness.EmailTaken(db, email, ignoreTable, ignoreId))
        {
            yield return new ValidationResult(EmailTakenMessage, new[] { field });
        }
    }

    protected static IEnumerable<ValidationResult> PhoneRules(ValidationContext ctx, string? phone, string field = "phone", string? ignoreTable = null, int? ignoreId = null)
    {
        var db = (AppDbContext?)ctx.GetService(typeof(AppDbContext));
        if (db != null && !string.IsNullOrEmpty(phone) && FixoraMobileAttribute.Passes(phone) && AccountUniqueness.PhoneTaken(db, phone, ignoreTable, ignoreId))
        {
            yield return new ValidationResult(PhoneTakenMessage, new[] { field });
        }
    }

    protected static IEnumerable<ValidationResult> PasswordRules(string? password, string? firstName, string? phone, string field = "password")
    {
        var pw = password ?? "";
        if (pw == "")
        {
            yield return new ValidationResult(PasswordWeakMessage, new[] { field });
            yield break;
        }
        if (pw.Length < 8 || !Regex.IsMatch(pw, @"\d") || !Regex.IsMatch(pw, @"[^A-Za-z0-9]"))
        {
            yield return new ValidationResult(PasswordWeakMessage, new[] { field });
            yield break;
        }
        if (pw.Length > 64)
        {
            yield return new ValidationResult(PasswordMaxMessage, new[] { field });
            yield break;
        }
        if (Regex.IsMatch(pw, @"\s"))
        {
            yield return new ValidationResult(PasswordSpacesMessage, new[] { field });
            yield break;
        }
        if (!NotContainFirstNameOrPhoneAttribute.Passes(pw, firstName, phone))
        {
            yield return new ValidationResult(NotContainFirstNameOrPhoneAttribute.Message, new[] { field });
        }
    }

    protected static IEnumerable<ValidationResult> CityRule(string? city, string field = "city")
    {
        if (string.IsNullOrEmpty(city) || !FixoraConfig.Current.Cities.ContainsKey(city))
        {
            yield return new ValidationResult(CityMessage, new[] { field });
        }
    }

    protected static IEnumerable<ValidationResult> DistrictRule(string? city, string? district, string field = "district")
    {
        var districts = city != null && FixoraConfig.Current.Cities.TryGetValue(city, out var d) ? d.Keys : Enumerable.Empty<string>();
        if (string.IsNullOrEmpty(district) || !districts.Contains(district))
        {
            yield return new ValidationResult(DistrictMessage, new[] { field });
        }
    }
}
