using SageCommonLib.Globals;

namespace SageCommonLib.Models;

public class AppException : Exception
{
    public string HttpStatusCode { get; }
    public string MessageType { get; }
    public AppException(string message, string httpStatusCode = Constants.HttpStatusCodes.BadRequest, string messageType = Constants.ReturnMessageTypes.AppError, Exception? inner = null)
        : base(message, inner) { HttpStatusCode = httpStatusCode; MessageType = messageType; }
}

public class ValidationException : AppException
{
    public IReadOnlyList<string> Errors { get; }
    public ValidationException(IEnumerable<string> errors)
        : base("Validation failed.", Constants.HttpStatusCodes.BadRequest, Constants.ReturnMessageTypes.ValidationError)
        => Errors = errors.ToList();
}

public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message, Constants.HttpStatusCodes.NotFound, Constants.ReturnMessageTypes.NotFound) { }
}

public class ConflictException : AppException
{
    public ConflictException(string message) : base(message, Constants.HttpStatusCodes.Conflict, Constants.ReturnMessageTypes.Conflict) { }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Insufficient permissions.") : base(message, Constants.HttpStatusCodes.Forbidden, Constants.ReturnMessageTypes.PermissionDenied) { }
}

public class UnauthorizedAppException : AppException
{
    public UnauthorizedAppException(string message = "Unauthorized.") : base(message, Constants.HttpStatusCodes.Unauthorized, Constants.ReturnMessageTypes.TokenError) { }
}

public class DatabaseException : AppException
{
    public int? SqlErrorNumber { get; }
    public DatabaseException(string message, int? sqlErrorNumber = null, Exception? inner = null)
        : base(message, Constants.HttpStatusCodes.InternalServerError, Constants.ReturnMessageTypes.SystemError, inner)
        => SqlErrorNumber = sqlErrorNumber;
}
