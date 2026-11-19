using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SageAdminApi.AppValidations;
using SageCommonLib.AppFunctions;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using static SageCommonLib.AppFunctions.Sql;

namespace SageAdminApi.AppFunctions
{
    /// <summary>One change to a sales user, from the Admin Panel or from their manager in the User Panel.</summary>
    public class SalesUserHistoryEntry
    {
        public DateTime AtUtc { get; set; }
        public Guid? SalesUserId { get; set; }
        public string SalesUserName { get; set; }
        public string Username { get; set; }
        /// <summary>Created, Status, Manager, Role, Details, Cancel requests.</summary>
        public string Change { get; set; } = string.Empty;
        /// <summary>e.g. "Active → Inactive", "Kumar Gaurav → Asha Rao".</summary>
        public string Details { get; set; } = string.Empty;
        public string ByName { get; set; }
        /// <summary>"Admin Panel" or "User Panel".</summary>
        public string ByPanel { get; set; } = string.Empty;
    }

    public interface ISalesUserHistoryFunctions
    {
        /// <summary>Changes to sales users, newest first: one user (salesUserId) or everyone (at most 500).</summary>
        Task<(List<SalesUserHistoryEntry> entries, IFunctionReturn functionReturn)> GetHistoryAsync(Guid? _salesUserId, CancellationToken _ct = default);
    }

    /// <summary>
    /// History of sales users read from dbo.AuditLogs (EntityType SalesUser): accounts created, activated / deactivated,
    /// manager and role changes, other edits and cancellation-request permission, whether made by an admin or by the
    /// sales manager in the User Panel. Sign-ins and password changes are left out.
    /// </summary>
    public class SalesUserHistoryFunctions : ISalesUserHistoryFunctions
    {
        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;

        public SalesUserHistoryFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
        }

