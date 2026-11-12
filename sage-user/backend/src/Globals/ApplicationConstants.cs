namespace SageUserApi.Globals
{
    public class ApplicationConstants
    {
        public static class Api
        {
            /// <summary>Route prefix for all User Panel API controllers.</summary>
            public const string RoutePrefix = "api/user";
        }

        public static class Auth
        {
            /// <summary>Different from the Admin API's cookie so both panels can be signed in side by side.</summary>
            public const string RefreshCookieName = "sage_user_rt";
            public const string RefreshCookiePath = "/api/user/auth";
            public const string RateLimitPolicy = "auth";
            public const string InvalidCredentials = "Invalid username or password.";
            public const string AccountLocked = "Too many failed attempts. The account is temporarily locked; try again later.";
            public const string InvalidRefreshToken = "Invalid or expired session. Please sign in again.";
            public const string CurrentPasswordIncorrect = "Current password is incorrect.";
            public const string NewPasswordSameAsCurrent = "New password must be different from the current password.";
            public const string PasswordChanged = "Password changed. Please sign in again.";
            public const string LoggedOut = "Logged out.";
            public const string RateLimited = "Too many requests. Please try again later.";
            public const string PasswordMismatch = "Password and confirm password do not match";
        }

        public static class ConfigSettingNames
        {
            public const string DatabaseSettings = "DatabaseSettings";
            public const string CorsAllowedOrigins = "Cors:AllowedOrigins";
            public const string AuthSettings = "AuthSettings";
            public const string RateLimitAuthPermit = "RateLimiting:AuthPermitPerMinute";
        }

        public static class SeriLog
        {
            public const string ConfigSectionName = "Serilog";
            public const string FilePath = "FilePath";
            public const string FileName = "FileName";
            public const string MinimumLevel = "MinimumLevel";
        }
    }

    public class ApplicationDatabaseConstants
    {
        /// <summary>Stored procedures (sage-admin/backend/database/StoredProcedures). Never build a name from input.</summary>
        public static class StoredProcedures
        {
            public const string SalesUserRecordLogin = "dbo.usp_SalesUser_RecordLogin";
            public const string SalesUserChangeOwnPassword = "dbo.usp_SalesUser_ChangeOwnPassword";
            public const string SalesUserRefreshTokenRotate = "dbo.usp_SalesUserRefreshToken_Rotate";
        }

        public static class Status
        {
            public const string Active = "Active";
        }

        public static class SalesRoles
        {
            public const string SalesPerson = "SalesPerson";
            public const string SalesManager = "SalesManager";
        }
    }
}
