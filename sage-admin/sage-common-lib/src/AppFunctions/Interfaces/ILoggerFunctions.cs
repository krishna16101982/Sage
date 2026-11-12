namespace SageCommonLib.AppFunctions.Interfaces;

/// <summary>Injectable logger (mirrors ixcheck ILoggerFunctions). methodName uses the "C:Controller:Method" / "F:Function:Method" tags.</summary>
public interface ILoggerFunctions
{
    void Information(string methodName, string message);
    void Warning(string methodName, string message);
    void Error(string methodName, string message, Exception? exception = null);
}
