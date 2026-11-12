using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace SageAdminApi.Tests.Infrastructure;

/// <summary>Executes the version-controlled .sql files (splitting on GO) the same way deployment does.</summary>
public static partial class SqlScriptRunner
{
    private static readonly string[] Order = { "DDL", "Views", "Functions", "StoredProcedures", "SeedData" };

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoRegex();

    public static string DatabaseFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "database", "DDL");
            if (Directory.Exists(candidate)) return Path.Combine(dir.FullName, "database");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("backend/database folder not found.");
    }

    public static IEnumerable<string> ScriptFiles(params string[] folders)
    {
        var root = DatabaseFolder();
        foreach (var f in folders)
        {
            var path = Path.Combine(root, f);
            if (!Directory.Exists(path)) continue;
            foreach (var file in Directory.GetFiles(path, "*.sql").OrderBy(x => x, StringComparer.Ordinal)) yield return file;
        }
    }

    public static async Task ApplyAllAsync(string connectionString) => await ApplyAsync(connectionString, Order);

    public static async Task ApplyAsync(string connectionString, params string[] folders)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        foreach (var file in ScriptFiles(folders))
            foreach (var batch in GoRegex().Split(await File.ReadAllTextAsync(file)).Where(b => !string.IsNullOrWhiteSpace(b)))
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = batch;
                await cmd.ExecuteNonQueryAsync();
            }
    }
}
