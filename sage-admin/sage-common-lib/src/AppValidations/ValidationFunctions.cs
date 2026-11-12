using System.Net.Mail;
using System.Text.RegularExpressions;
using SageCommonLib.Globals;

namespace SageCommonLib.AppValidations;

public static partial class ValidationFunctions
{
    [GeneratedRegex(@"^[A-Za-z0-9._-]{3,50}$")] private static partial Regex UsernameRegex();
    [GeneratedRegex(@"^\+?[0-9][0-9\s-]{6,18}[0-9]$")] private static partial Regex PhoneRegex();
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_-]{1,49}$")] private static partial Regex CodeRegex();

    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256) return false;
        try { var a = new MailAddress(email); return a.Address == email.Trim() && a.Host.Contains('.'); }
        catch (FormatException) { return false; }
    }

    public static bool IsValidUsername(string? v) => v is not null && UsernameRegex().IsMatch(v);
    public static bool IsValidPhone(string? v) => v is not null && PhoneRegex().IsMatch(v);
    public static bool IsValidCode(string? v) => v is not null && CodeRegex().IsMatch(v);

    /// <summary>Returns a list of policy violations; empty when the password is acceptable.</summary>
    public static IReadOnlyList<string> CheckPasswordStrength(string? password)
    {
        var errors = new List<string>();
        if (string.IsNullOrEmpty(password) || password.Length < Constants.Password.MinLength)
            errors.Add($"Password must be at least {Constants.Password.MinLength} characters.");
        if (password is null) return errors;
        if (!password.Any(char.IsUpper)) errors.Add("Password must contain an uppercase letter.");
        if (!password.Any(char.IsLower)) errors.Add("Password must contain a lowercase letter.");
        if (!password.Any(char.IsDigit)) errors.Add("Password must contain a digit.");
        if (!password.Any(c => !char.IsLetterOrDigit(c))) errors.Add("Password must contain a special character.");
        return errors;
    }
}
