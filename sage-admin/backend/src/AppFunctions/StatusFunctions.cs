using System;
using System.Threading;
using System.Threading.Tasks;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.AppValidations;
using SageAdminApi.Globals;
using SageAdminApi.Models;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;

namespace SageAdminApi.AppFunctions
{
    public class StatusFunctions : IStatusFunctions
    {
        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IDatabaseFunctions _databaseFunctions;

        /// <summary>
        /// Constructor
        /// </summary>
        public StatusFunctions(ILoggerFunctions __loggerFunctions, IDatabaseFunctions __databaseFunctions)
        {
            _loggerFunctions = __loggerFunctions;
            _databaseFunctions = __databaseFunctions;
        }

        /// <summary>
        /// Get service status including a database ping.
        /// </summary>
        /// <returns>ServiceStatus serviceStatus, IFunctionReturn functionReturn</returns>
        public async Task<(ServiceStatus serviceStatus, IFunctionReturn functionReturn)> GetStatusAsync(string _project, string _projectVersion, CancellationToken _ct = default)
        {
            #region Local Variables
            string _methodName = "F:Status:GetStatus";
            IFunctionReturn _functionReturn = new FunctionReturn();
            ServiceStatus _serviceStatus = new ServiceStatus();
            #endregion

            try
            {
                string _platformVersion = (Environment.GetEnvironmentVariable(ApplicationConstants.Environment.PlatformVersionVariable) + string.Empty).Trim();
                _serviceStatus.Host = Environment.MachineName;
                _serviceStatus.Project = _project;
                _serviceStatus.ProjectVersion = _projectVersion;
                _serviceStatus.PlatformVersion = _platformVersion == string.Empty ? "N/A" : _platformVersion;

                bool _databaseOk = await _databaseFunctions.PingAsync(_ct);
                _serviceStatus.Database = _databaseOk ? "ok" : "unavailable";
                if (!_databaseOk)
                {
                    _serviceStatus.Status = "degraded";
                    _functionReturn = FunctionReturn.Fail(Constants.ReturnMessageTypes.SystemError, "Database unavailable.", "503", _methodName);
                }
            }
            catch (Exception ex)
            {
                _loggerFunctions.Error(_methodName, ex.Message, ex);
                _functionReturn = ApplicationCommonFunctions.SystemError(_methodName);
            }
            return (_serviceStatus, _functionReturn);
        }
    }
}
