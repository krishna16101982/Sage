using SageCommonLib.Globals;

namespace SageAdminApi.Models
{
    /// <summary>
    /// Bound from the "AuthSettings" configuration section.
    /// </summary>
    public class AuthSettings
    {
        public const string SectionName = "AuthSettings";

        /// <summary>Consecutive wrong passwords before the account is temporarily locked.</summary>
        public int MaxFailedLoginAttempts { get; set; } = 5;
        public int LockoutMinutes { get; set; } = 15;
        public int PasswordResetMinutes { get; set; } = 30;
        /// <summary>PBKDF2 iterations for new hashes (tests lower this for speed; keep the default elsewhere).</summary>
        public int PasswordHashIterations { get; set; } = Constants.Password.Pbkdf2Iterations;
        /// <summary>Refresh-token cookie flags. Use Secure=true behind HTTPS; SameSite=None only for a cross-site frontend.</summary>
        public bool RefreshCookieSecure { get; set; } = true;
        public string RefreshCookieSameSite { get; set; } = "Lax";
        /// <summary>Base URL used to build the password-reset link.</summary>
        public string FrontendBaseUrl { get; set; } = "http://localhost:3000";
    }
}
