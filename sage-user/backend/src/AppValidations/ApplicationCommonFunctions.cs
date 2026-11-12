using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;

namespace SageUserApi.AppValidations
{
    public class ApplicationCommonFunctions
    {
        /// <summary>
        /// Client IP as seen by Kestrel (or the forwarded-for header once a trusted proxy is configured).
        /// </summary>
        public static string GetClientIp(HttpContext _httpContext)
        {
            return _httpContext?.Connection?.RemoteIpAddress?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Convert a failed IFunctionReturn into the HTTP result with the common error envelope.
        /// </summary>
        public static IActionResult ErrorResult(IApiResponse _apiResponse, IFunctionReturn _functionReturn)
        {
            int _statusCode = int.TryParse(_functionReturn.HttpStatusCode, out int _parsed) ? _parsed : 500;
            var _body = _apiResponse.Error(_functionReturn.MessageType, _functionReturn.Message, _functionReturn.HttpStatusCode);
            return new ObjectResult(_body) { StatusCode = _statusCode };
        }

        /// <summary>
        /// Generic 500 result that never exposes exception details.
        /// </summary>
        public static IActionResult SystemErrorResult(IApiResponse _apiResponse)
        {
            var _body = _apiResponse.Error(Constants.ReturnMessageTypes.SystemError, new List<string> { Constants.GenericMessages.UnexpectedError }, Constants.HttpStatusCodes.InternalServerError);
            return new ObjectResult(_body) { StatusCode = 500 };
        }

        /// <summary>404 result for a missing record.</summary>
        public static IFunctionReturn NotFound(string _message, string _methodName)
        {
            return FunctionReturn.Fail(Constants.ReturnMessageTypes.NotFound, _message, Constants.HttpStatusCodes.NotFound, _methodName);
        }

        /// <summary>409 result for a duplicate or a conflicting state.</summary>
        public static IFunctionReturn Conflict(string _message, string _methodName)
        {
            return FunctionReturn.Fail(Constants.ReturnMessageTypes.Conflict, _message, Constants.HttpStatusCodes.Conflict, _methodName);
        }

        /// <summary>400 result for a business-rule failure.</summary>
        public static IFunctionReturn BadRequest(string _message, string _methodName)
        {
            return FunctionReturn.Fail(Constants.ReturnMessageTypes.ValidationError, _message, Constants.HttpStatusCodes.BadRequest, _methodName);
        }

        /// <summary>
        /// Failed IFunctionReturn for a system error (logs the technical message via the caller).
        /// </summary>
        public static IFunctionReturn SystemError(string _methodName)
        {
            return FunctionReturn.Fail(Constants.ReturnMessageTypes.SystemError, Constants.GenericMessages.UnexpectedError, Constants.HttpStatusCodes.InternalServerError, _methodName);
        }
    }
}
