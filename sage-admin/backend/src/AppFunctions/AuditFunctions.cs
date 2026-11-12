using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SageAdminApi.AppFunctions.Interfaces;
using SageCommonLib.AppFunctions.Interfaces;

namespace SageAdminApi.AppFunctions
{
    public class AuditFunctions : IAuditFunctions
    {
        private readonly IDatabaseFunctions _databaseFunctions;
        private readonly ILoggerFunctions _loggerFunctions;

        public AuditFunctions(IDatabaseFunctions __databaseFunctions, ILoggerFunctions __loggerFunctions)
        {
            _databaseFunctions = __databaseFunctions;
            _loggerFunctions = __loggerFunctions;
        }

        /// <summary>
        /// Append an audit row. A failure to audit is logged but never breaks the business operation.
        /// Never pass passwords, hashes or tokens in oldValues/newValues.
        /// </summary>
        public async Task LogAsync(long? _actorUserId, string _action, string _entityType, string _entityId, object _oldValues, object _newValues, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Audit:Log";
            try
            {
                var _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" INSERT dbo.AuditLogs (ActorUserId, Action, EntityType, EntityId, OldValues, NewValues, IpAddress) ");
                _sqlQuery.Append(" VALUES (@actor, @action, @type, @entity, @old, @new, @ip) ");
                var _parameters = new List<SqlParameter>
                {
                    new SqlParameter("@actor", SqlDbType.BigInt) { Value = (object)_actorUserId ?? DBNull.Value },
                    new SqlParameter("@action", SqlDbType.VarChar, 100) { Value = _action },
                    new SqlParameter("@type", SqlDbType.VarChar, 100) { Value = _entityType },
                    new SqlParameter("@entity", SqlDbType.VarChar, 100) { Value = (object)_entityId ?? DBNull.Value },
                    new SqlParameter("@old", SqlDbType.NVarChar, -1) { Value = _oldValues == null ? DBNull.Value : JsonSerializer.Serialize(_oldValues) },
                    new SqlParameter("@new", SqlDbType.NVarChar, -1) { Value = _newValues == null ? DBNull.Value : JsonSerializer.Serialize(_newValues) },
                    new SqlParameter("@ip", SqlDbType.VarChar, 45) { Value = string.IsNullOrEmpty(_ipAddress) ? DBNull.Value : _ipAddress }
                };
                (SageCommonLib.Models.Interfaces.IFunctionReturn _functionReturn, long _id) = await _databaseFunctions.ExecuteInsertAsync(_sqlQuery.ToString(), _parameters, _methodName, null, _ct);
                if (!_functionReturn.Status) _loggerFunctions.Error(_methodName, "Audit write failed: " + string.Join("; ", _functionReturn.Message));
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, "Audit write failed: " + ex.Message, ex);
            }
        }
    }
}
