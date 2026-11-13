using System;
using SageCommonLib.Globals;

namespace SageUserApi.Models
{
    /// <summary>Bound from the "AuthSettings" configuration section.</summary>
    public class AuthSettings
    {
        public const string SectionName = "AuthSettings";
        public int MaxFailedLoginAttempts { get; set; } = 5;
        public int LockoutMinutes { get; set; } = 15;
        /// <summary>PBKDF2 iterations for new hashes (tests lower this for speed; keep the default elsewhere).</summary>
        public int PasswordHashIterations { get; set; } = Constants.Password.Pbkdf2Iterations;
        public bool RefreshCookieSecure { get; set; } = true;
        public string RefreshCookieSameSite { get; set; } = "Lax";
    }

    /// <summary>Row model for dbo.SalesUsers (sign-in columns).</summary>
    public class SalesUser
    {
        public long Id { get; set; }
        public Guid PublicId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public bool MustChangePassword { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? LockoutEndUtc { get; set; }
        public int PermissionVersion { get; set; }
        public string ManagerName { get; set; }
        public bool CanRequestCancellation { get; set; }
    }

    /// <summary>The signed-in sales user as the User Panel sees it.</summary>
    public class CurrentSalesUserResponse
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        /// <summary>SalesPerson or SalesManager.</summary>
        public string Role { get; set; } = string.Empty;
        /// <summary>True for a sales manager (they can also see their team's sales).</summary>
        public bool IsManager { get; set; }
        /// <summary>The sales person's manager (null for a manager).</summary>
        public string ManagerName { get; set; }
        public bool MustChangePassword { get; set; }
        /// <summary>A sales person whose manager allows them to ask for sales to be cancelled (managers cancel directly).</summary>
        public bool CanRequestCancellation { get; set; }
    }

    /// <summary>Body of login and refresh responses. The refresh token is NOT in the body; it is an HttpOnly cookie.</summary>
    public class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public CurrentSalesUserResponse User { get; set; } = new CurrentSalesUserResponse();
    }

    /// <summary>Internal result of login/refresh: the controller turns RefreshToken into a cookie.</summary>
    public class AuthResult
    {
        public LoginResponse Response { get; set; } = new LoginResponse();
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime RefreshTokenExpiresAtUtc { get; set; }
    }
}
