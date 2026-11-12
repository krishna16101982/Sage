namespace SageCommonLib.Models.Interfaces;

public interface IFunctionReturn
{
    bool Status { get; set; }
    string MessageType { get; set; }
    List<string> Message { get; set; }
    string HttpStatusCode { get; set; }
    string MethodName { get; set; }
}
