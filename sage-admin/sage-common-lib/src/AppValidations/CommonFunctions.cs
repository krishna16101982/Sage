using System;
using System.Collections.Generic;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;

namespace SageCommonLib.AppValidations
{
    public static class CommonFunctions
    {
        /// <summary>
        /// Seconds since 1970-01-01 UTC (JWT iat / exp claims).
        /// </summary>
        public static long ToUnixEpochDate(DateTime _date)
        {
            return new DateTimeOffset(_date.ToUniversalTime()).ToUnixTimeSeconds();
        }

        /// <summary>
        /// System error: unexpected failure. The technical message is logged by the caller; the client only sees a generic message.
        /// </summary>
        public static IFunctionReturn SystemError(string _errorMessage, string _methodName)
        {
            IFunctionReturn _functionReturn = new FunctionReturn
            {
                Status = false,
                MessageType = Constants.ReturnMessageTypes.SystemError,
                HttpStatusCode = Constants.HttpStatusCodes.InternalServerError,
                MethodName = _methodName
            };
            _functionReturn.Message.Add(_errorMessage);
            return _functionReturn;
        }

        /// <summary>
        /// Application error: a business-rule failure (validation, duplicate, not found...).
        /// </summary>
        public static IFunctionReturn AppError(string _errorMessage, string _methodName, string _httpStatusCode = Constants.HttpStatusCodes.BadRequest)
        {
            IFunctionReturn _functionReturn = new FunctionReturn
            {
                Status = false,
                MessageType = Constants.ReturnMessageTypes.AppError,
                HttpStatusCode = _httpStatusCode,
                MethodName = _methodName
            };
            _functionReturn.Message.Add(_errorMessage);
            return _functionReturn;
        }

        /// <summary>
        /// Application success.
        /// </summary>
        public static IFunctionReturn AppSuccess(string _successMessage, string _methodName)
        {
            IFunctionReturn _functionReturn = new FunctionReturn
            {
                Status = true,
                MessageType = Constants.ReturnMessageTypes.Success,
                HttpStatusCode = Constants.HttpStatusCodes.Ok,
                MethodName = _methodName
            };
            _functionReturn.Message.Add(_successMessage);
            return _functionReturn;
        }

        /// <summary>
        /// Log a failure and build the 500 envelope JSON-ready object (no exception details are returned).
        /// </summary>
        public static ApiResponse<object> Exception500Message(ILoggerFunctions _loggerFunctions, IApiResponse _apiResponse, Exception _exception, string _methodName)
        {
            _loggerFunctions.Error(_methodName, _exception.Message, _exception);
            return _apiResponse.Error(Constants.ReturnMessageTypes.SystemError, new List<string> { Constants.GenericMessages.UnexpectedError }, Constants.HttpStatusCodes.InternalServerError);
        }

        /// <summary>
        /// Log a failure and build the error envelope for a failed IFunctionReturn (status code and messages come from it).
        /// </summary>
        public static ApiResponse<object> ExceptionMessage(ILoggerFunctions _loggerFunctions, IApiResponse _apiResponse, IFunctionReturn _functionReturn, string _methodName)
        {
            _loggerFunctions.Warning(_methodName, string.Join("; ", _functionReturn.Message));
            return _apiResponse.Error(_functionReturn.MessageType, _functionReturn.Message, _functionReturn.HttpStatusCode);
        }
    }
}