        public async Task<(List<SalesUserHistoryEntry> entries, IFunctionReturn functionReturn)> GetHistoryAsync(Guid? _salesUserId, CancellationToken _ct = default)
        {
            string _methodName = "F:SalesUserHistory:Get";
            try
            {
                StringBuilder _sqlQuery = new StringBuilder();
                _sqlQuery.Append(" SELECT TOP (500) a.CreatedAtUtc, a.Action, a.EntityId, a.OldValues, a.NewValues, a.ActorUserId, ");
                _sqlQuery.Append("        CONCAT(au.FirstName, N' ', au.LastName) AS AdminName, ");
                _sqlQuery.Append("        su.PublicId AS SalesUserId, CONCAT(su.FirstName, N' ', su.LastName) AS SalesUserName, su.Username ");
                _sqlQuery.Append(" FROM dbo.AuditLogs a ");
                _sqlQuery.Append(" LEFT JOIN dbo.AdminUsers au ON au.Id = a.ActorUserId ");
                _sqlQuery.Append(" LEFT JOIN dbo.SalesUsers su ON su.PublicId = TRY_CAST(a.EntityId AS UNIQUEIDENTIFIER) ");
                _sqlQuery.Append(" WHERE a.EntityType = 'SalesUser' ");
                _sqlQuery.Append("   AND a.Action IN ('Create', 'Update', 'StatusChange', 'CreateTeamMember', 'TeamMemberStatus', 'CancelPermission') ");
                if (_salesUserId.HasValue) _sqlQuery.Append(" AND a.EntityId = @id ");
                _sqlQuery.Append(" ORDER BY a.Id DESC ");

                SqlParameter[] _parameters = _salesUserId.HasValue ? new[] { P("@id", _salesUserId.Value.ToString()) } : null;
                (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(_sqlQuery.ToString(), _parameters, _methodName, _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                var _entries = new List<SalesUserHistoryEntry>();
                foreach (DataRow _row in _dataTable.Rows)
                {
                    string _action = _row.GetText("Action");
                    JsonElement? _old = Parse(_row.GetText("OldValues"));
                    JsonElement? _new = Parse(_row.GetText("NewValues"));
                    bool _fromUserPanel = _row["ActorUserId"] == DBNull.Value;
                    // User Panel rows: { By, SalesUserId, SalesUserName (the manager), Values }.
                    JsonElement? _values = _fromUserPanel && _new.HasValue && _new.Value.TryGetProperty("Values", out JsonElement _v) ? _v : (JsonElement?)null;

                    var _base = new SalesUserHistoryEntry
                    {
                        AtUtc = _row.GetUtc("CreatedAtUtc"),
                        SalesUserId = _row.GetNullableGuid("SalesUserId"),
                        SalesUserName = _row.GetText("SalesUserName"),
                        Username = _row.GetText("Username"),
                        ByName = _fromUserPanel ? Text(_new, "SalesUserName") : _row.GetText("AdminName"),
                        ByPanel = _fromUserPanel ? "User Panel" : "Admin Panel"
                    };

                    foreach ((string _change, string _details) in Describe(_action, _old, _new, _values))
                    {
                        _entries.Add(new SalesUserHistoryEntry
                        {
                            AtUtc = _base.AtUtc, SalesUserId = _base.SalesUserId, SalesUserName = _base.SalesUserName, Username = _base.Username,
                            ByName = _base.ByName, ByPanel = _base.ByPanel, Change = _change, Details = _details
                        });
                    }
                }
                return (_entries, _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        /// <summary>Turns one audit row into the changes it records (an edit can change manager and role at once).</summary>
        private static IEnumerable<(string change, string details)> Describe(string _action, JsonElement? _old, JsonElement? _new, JsonElement? _values)
        {
            switch (_action)
            {
                case "Create":
                {
                    string _role = Role(Text(_new, "Role"));
                    string _manager = Text(_new, "ManagerName");
                    yield return ("Created", _manager == null ? _role : $"{_role}, manager {_manager}");
                    break;
                }
                case "CreateTeamMember":
                    yield return ("Created", "Sales person, added to the team by their manager");
                    break;
                case "StatusChange":
                {
                    string _from = Text(_old, "Status"), _to = Text(_new, "Status");
                    if (_from != _to) yield return ("Status", $"{_from} → {_to}");
                    else if (Text(_old, "IsLockedOut") == "true" && Text(_new, "IsLockedOut") == "false") yield return ("Status", "Unlocked");
                    break;
                }
                case "TeamMemberStatus":
                    yield return ("Status", $"{Text(_values, "From")} → {Text(_values, "To")}");
                    break;
                case "CancelPermission":
                    yield return ("Cancel requests", Text(_values, "CanRequestCancellation") == "true" ? "Allowed to request cancellations" : "No longer allowed to request cancellations");
                    break;
                case "Update":
                {
                    bool _any = false;
                    string _oldManager = Text(_old, "ManagerName"), _newManager = Text(_new, "ManagerName");
                    if (_oldManager != _newManager) { _any = true; yield return ("Manager", $"{_oldManager ?? "none"} → {_newManager ?? "none"}"); }
                    string _oldRole = Text(_old, "Role"), _newRole = Text(_new, "Role");
                    if (_oldRole != _newRole) { _any = true; yield return ("Role", $"{Role(_oldRole)} → {Role(_newRole)}"); }
                    string[] _fields = { "FirstName", "LastName", "Email", "MobileNumber" };
                    string[] _changed = _fields.Where(f => Text(_old, f) != Text(_new, f)).ToArray();
                    if (_changed.Length > 0 || !_any)
                        yield return ("Details", _changed.Length > 0 ? "Changed: " + string.Join(", ", _changed.Select(Label)) : "Saved without changes");
                    break;
                }
            }
        }

        private static string Role(string _role) => _role == "SalesManager" ? "Sales manager" : _role == "SalesPerson" ? "Sales person" : _role;

        private static string Label(string _field) => _field switch
        {
            "FirstName" => "first name", "LastName" => "last name", "MobileNumber" => "mobile", "Email" => "email", _ => _field
        };

        private static JsonElement? Parse(string _json)
        {
            if (string.IsNullOrWhiteSpace(_json)) return null;
            try { return JsonDocument.Parse(_json).RootElement.Clone(); }
            catch (JsonException) { return null; }
        }

        /// <summary>A property as text (true/false for booleans), or null when missing or null.</summary>
        private static string Text(JsonElement? _element, string _name)
        {
            if (!_element.HasValue || _element.Value.ValueKind != JsonValueKind.Object || !_element.Value.TryGetProperty(_name, out JsonElement _p)) return null;
            return _p.ValueKind switch
            {
                JsonValueKind.String => _p.GetString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => _p.ToString()
            };
        }
    }
}
