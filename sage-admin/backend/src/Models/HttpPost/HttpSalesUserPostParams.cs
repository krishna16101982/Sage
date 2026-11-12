using System;

namespace SageAdminApi.Models.HttpPost
{
    public class HttpSalesUserPostParams
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        /// <summary>Immutable once created.</summary>
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MobileNumber { get; set; }
        /// <summary>SalesPerson or SalesManager.</summary>
        public string Role { get; set; } = "SalesPerson";
        /// <summary>Required for a SalesPerson (an active SalesManager); must be empty for a SalesManager.</summary>
        public Guid? ManagerId { get; set; }
        /// <summary>Temporary password; the user must change it at first sign-in.</summary>
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class HttpSalesUserPutParams
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MobileNumber { get; set; }
        public string Role { get; set; } = "SalesPerson";
        public Guid? ManagerId { get; set; }
    }

    public class HttpSalesUserStatusParams
    {
        /// <summary>Active or Inactive.</summary>
        public string Status { get; set; } = string.Empty;
    }

    public class HttpSalesUserPasswordParams
    {
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
