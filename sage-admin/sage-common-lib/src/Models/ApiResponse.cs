using SageCommonLib.Globals;
using SageCommonLib.Models.Interfaces;

namespace SageCommonLib.Models;

/// <summary>Consistent API envelope for every endpoint.</summary>
public class ApiResponse<T>
{
    public bool Status { get; set; }
    public string HttpStatus { get; set; } = Constants.HttpStatusCodes.Ok;
    public string MessageType { get; set; } = Constants.ReturnMessageTypes.Success;
    public List<string> Message { get; set; } = new();
    public T? Data { get; set; }
}

public class ApiResponse : IApiResponse
{
    public ApiResponse<T> Success<T>(T? data, string message = "OK", string httpStatus = Constants.HttpStatusCodes.Ok) => new()
    {
        Status = true,
        HttpStatus = httpStatus,
        MessageType = Constants.ReturnMessageTypes.Success,
        Message = new List<string> { message },
        Data = data
    };

    public ApiResponse<object> Error(string messageType, IEnumerable<string> messages, string httpStatus) => new()
    {
        Status = false,
        HttpStatus = httpStatus,
        MessageType = messageType,
        Message = messages.ToList(),
        Data = null
    };
}
