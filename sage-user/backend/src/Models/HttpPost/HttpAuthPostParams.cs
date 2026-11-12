namespace SageUserApi.Models.HttpPost
{
    public class HttpLoginPostParams
    {
        /// <summary>Username or email.</summary>
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }


    public class HttpChangePasswordPostParams
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
