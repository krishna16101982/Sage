namespace SageCommonLib.Models.Interfaces;

public interface IApiResponse
{
    ApiResponse<T> Success<T>(T? data, string message = "OK", string httpStatus = "200");
    ApiResponse<object> Error(string messageType, IEnumerable<string> messages, string httpStatus);
}
