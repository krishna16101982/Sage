using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.Models;
using SageCommonLib.AppFunctions.Interfaces;

namespace SageAdminApi.AppFunctions
{
    /// <summary>
    /// Placeholder delivery until an email provider (SMTP) is configured. In Development it logs the reset link so the flow
    /// can be tested; in other environments it only logs that a reset was requested (the token is never logged).
    /// </summary>
    public class LogPasswordResetNotifier : IPasswordResetNotifier
    {
        private readonly ILoggerFunctions _loggerFunctions;
        private readonly IHostEnvironment _environment;
        private readonly AuthSettings _authSettings;

        public LogPasswordResetNotifier(ILoggerFunctions __loggerFunctions, IHostEnvironment __environment, IOptions<AuthSettings> __authSettings)
        {
            _loggerFunctions = __loggerFunctions;
            _environment = __environment;
            _authSettings = __authSettings.Value;
        }

        public Task SendAsync(string _email, string _firstName, string _rawToken, DateTime _expiresAtUtc, CancellationToken _ct = default)
        {
            string _methodName = "F:PasswordResetNotifier:Send";
            if (_environment.IsDevelopment())
            {
                string _link = $"{_authSettings.FrontendBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(_rawToken)}";
                _loggerFunctions.Information(_methodName, $"[DEV ONLY] Password reset link for {_email} (expires {_expiresAtUtc:u}): {_link}");
            }
            else
            {
                _loggerFunctions.Warning(_methodName, "Password reset requested but no email provider is configured; nothing was sent.");
            }
            return Task.CompletedTask;
        }
    }
}
