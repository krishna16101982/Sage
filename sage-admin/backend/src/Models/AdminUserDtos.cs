using System;

namespace SageAdminApi.Models
{
    /// <summary>An Admin Panel account as listed/managed. Id is the user's PublicId. No password data is ever returned.</summary>
    public class AdminUserDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        /// <summary>Immutable once created.</summary>
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MobileNumber { get; set; }
        /// <summary>Active, Inactive or Locked.</summary>
        public string Status { get; set; } = string.Empty;
        /// <summary>Super Admins have every permission; only a Super Admin can manage them.</summary>
        public bool IsSuperAdmin { get; set; }
        public Guid? GroupId { get; set; }
        public string GroupName { get; set; }
        /// <summary>False when the user's group has been deactivated (it then grants nothing).</summary>
        public bool GroupIsActive { get; set; }
        public bool MustChangePassword { get; set; }
        public bool IsLockedOut { get; set; }
        public DateTime? LastLoginAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    /// <summary>An active permission group, for the group drop-down.</summary>
    public class GroupOption
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
