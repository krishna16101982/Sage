using SageCommonLib.Models.Interfaces;

namespace SageCommonLib.Models
{
    public class LoggerSettings : ILoggerSettings
    {
        public int LogLevel { get; set; }
        public bool LoggingEnabled { get; set; }
    }
}
