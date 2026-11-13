namespace SageCommonLib.Email
{
    /// <summary>
    /// Bound from the "Email" configuration section. When Host and FromAddress are set, password-reset emails are sent over
    /// SMTP; otherwise each API falls back to logging (the reset link only in Development). Used by both APIs.
    /// Keep the password out of git: use User Secrets (dotnet user-secrets set "Email:Password" "...") on a developer
    /// machine, or an environment variable (Email__Password) on a server.
    /// </summary>
    public class EmailSettings
    {
        public const string SectionName = "Email";

        public string Host { get; set; } = string.Empty;
        /// <summary>587 with EnableSsl = STARTTLS (the usual setting). Port 465 (implicit TLS) is not supported by System.Net.Mail.</summary>
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        /// <summary>SMTP login; leave empty for a relay that needs no sign-in.</summary>
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        /// <summary>The sender address. Many servers require it to match UserName.</summary>
        public string FromAddress { get; set; } = string.Empty;
        /// <summary>The name shown as the sender, e.g. "Sage Admin" or "Sage Sales".</summary>
        public string FromName { get; set; } = "Sage";
        public int TimeoutSeconds { get; set; } = 30;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
    }
}
