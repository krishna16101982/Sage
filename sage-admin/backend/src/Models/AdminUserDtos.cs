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

    /// <summary>My Profile: the signed-in admin's own account, with when the password was last changed and their recent activity.</summary>
    public class ProfileDto : AdminUserDto
    {
        public DateTime PasswordUpdatedAtUtc { get; set; }
        /// <summary>The last changes this admin made (newest first), from the audit log.</summary>
        public System.Collections.Generic.List<ProfileActivity> RecentActivity { get; set; } = new System.Collections.Generic.List<ProfileActivity>();
    }

    public class ProfileActivity
    {
        public long Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
