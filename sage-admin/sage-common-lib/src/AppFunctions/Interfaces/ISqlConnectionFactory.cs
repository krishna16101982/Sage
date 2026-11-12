using Microsoft.Data.SqlClient;

namespace SageCommonLib.AppFunctions.Interfaces;

/// <summary>Connection initialization lives here and only here.</summary>
public interface ISqlConnectionFactory
{
    /// <summary>Creates and opens a connection.</summary>
    Task<SqlConnection> OpenConnectionAsync(CancellationToken ct = default);
}
