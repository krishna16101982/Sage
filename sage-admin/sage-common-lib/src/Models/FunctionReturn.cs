using SageCommonLib.Globals;
using SageCommonLib.Models.Interfaces;

namespace SageCommonLib.Models;

/// <summary>Result envelope returned by service/data functions (mirrors ixcheck FunctionReturn).</summary>
public class FunctionReturn : IFunctionReturn
{
    public bool Status { get; set; } = true;
    public string MessageType { get; set; } = string.Empty;
    public List<string> Message { get; set; } = new();
    public string HttpStatusCode { get; set; } = Constants.HttpStatusCodes.Ok;
    public string MethodName { get; set; } = string.Empty;

    public static FunctionReturn Fail(string messageType, string message, string httpStatusCode, string methodName = "") => new()
    {
        Status = false,
        MessageType = messageType,
        Message = new List<string> { message },
        HttpStatusCode = httpStatusCode,
        MethodName = methodName
    };
}
