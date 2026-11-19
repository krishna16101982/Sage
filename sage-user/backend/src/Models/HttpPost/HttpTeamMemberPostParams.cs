namespace SageUserApi.Models.HttpPost
{
    /// <summary>Body of POST api/user/sales/team-members: a manager adds a sales person to their own team.</summary>
    public class HttpTeamMemberPostParams
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        /// <summary>Sign-in name; cannot be changed later.</summary>
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MobileNumber { get; set; }
        /// <summary>Temporary password; the new user must change it at first sign-in.</summary>
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    /// <summary>Body of PUT api/user/sales/team-members/{id}/status: "Active" or "Inactive".</summary>
    public class HttpTeamMemberStatusParams
    {
        public string Status { get; set; } = string.Empty;
    }
}
