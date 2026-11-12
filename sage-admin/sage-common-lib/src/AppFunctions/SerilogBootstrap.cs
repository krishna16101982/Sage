using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace SageCommonLib.AppFunctions;

/// <summary>Shared Serilog bootstrap so Admin and User APIs log identically.</summary>
public static class SerilogBootstrap
{
    public static Logger CreateLogger(string filePath, string fileName, LogEventLevel minimumLevel = LogEventLevel.Information, bool console = true)
    {
        var cfg = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(Path.Combine(filePath, fileName + "-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 100, shared: true);
        if (console) cfg = cfg.WriteTo.Console();
        return cfg.CreateLogger();
    }
}
