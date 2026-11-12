namespace SageAdminApi.Globals
{
    public class ApplicationConstants
    {
        public static class Api
        {
            /// <summary>Route prefix for all Admin API controllers (api/admin/[controller]).</summary>
            public const string RoutePrefix = "api/admin";
        }

        public static class Auth
        {
            public const string RefreshCookieName = "sage_rt";
            public const string RefreshCookiePath = "/api/admin/auth";
            public const string RateLimitPolicy = "auth";
            public const string InvalidCredentials = "Invalid username or password.";
            public const string AccountLocked = "Too many failed attempts. The account is temporarily locked; try again later.";
            public const string InvalidRefreshToken = "Invalid or expired session. Please sign in again.";
            public const string InvalidResetToken = "Invalid or expired reset token.";
            public const string CurrentPasswordIncorrect = "Current password is incorrect.";
            public const string NewPasswordSameAsCurrent = "New password must be different from the current password.";
            public const string ForgotPasswordGeneric = "If the email belongs to an active account, a reset link has been sent.";
            public const string PasswordChanged = "Password changed. Please sign in again.";
            public const string PasswordResetDone = "Password reset. Please sign in.";
            public const string LoggedOut = "Logged out.";
            public const string RateLimited = "Too many requests. Please try again later.";
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

        public static class Environment
        {
            /// <summary>Set by the platform at deployment time (same idea as ixcheck IXCHECK_PLATFORM_VERSION).</summary>
            public const string PlatformVersionVariable = "SAGE_PLATFORM_VERSION";
        }

        public static class ValidationMessages
        {
            public const string InvalidGuid = "Id is invalid";
            public const string DataRequired = "Data is required";
            public const string UsernameRequired = "Username is required";
            public const string UsernameInvalid = "Username must be 3-50 characters: letters, digits, '.', '_' or '-'";
            public const string EmailInvalid = "Email is invalid";
            public const string MobileInvalid = "Mobile number is invalid";
            public const string PasswordMismatch = "Password and confirm password do not match";
            public const string GroupCodeInvalid = "Group code must start with a letter and contain only letters, digits, '_' or '-'";
            public const string GroupNameRequired = "Group name is required (max 100 characters)";
            public const string GroupStatusInvalid = "Status must be Active or Inactive";
            public const string GroupNotFound = "Group not found";
            public const string GroupSystemDelete = "System groups cannot be deleted";
            public const string GroupSystemDeactivate = "System groups cannot be deactivated";
            public const string PermissionIdsRequired = "Permission list is required (send an empty list to remove all)";
            public const string RecordAlreadyExists = "Record already exists";
            public const string ModuleCodeInvalid = "Module code must start with a letter and contain only letters, digits, '_' or '-' (2-50 characters)";
            public const string ModuleNameRequired = "Module name is required (max 100 characters)";
            public const string CategoryTooLong = "Category must be at most 100 characters";
            public const string ActionInvalid = "Action must start with a letter and contain only letters, digits, '_' or '-' (2-50 characters)";
            public const string PermissionNameRequired = "Permission name is required (max 150 characters)";
            public const string DescriptionTooLong = "Description must be at most 500 characters";
            public const string ModuleCodeExists = "A module with this code already exists";
            public const string PermissionCodeExists = "This permission already exists for the module";
            public const string ModuleNotFound = "Module not found";
            public const string PermissionNotFound = "Permission not found";
            public const string ModuleInactive = "Permissions cannot be added to an inactive module";
            public const string SystemModuleDeactivate = "System modules cannot be deactivated";
            public const string SystemPermissionDeactivate = "System permissions cannot be deactivated";
        }
    }
}
