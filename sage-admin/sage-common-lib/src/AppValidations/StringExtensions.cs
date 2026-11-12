namespace SageCommonLib.AppValidations;

public static class StringExtensions
{
    public static string? NullIfWhiteSpace(this string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Escapes LIKE wildcards so user input is matched literally (use with ESCAPE backslash).</summary>
    public static string EscapeLike(this string s) => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

    /// <summary>Strips CR/LF so user text cannot forge log lines.</summary>
    public static string ForLog(this string? s) => (s ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
}
