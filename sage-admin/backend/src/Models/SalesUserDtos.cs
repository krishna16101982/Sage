using System;

namespace SageAdminApi.Models
{
    /// <summary>A User Panel account as the Admin Panel sees it. Id is the user's PublicId. No password data is ever returned.</summary>
    public class SalesUserDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        /// <summary>Immutable once created.</summary>
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MobileNumber { get; set; }
        /// <summary>SalesPerson or SalesManager.</summary>
        public string Role { get; set; } = string.Empty;
        /// <summary>Active, Inactive or Locked.</summary>
        public string Status { get; set; } = string.Empty;
        public Guid? ManagerId { get; set; }
        public string ManagerName { get; set; }
        /// <summary>Number of sales persons reporting to this user (managers only).</summary>
        public int TeamSize { get; set; }
        public bool MustChangePassword { get; set; }
        /// <summary>True while a lockout from failed sign-ins is running.</summary>
        public bool IsLockedOut { get; set; }
        public DateTime? LastLoginAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    /// <summary>An active sales manager, for the manager drop-down.</summary>
    public class SalesManagerOption
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public int TeamSize { get; set; }
    }
}
