using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.AppValidations;
using Serilog;

namespace SageCommonLib.AppFunctions;

public class LoggerFunctions : ILoggerFunctions
{
    public void Information(string methodName, string message)
        => Log.Information("{Method} {Message}", methodName, message.ForLog());

    public void Warning(string methodName, string message)
        => Log.Warning("{Method} {Message}", methodName, message.ForLog());

    public void Error(string methodName, string message, Exception? exception = null)
        => Log.Error(exception, "{Method} {Message}", methodName, message.ForLog());
}
