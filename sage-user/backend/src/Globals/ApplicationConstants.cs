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
            public const string ForgotPasswordGeneric = "If the email belongs to an active account, a reset link has been sent.";
            public const string InvalidResetToken = "This reset link is invalid or has expired. Ask for a new one.";
            public const string PasswordResetDone = "Password reset. Please sign in.";
        }

        public static class Messages
        {
            public const string CustomerNameRequired = "Customer name is required.";
            public const string CustomerNameTooLong = "Customer name must be 200 characters or fewer.";
            public const string ContactPersonTooLong = "Contact person must be 150 characters or fewer.";
            public const string InvalidEmail = "Enter a valid email address.";
            public const string InvalidPhone = "Enter a valid phone number.";
            public const string InvalidGstin = "Enter a valid 15-character GSTIN (e.g. 27AAPFU0939F1ZV).";
            public const string AddressTooLong = "Address must be 500 characters or fewer.";
            public const string PlaceTooLong = "City, state and country must be 100 characters or fewer.";
            public const string CustomerCreated = "Customer added.";
            public const string CustomerNotFound = "Customer not found.";

            public const string CustomerRequired = "Choose a customer.";
            public const string PlanRequired = "Choose a plan.";
            public const string StartDateRequired = "Start date is required.";
            public const string StartDateTooFarAhead = "The start date cannot be more than a year ahead.";
            public const string AmountDecimals = "Amounts can have at most 2 decimal places.";
            public const string NotesTooLong = "Notes must be 1000 characters or fewer.";
            public const string SaleCreated = "Sale recorded.";
            public const string SaleNotFound = "Sale not found.";
            public const string CancelReasonRequired = "Enter a reason for cancelling (up to 500 characters).";
            public const string SaleCancelled = "Sale cancelled.";
            public const string ManagersOnly = "Only sales managers can see team sales.";
            public const string ManagersOnlyCancel = "Only your manager can cancel a sale. Use Request cancellation, or ask your manager.";
            public const string ManagersOnlyTeam = "Only sales managers can manage a team.";
            public const string RequestNotAllowed = "Your manager has not allowed you to request cancellations.";
            public const string ReasonRequired = "Enter a reason (up to 500 characters).";
            public const string NoteTooLong = "The note must be 500 characters or fewer.";
            public const string CancelRequested = "Cancellation requested. Your manager will approve or reject it.";
            public const string RequestApproved = "Request approved: the sale is cancelled.";
            public const string RequestRejected = "Request rejected: the sale stays active.";
            public const string PermissionSaved = "Saved.";
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
            public const string SalesUserPasswordResetTokenCreate = "dbo.usp_SalesUserPasswordResetToken_Create";
            public const string SalesUserResetPasswordWithToken = "dbo.usp_SalesUser_ResetPasswordWithToken";
            public const string SalesUserRefreshTokenRotate = "dbo.usp_SalesUserRefreshToken_Rotate";
            public const string CustomerCreate = "dbo.usp_Customer_Create";
            public const string CustomerGetPaged = "dbo.usp_Customer_GetPaged";
            public const string SaleCreate = "dbo.usp_Sale_Create";
            public const string SaleCancel = "dbo.usp_Sale_Cancel";
            public const string SaleGetPaged = "dbo.usp_Sale_GetPaged";
            public const string SaleCancelRequestCreate = "dbo.usp_SaleCancelRequest_Create";
            public const string SaleCancelRequestDecide = "dbo.usp_SaleCancelRequest_Decide";
            public const string SalesUserSetCancelPermission = "dbo.usp_SalesUser_SetCancelPermission";
        }

        public static class SaleStatus
        {
            public const string Active = "Active";
            public const string Cancelled = "Cancelled";
        }

        public static class SaleScope
        {
            public const string Mine = "mine";
            public const string Team = "team";
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
