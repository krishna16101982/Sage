using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.Models;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Email;

namespace SageAdminApi.AppFunctions
{
    /// <summary>
    /// Emails the password-reset link (used when the "Email" settings are filled in). The email is queued and sent in the
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
            EmailMessage _message = PasswordResetEmail.Build("Sage Admin", _email, _firstName, _rawToken, _expiresAtUtc, _authSettings.FrontendBaseUrl, DateTime.UtcNow);
            if (!_queue.TryQueue(_message))
                _loggerFunctions.Warning("F:PasswordResetNotifier:Send", "The email queue is full; a password reset email was not sent.");
            return Task.CompletedTask;
        }
    }
}
