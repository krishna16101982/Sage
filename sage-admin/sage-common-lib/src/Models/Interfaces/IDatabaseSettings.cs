namespace SageCommonLib.Models.Interfaces;

public interface IDatabaseSettings
{
    string ConnectionString { get; set; }
    int CommandTimeoutSeconds { get; set; }
    int MaxRetryCount { get; set; }
}
