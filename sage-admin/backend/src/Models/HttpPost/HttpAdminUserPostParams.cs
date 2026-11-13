using System;

namespace SageAdminApi.Models.HttpPost
{
    public class HttpAdminUserPostParams
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        /// <summary>Immutable once created.</summary>
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MobileNumber { get; set; }
        /// <summary>An active permission group; empty = no permissions until a group is assigned.</summary>
        public Guid? GroupId { get; set; }
        /// <summary>Temporary password; the user must change it at first sign-in.</summary>
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class HttpAdminUserPutParams
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MobileNumber { get; set; }
        public Guid? GroupId { get; set; }
    }

    public class HttpUserStatusParams
    {
        /// <summary>Active or Inactive.</summary>
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>PUT api/admin/profile: the signed-in admin's own name and mobile number (email, group and status are changed by an administrator).</summary>
    public class HttpProfilePutParams
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string MobileNumber { get; set; }
    }
}
