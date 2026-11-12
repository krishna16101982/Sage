namespace SageCommonLib.Globals;

/// <summary>Shared constants reusable by Admin and future User panel APIs.</summary>
public static class Constants
{
    public static class Api
    {
        public const string Version = "V1";
        public const string CorsPolicy = "CorsPolicy";
        public const string ContentType = "application/json";
    }

    public static class HttpStatusCodes
    {
        public const string Ok = "200";
        public const string Created = "201";
        public const string BadRequest = "400";
        public const string Unauthorized = "401";
        public const string Forbidden = "403";
        public const string NotFound = "404";
        public const string Conflict = "409";
        public const string TooManyRequests = "429";
        public const string InternalServerError = "500";
    }

    public static class ReturnMessageTypes
    {
        public const string Success = "SUCCESS";
        public const string AppError = "APP_ERROR";
        public const string SystemError = "SYSTEM_ERROR";
        public const string ValidationError = "VALIDATION_ERROR";
        public const string TokenError = "TOKEN_ERROR";
        public const string PermissionDenied = "PERMISSION_DENIED";
        public const string NotFound = "NOT_FOUND";
        public const string Conflict = "CONFLICT";
    }

    public static class TokenMessages
    {
        public const string InvalidToken = "Invalid or expired token.";
        public const string UnAuthorizedAccess = "Authentication is required.";
    }

    public static class GenericMessages
    {
        public const string PermissionDenied = "You do not have permission to perform this action.";
        public const string ErrorInFetchingRecord = "Error in fetching record.";
        public const string UnexpectedError = "An unexpected error occurred.";
    }

    public static class TokenValues
    {
        public const string UserId = "uid";
        public const string PermissionVersion = "pv";
        public const string UserType = "ut";
    }

    public static class Password
    {
        public const int Pbkdf2Iterations = 600_000;
        public const int SaltSize = 16;
        public const int SubkeySize = 32;
        public const int MinLength = 10;
    }
}
