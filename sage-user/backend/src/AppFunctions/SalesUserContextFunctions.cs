using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SageCommonLib.AppFunctions.Interfaces;
using SageUserApi.AppFunctions.Interfaces;
using SageUserApi.Models;

namespace SageUserApi.AppFunctions
{
    /// <summary>Turns the token's subject (public id) into the internal id and role the sales functions work with.</summary>
    public class SalesUserContextFunctions : ISalesUserContextFunctions
    {
        private readonly IDatabaseFunctions _databaseFunctions;

        public SalesUserContextFunctions(IDatabaseFunctions __databaseFunctions)
        {
            _databaseFunctions = __databaseFunctions;
        }

        public Task<SalesUserContext> GetAsync(Guid _publicId, CancellationToken _ct = default)
            => _databaseFunctions.QuerySingleOrDefaultAsync(
                "SELECT Id, PublicId, Role, CONCAT(FirstName, N' ', LastName) FROM dbo.SalesUsers WHERE PublicId = @p AND IsDeleted = 0 AND Status = 'Active'",
                r => new SalesUserContext { Id = r.GetInt64(0), PublicId = r.GetGuid(1), Role = r.GetString(2), Name = r.GetString(3) },
                new[] { new SqlParameter("@p", _publicId) }, _ct);
    }

    /// <summary>Audit rows for sales users' own actions. ActorUserId stays NULL (it points at admin users); the seller is in NewValues.</summary>
    internal static class SalesAudit
    {
        public static async Task LogAsync(IDatabaseFunctions _db, ILoggerFunctions _logger, string _action, string _entityType, Guid _entityId,
            SalesUserContext _user, object _values, string _ipAddress, CancellationToken _ct)
        {
            try
            {
                string _json = JsonSerializer.Serialize(new { By = "SalesUser", SalesUserId = _user.PublicId, SalesUserName = _user.Name, Values = _values });
                await _db.ExecuteInsertAsync(
                    "INSERT dbo.AuditLogs (ActorUserId, Action, EntityType, EntityId, NewValues, IpAddress) VALUES (NULL, @a, @t, @e, @n, @ip)",
                    new[]
                    {
                        new SqlParameter("@a", _action), new SqlParameter("@t", _entityType), new SqlParameter("@e", _entityId.ToString()),
                        new SqlParameter("@n", _json), new SqlParameter("@ip", (object)Truncate(_ipAddress, 45) ?? DBNull.Value)
                    },
                    "F:SalesAudit:Log", null, _ct);
            }
            catch (Exception ex)
            {
                _logger.Error("F:SalesAudit:Log", "Audit write failed: " + ex.Message, ex);
            }
        }

        private static string Truncate(string _value, int _max) => string.IsNullOrEmpty(_value) || _value.Length <= _max ? _value : _value.Substring(0, _max);
    }
}
