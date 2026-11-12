using System;
using System.Collections.Generic;

namespace SageAdminApi.Models
{
    public class ModuleItem
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>User info returned to the frontend. Permissions come from SQL (never hardcoded in the UI).</summary>
    public class CurrentUserResponse
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool IsSuperAdmin { get; set; }
        public bool MustChangePassword { get; set; }
        public List<string> Permissions { get; set; } = new List<string>();
        public List<ModuleItem> Modules { get; set; } = new List<ModuleItem>();
    }

    /// <summary>Body of login and refresh responses. The refresh token is NOT in the body; it is an HttpOnly cookie.</summary>
    public class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public CurrentUserResponse User { get; set; } = new CurrentUserResponse();
    }

    /// <summary>Internal result of login/refresh: the controller turns RefreshToken into a cookie.</summary>
    public class AuthResult
    {
        public LoginResponse Response { get; set; } = new LoginResponse();
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime RefreshTokenExpiresAtUtc { get; set; }
    }
}
