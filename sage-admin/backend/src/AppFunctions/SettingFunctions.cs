using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.AppValidations;
using SageAdminApi.Globals;
using SageAdminApi.Models;
using SageAdminApi.Models.HttpPost;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.AppValidations;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SP = SageAdminApi.Globals.ApplicationDatabaseConstants.StoredProcedures;

namespace SageAdminApi.AppFunctions
{
    /// <summary>
    /// Settings screen. Each setting carries its own type and limits (dbo.Settings), so a new value is checked against them
    /// here; only the value can change. Every change is audited with the old and new value.
    /// </summary>
    public class SettingFunctions : ISettingFunctions
    {
        private const string SettingSelect = @"SELECT s.SettingKey, s.SettingValue, s.ValueType, s.Category, ISNULL(s.Label, s.SettingKey), s.Description,
    s.MinValue, s.MaxValue, s.MaxLength, s.Pattern, s.UpdatedAtUtc, CASE WHEN u.Id IS NULL THEN NULL ELSE CONCAT(u.FirstName, N' ', u.LastName) END
FROM dbo.Settings s LEFT JOIN dbo.AdminUsers u ON u.Id = s.UpdatedBy";

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

        private static SqlParameter P(string _name, object _value) => new SqlParameter(_name, _value ?? DBNull.Value);
        private static string S(SqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
        private static int? I(SqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetInt32(i);

        private static SettingDto Map(SqlDataReader r) => new SettingDto
        {
            Key = r.GetString(0),
            Value = S(r, 1),
            ValueType = r.GetString(2),
            Category = r.GetString(3),
            Label = r.GetString(4),
            Description = S(r, 5),
            MinValue = I(r, 6),
            MaxValue = I(r, 7),
            MaxLength = I(r, 8),
            Pattern = S(r, 9),
            UpdatedAtUtc = r.IsDBNull(10) ? null : DateTime.SpecifyKind(r.GetDateTime(10), DateTimeKind.Utc),
            UpdatedByName = S(r, 11)
        };

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
                var _settings = await _databaseFunctions.QueryAsync(SettingSelect + " ORDER BY s.Category, s.SortOrder, s.SettingKey", Map, null, _ct);
                return (_settings.ToList(), new FunctionReturn { MethodName = _methodName });
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
                SettingDto _existing = await _databaseFunctions.QuerySingleOrDefaultAsync(SettingSelect + " WHERE s.SettingKey = @k", Map, new[] { P("@k", _key) }, _ct);
                if (_existing == null) return (null, ApplicationCommonFunctions.NotFound("Setting not found.", _methodName));

                (string _value, string _error) = Check(_existing, _params?.Value);
                if (_error != null) return (null, Invalid(_error, _methodName));

                (IFunctionReturn _functionReturn, long _) = await _databaseFunctions.ExecuteProcedureWriteAsync(SP.SettingUpdate, new[]
                {
                    P("@SettingKey", _existing.Key), P("@SettingValue", _value), P("@ActorUserId", _actorUserId)
                }, _methodName, "Setting saved.", _ct);
                if (!_functionReturn.Status) return (null, _functionReturn);

                SettingDto _updated = await _databaseFunctions.QuerySingleOrDefaultAsync(SettingSelect + " WHERE s.SettingKey = @k", Map, new[] { P("@k", _key) }, _ct);
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
