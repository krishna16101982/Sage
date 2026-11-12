namespace SageCommonLib.Models.Interfaces
{
    public interface ILoggerSettings
    {
        int LogLevel { get; set; }
        bool LoggingEnabled { get; set; }
    }
}
