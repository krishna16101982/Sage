using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models;
using SageCommonLib.Globals;
using SageCommonLib.Models.Interfaces;

namespace SageUserApi.AppFunctions
{
    /// <summary>
    /// Converts exceptions into the common API envelope. Internal details are logged, never returned.
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private readonly RequestDelegate _next;

        public ExceptionHandlingMiddleware(RequestDelegate __next)
        {
            _next = __next;
        }

        public async Task InvokeAsync(HttpContext _context, IApiResponse _apiResponse, ILoggerFunctions _loggerFunctions)
        {
            string _methodName = "M:ExceptionHandling:Invoke";
            try
            {
                await _next(_context);
            }
            catch (Exception ex) when (!_context.Response.HasStarted)
            {
                (int _status, ApiResponse<object> _body) = Build(ex, _apiResponse);
                if (_status >= 500) _loggerFunctions.Error(_methodName, $"Unhandled exception on {_context.Request.Method} {_context.Request.Path.Value}", ex);
                else _loggerFunctions.Information(_methodName, $"Request failed with {_status}: {ex.GetType().Name}");
                _context.Response.StatusCode = _status;
                _context.Response.ContentType = Constants.Api.ContentType;
                await _context.Response.WriteAsync(JsonSerializer.Serialize(_body, JsonOptions));
            }
        }

        internal static (int status, ApiResponse<object> body) Build(Exception ex, IApiResponse api) => ex switch
        {
            ValidationException v => (400, api.Error(v.MessageType, v.Errors, v.HttpStatusCode)),
            AppException a => (int.Parse(a.HttpStatusCode), api.Error(a.MessageType, new[] { a.Message }, a.HttpStatusCode)),
            _ => (500, api.Error(Constants.ReturnMessageTypes.SystemError, new[] { Constants.GenericMessages.UnexpectedError }, Constants.HttpStatusCodes.InternalServerError))
        };
    }
}
