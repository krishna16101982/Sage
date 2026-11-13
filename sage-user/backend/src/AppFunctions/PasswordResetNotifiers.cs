using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Email;
using SageUserApi.AppFunctions.Interfaces;
using SageUserApi.Models;

namespace SageUserApi.AppFunctions
{
    /// <summary>
    /// Emails the User Panel reset link (used when the "Email" settings are filled in). The email is queued and sent in the
    /// background, so the request does not wait for the mail server. The token is never logged.
    /// </summary>
    public class EmailPasswordResetNotifier : IPasswordResetNotifier
    {
        private readonly IEmailQueue _queue;
        private readonly ILoggerFunctions _loggerFunctions;
        private readonly AuthSettings _authSettings;

        public EmailPasswordResetNotifier(IEmailQueue __queue, ILoggerFunctions __loggerFunctions, IOptions<AuthSettings> __authSettings)
        {
            _queue = __queue;
            _loggerFunctions = __loggerFunctions;
            _authSettings = __authSettings.Value;
        }

        public Task SendAsync(string _email, string _firstName, string _rawToken, DateTime _expiresAtUtc, CancellationToken _ct = default)
        {
            EmailMessage _message = PasswordResetEmail.Build("Sage Sales", _email, _firstName, _rawToken, _expiresAtUtc, _authSettings.FrontendBaseUrl, DateTime.UtcNow);
            if (!_queue.TryQueue(_message))
                _loggerFunctions.Warning("F:PasswordResetNotifier:Send", "The email queue is full; a password reset email was not sent.");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Used when no email is configured. In Development it logs the reset link so the flow can be tried; elsewhere it only
    /// logs that nothing was sent (the token is never logged outside Development).
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
                _loggerFunctions.Information(_methodName, $"[DEV ONLY] Password reset link for {EmailBackgroundService.MaskEmail(_email)} (expires {_expiresAtUtc:u}): {_link}");
            }
            else
            {
                _loggerFunctions.Warning(_methodName, "Password reset requested but no email provider is configured; nothing was sent.");
            }
            return Task.CompletedTask;
        }
    }
}
