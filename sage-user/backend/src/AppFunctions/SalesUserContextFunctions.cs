using System;
using System.Data;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SageCommonLib.AppFunctions;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models.Interfaces;
using SageUserApi.AppFunctions.Interfaces;
using SageUserApi.Models;
using static SageCommonLib.AppFunctions.Sql;

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

        /// <summary>The active sales user behind the token, or null.</summary>
        public async Task<SalesUserContext> GetAsync(Guid _publicId, CancellationToken _ct = default)
        {
            StringBuilder _sqlQuery = new StringBuilder();
            _sqlQuery.Append(" SELECT Id, PublicId, Role, CONCAT(FirstName, N' ', LastName) AS Name, CanRequestCancellation ");
            _sqlQuery.Append(" FROM dbo.SalesUsers ");
            _sqlQuery.Append(" WHERE PublicId = @p AND IsDeleted = 0 AND Status = 'Active' ");

            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                _sqlQuery.ToString(), new[] { P("@p", _publicId) }, "F:SalesUserContext:Get", _ct);
            EnsureRead(_functionReturn);
            DataRow _row = _dataTable.FirstRow();
            if (_row == null) return null;
            return new SalesUserContext
            {
                Id = _row.GetLong("Id"),
                PublicId = _row.GetGuid("PublicId"),
                Role = _row.GetText("Role"),
                Name = _row.GetText("Name"),
                CanRequestCancellation = _row.GetBool("CanRequestCancellation")
            };
        }
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

    }
}
