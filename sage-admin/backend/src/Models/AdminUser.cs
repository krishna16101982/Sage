using System;

namespace SageAdminApi.Models
{
    /// <summary>Row model for dbo.AdminUsers (authentication columns).</summary>
    public class AdminUser
    {
        public long Id { get; set; }
        public Guid PublicId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public bool MustChangePassword { get; set; }
        public bool IsSuperAdmin { get; set; }
        public string Status { get; set; } = string.Empty;
        public int FailedLoginCount { get; set; }
        public DateTime? LockoutEndUtc { get; set; }
        public int PermissionVersion { get; set; }
    }
}
