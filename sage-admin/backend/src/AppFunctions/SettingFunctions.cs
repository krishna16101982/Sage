using System;
using System.Collections.Generic;
using System.Globalization;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.AppValidations;
using SageAdminApi.Globals;
using SageAdminApi.Models;
using SageAdminApi.Models.HttpPost;
using SageCommonLib.AppFunctions;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.AppValidations;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SP = SageAdminApi.Globals.ApplicationDatabaseConstants.StoredProcedures;
using static SageCommonLib.AppFunctions.Sql;

namespace SageAdminApi.AppFunctions
{
    /// <summary>
    /// Settings screen. Each setting carries its own type and limits (dbo.Settings), so a new value is checked against them
    /// here; only the value can change. Every change is audited with the old and new value.
    /// </summary>
    public class SettingFunctions : ISettingFunctions
    {
        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;
        private readonly IAuditFunctions _auditFunctions;

        public SettingFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions, IAuditFunctions __auditFunctions)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
            _auditFunctions = __auditFunctions;
        }

        #region Helpers

        private static SettingDto Map(DataRow _row) => new SettingDto
        {
            Key = _row.GetText("SettingKey"),
            Value = _row.GetText("SettingValue"),
            ValueType = _row.GetText("ValueType"),
            Category = _row.GetText("Category"),
            Label = _row.GetText("Label"),
            Description = _row.GetText("Description"),
            MinValue = _row.GetNullableInt("MinValue"),
            MaxValue = _row.GetNullableInt("MaxValue"),
            MaxLength = _row.GetNullableInt("MaxLength"),
            Pattern = _row.GetText("Pattern"),
            UpdatedAtUtc = _row.GetNullableUtc("UpdatedAtUtc"),
            UpdatedByName = _row.GetText("UpdatedByName")
        };

        /// <summary>Every setting (key = null) or one setting, with who changed it last.</summary>
        private async Task<(IFunctionReturn functionReturn, List<SettingDto> settings)> ReadSettingsAsync(string _key, string _methodName, CancellationToken _ct)
        {
            StringBuilder _sqlQuery = new StringBuilder();
            _sqlQuery.Append(" SELECT s.SettingKey, s.SettingValue, s.ValueType, s.Category, ISNULL(s.Label, s.SettingKey) AS Label, s.Description, ");
            _sqlQuery.Append("        s.MinValue, s.MaxValue, s.MaxLength, s.Pattern, s.UpdatedAtUtc, ");
            _sqlQuery.Append("        CASE WHEN u.Id IS NULL THEN NULL ELSE CONCAT(u.FirstName, N' ', u.LastName) END AS UpdatedByName ");
            _sqlQuery.Append(" FROM dbo.Settings s ");
            _sqlQuery.Append(" LEFT JOIN dbo.AdminUsers u ON u.Id = s.UpdatedBy ");
            if (_key != null) _sqlQuery.Append(" WHERE s.SettingKey = @k ");
            _sqlQuery.Append(" ORDER BY s.Category, s.SortOrder, s.SettingKey ");

            (IFunctionReturn _functionReturn, DataTable _dataTable) = await _databaseFunctions.ExecuteSelectDataTableAsync(
                _sqlQuery.ToString(), _key == null ? null : new[] { P("@k", _key) }, _methodName, _ct);
            return (_functionReturn, _dataTable.MapRows(Map));
        }

        private static IFunctionReturn Invalid(string _message, string _methodName)
            => ApplicationCommonFunctions.BadRequest(_message, _methodName);

        /// <summary>Checks a new value against the setting's type and limits; returns the value to store, or an error.</summary>
        internal static (string value, string error) Check(SettingDto _setting, string _input)
        {
            string _value = (_input ?? string.Empty).Trim();
            switch (_setting.ValueType)
            {
                case "int":
                    if (!int.TryParse(_value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int _number))
                        return (null, $"{_setting.Label} must be a whole number.");
                    if (_setting.MinValue.HasValue && _number < _setting.MinValue.Value || _setting.MaxValue.HasValue && _number > _setting.MaxValue.Value)
                        return (null, $"{_setting.Label} must be between {_setting.MinValue ?? int.MinValue} and {_setting.MaxValue ?? int.MaxValue}.");
                    return (_number.ToString(CultureInfo.InvariantCulture), null);
                case "bool":
                    if (!bool.TryParse(_value, out bool _flag)) return (null, $"{_setting.Label} must be true or false.");
                    return (_flag ? "true" : "false", null);
                case "json":
                    try { using var _doc = System.Text.Json.JsonDocument.Parse(_value); }
                    catch (System.Text.Json.JsonException) { return (null, $"{_setting.Label} must be valid JSON."); }
                    return (_value, null);
                default:
                    _value = Sanitization.Sanitize(_value) ?? string.Empty;
                    int _max = Math.Min(_setting.MaxLength ?? 2000, 2000);
                    if (_value.Length > _max) return (null, $"{_setting.Label} must be {_max} characters or fewer.");
                    if (!string.IsNullOrEmpty(_setting.Pattern) && !Regex.IsMatch(_value, _setting.Pattern, RegexOptions.None, TimeSpan.FromSeconds(1)))
                        return (null, $"{_setting.Label} is not in the right format. {_setting.Description}");
                    return (_value, null);
            }
        }

        #endregion

        public async Task<(List<SettingDto> settings, IFunctionReturn functionReturn)> GetSettingsAsync(CancellationToken _ct = default)
        {
            string _methodName = "F:Setting:GetSettings";
            try
            {
                (IFunctionReturn _functionReturn, List<SettingDto> _settings) = await ReadSettingsAsync(null, _methodName, _ct);
                return _functionReturn.Status ? (_settings, _functionReturn) : (null, _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }

        public async Task<(SettingDto setting, IFunctionReturn functionReturn)> UpdateSettingAsync(string _key, HttpSettingPutParams _params, long? _actorUserId, string _ipAddress, CancellationToken _ct = default)
        {
            string _methodName = "F:Setting:UpdateSetting";
            try
            {
                (IFunctionReturn _readReturn, List<SettingDto> _found) = await ReadSettingsAsync(_key ?? string.Empty, _methodName, _ct);
                if (!_readReturn.Status) return (null, _readReturn);
                SettingDto _existing = _found.Count > 0 ? _found[0] : null;
                if (_existing == null) return (null, ApplicationCommonFunctions.NotFound("Setting not found.", _methodName));

                (string _value, string _error) = Check(_existing, _params?.Value);
                if (_error != null) return (null, Invalid(_error, _methodName));

                (IFunctionReturn _functionReturn, long _) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.SettingUpdate, new[]
                {
                    P("@SettingKey", _existing.Key), P("@SettingValue", _value), P("@ActorUserId", _actorUserId)
                }, _methodName, "Setting saved.", _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                (IFunctionReturn _reread, List<SettingDto> _after) = await ReadSettingsAsync(_existing.Key, _methodName, _ct);
                EnsureRead(_reread);
                SettingDto _updated = _after[0];
                await _auditFunctions.LogAsync(_actorUserId, ApplicationEnumeration.AuditAction.Update.ToString(), ApplicationEnumeration.ObjectType.Setting.ToString(),
                    _existing.Key, new { _existing.Label, Value = _existing.Value }, new { _updated.Label, Value = _updated.Value }, _ipAddress, _ct);
                return (_updated, _functionReturn);
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                return (null, ApplicationCommonFunctions.SystemError(_methodName));
            }
        }
    }
}
