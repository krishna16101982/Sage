using SageCommonLib.Models.Interfaces;

namespace SageCommonLib.Models;

/// <summary>Bound from the "DatabaseSettings" configuration section. ConnectionString must come from
/// User Secrets (Development) or environment variables / secret store (Staging, Production).</summary>
public class DatabaseSettings : IDatabaseSettings
{
    public const string SectionName = "DatabaseSettings";
    public string ConnectionString { get; set; } = string.Empty;
    public int CommandTimeoutSeconds { get; set; } = 30;
    public int MaxRetryCount { get; set; } = 3;
}
