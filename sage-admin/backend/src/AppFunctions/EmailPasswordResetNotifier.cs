using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.Models;
using SageCommonLib.AppFunctions.Interfaces;

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
            EmailMessage _message = Build(_email, _firstName, _rawToken, _expiresAtUtc, _authSettings.FrontendBaseUrl, DateTime.UtcNow);
            if (!_queue.TryQueue(_message))
                _loggerFunctions.Warning("F:PasswordResetNotifier:Send", "The email queue is full; a password reset email was not sent.");
            return Task.CompletedTask;
        }

        /// <summary>The reset email: the link, how long it is valid, and what to do if the user did not ask for it.</summary>
        public static EmailMessage Build(string _email, string _firstName, string _rawToken, DateTime _expiresAtUtc, string _frontendBaseUrl, DateTime _nowUtc)
        {
            string _link = $"{_frontendBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(_rawToken)}";
            int _minutes = Math.Max(1, (int)Math.Round((_expiresAtUtc - _nowUtc).TotalMinutes));
            string _name = string.IsNullOrWhiteSpace(_firstName) ? "there" : _firstName.Trim();

            string _text =
                $"Hello {_name},\r\n\r\n" +
                "We received a request to reset your Sage Admin password. Open this link to choose a new one:\r\n\r\n" +
                $"{_link}\r\n\r\n" +
                $"The link works once and expires in {_minutes} minutes.\r\n\r\n" +
                "If you did not ask for this, you can ignore this email; your password stays the same.\r\n\r\n" +
                "Sage Admin";

            string _safeName = WebUtility.HtmlEncode(_name);
            string _safeLink = WebUtility.HtmlEncode(_link);
            string _html =
                "<!DOCTYPE html><html><body style=\"margin:0;padding:24px;background:#eceff4;font-family:Segoe UI,Arial,sans-serif;color:#2a3f54\">" +
                "<div style=\"max-width:520px;margin:0 auto;background:#ffffff;border-radius:6px;padding:28px\">" +
                "<h2 style=\"margin:0 0 16px;font-size:20px\">Reset your password</h2>" +
                $"<p>Hello {_safeName},</p>" +
                "<p>We received a request to reset your Sage Admin password. Click the button to choose a new one.</p>" +
                $"<p style=\"margin:24px 0\"><a href=\"{_safeLink}\" style=\"background:#1abb9c;color:#ffffff;text-decoration:none;padding:10px 18px;border-radius:4px;display:inline-block\">Choose a new password</a></p>" +
                $"<p style=\"font-size:13px;color:#73879c\">The link works once and expires in {_minutes} minutes. If the button does not work, copy this address into your browser:<br>{_safeLink}</p>" +
                "<p style=\"font-size:13px;color:#73879c\">If you did not ask for this, you can ignore this email; your password stays the same.</p>" +
                "</div></body></html>";

            return new EmailMessage(_email, "Reset your Sage Admin password", _text, _html);
        }
    }
}
