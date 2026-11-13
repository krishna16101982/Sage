using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SageCommonLib.Email
{
    /// <summary>One email to send: a plain-text body and an HTML body with the same content.</summary>
    public sealed record EmailMessage(string To, string Subject, string TextBody, string HtmlBody);

    /// <summary>Delivers one email (SMTP in the app; tests swap in a fake).</summary>
    public interface IEmailTransport
    {
        Task SendAsync(EmailMessage _message, CancellationToken _ct = default);
    }

    /// <summary>
    /// Emails waiting to be sent. Requests only add to the queue, so they return at once and take the same time whether or
    /// not an email is sent (a "forgot password" call does not reveal which addresses have an account).
    /// </summary>
    public interface IEmailQueue
    {
        bool TryQueue(EmailMessage _message);
        ChannelReader<EmailMessage> Reader { get; }
    }

    public sealed class EmailQueue : IEmailQueue
    {
        // Bounded so a flood of requests cannot use up memory; when full, new emails are dropped (and logged by the caller).
        private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true
        });

        public bool TryQueue(EmailMessage _message) => _channel.Writer.TryWrite(_message);
        public ChannelReader<EmailMessage> Reader => _channel.Reader;
    }

    /// <summary>Sends over SMTP with the "Email" settings (STARTTLS on 587 by default).</summary>
    public sealed class SmtpEmailTransport : IEmailTransport
    {
        private readonly EmailSettings _settings;

        public SmtpEmailTransport(IOptions<EmailSettings> __settings)
        {
            _settings = __settings.Value;
        }

        public async Task SendAsync(EmailMessage _message, CancellationToken _ct = default)
        {
            using var _mail = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, _settings.FromName),
                Subject = _message.Subject,
                SubjectEncoding = Encoding.UTF8,
                Body = _message.TextBody,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = false
            };
            _mail.To.Add(new MailAddress(_message.To));
            _mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(_message.HtmlBody, Encoding.UTF8, "text/html"));

            using var _client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = Math.Max(5, _settings.TimeoutSeconds) * 1000,
                UseDefaultCredentials = false
            };
            if (!string.IsNullOrEmpty(_settings.UserName))
                _client.Credentials = new NetworkCredential(_settings.UserName, _settings.Password);

            await _client.SendMailAsync(_mail, _ct);
        }
    }

    /// <summary>Takes emails off the queue and sends them, trying each one up to three times. Failures are logged, never thrown.</summary>
    public sealed class EmailBackgroundService : BackgroundService
    {
        private static readonly TimeSpan[] RetryDelays = { TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30) };

        private readonly IEmailQueue _queue;
        private readonly IEmailTransport _transport;
        private readonly ILogger<EmailBackgroundService> _logger;

        public EmailBackgroundService(IEmailQueue __queue, IEmailTransport __transport, ILogger<EmailBackgroundService> __logger)
        {
            _queue = __queue;
            _transport = __transport;
            _logger = __logger;
        }

        protected override async Task ExecuteAsync(CancellationToken _stoppingToken)
        {
            try
            {
                await foreach (EmailMessage _message in _queue.Reader.ReadAllAsync(_stoppingToken))
                {
                    await SendWithRetryAsync(_message, _stoppingToken);
                }
            }
            catch (OperationCanceledException) when (_stoppingToken.IsCancellationRequested)
            {
                // Shutting down.
            }
        }

        private async Task SendWithRetryAsync(EmailMessage _message, CancellationToken _ct)
        {
            for (int _attempt = 0; ; _attempt++)
            {
                try
                {
                    await _transport.SendAsync(_message, _ct);
                    _logger.LogInformation("Email \"{Subject}\" sent to {Recipient}", _message.Subject, MaskEmail(_message.To));
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    if (_attempt >= RetryDelays.Length)
                    {
                        _logger.LogError(ex, "Email \"{Subject}\" to {Recipient} could not be sent after {Attempts} attempts", _message.Subject, MaskEmail(_message.To), _attempt + 1);
                        return;
                    }
                    _logger.LogWarning("Email \"{Subject}\" to {Recipient} failed ({Error}); trying again", _message.Subject, MaskEmail(_message.To), ex.Message);
                    await Task.Delay(RetryDelays[_attempt], _ct);
                }
            }
        }

        /// <summary>"kumar@example.com" becomes "k***@example.com" in the logs.</summary>
        public static string MaskEmail(string? _email)
        {
            int _at = _email?.IndexOf('@') ?? -1;
            return _at <= 0 || _email == null ? "***" : _email[0] + "***" + _email[_at..];
        }
    }

    public static class EmailServiceCollectionExtensions
    {
        /// <summary>
        /// Binds the "Email" settings and, when they are filled in, registers the queue, the SMTP transport and the background
        /// sender. Returns true when email is configured (the caller then registers its email-based notifiers).
        /// </summary>
        public static bool AddSageEmail(this IServiceCollection _services, IConfiguration _configuration)
        {
            IConfigurationSection _section = _configuration.GetSection(EmailSettings.SectionName);
            _services.Configure<EmailSettings>(_section);
            if (_section.Get<EmailSettings>()?.IsConfigured != true) return false;
            _services.AddSingleton<IEmailQueue, EmailQueue>();
            _services.AddSingleton<IEmailTransport, SmtpEmailTransport>();
            _services.AddHostedService<EmailBackgroundService>();
            return true;
        }
    }

    /// <summary>The "reset your password" email, shared by both panels (only the product name differs).</summary>
    public static class PasswordResetEmail
    {
        /// <summary>The link, how long it is valid, and what to do if the user did not ask for it. The name is HTML-encoded.</summary>
        public static EmailMessage Build(string _productName, string _email, string? _firstName, string _rawToken, DateTime _expiresAtUtc, string _frontendBaseUrl, DateTime _nowUtc)
        {
            string _link = $"{_frontendBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(_rawToken)}";
            int _minutes = Math.Max(1, (int)Math.Round((_expiresAtUtc - _nowUtc).TotalMinutes));
            string _name = string.IsNullOrWhiteSpace(_firstName) ? "there" : _firstName.Trim();

            string _text =
                $"Hello {_name},\r\n\r\n" +
                $"We received a request to reset your {_productName} password. Open this link to choose a new one:\r\n\r\n" +
                $"{_link}\r\n\r\n" +
                $"The link works once and expires in {_minutes} minutes.\r\n\r\n" +
                "If you did not ask for this, you can ignore this email; your password stays the same.\r\n\r\n" +
                _productName;

            string _safeName = WebUtility.HtmlEncode(_name);
            string _safeLink = WebUtility.HtmlEncode(_link);
            string _safeProduct = WebUtility.HtmlEncode(_productName);
            string _html =
                "<!DOCTYPE html><html><body style=\"margin:0;padding:24px;background:#eceff4;font-family:Segoe UI,Arial,sans-serif;color:#2a3f54\">" +
                "<div style=\"max-width:520px;margin:0 auto;background:#ffffff;border-radius:6px;padding:28px\">" +
                "<h2 style=\"margin:0 0 16px;font-size:20px\">Reset your password</h2>" +
                $"<p>Hello {_safeName},</p>" +
                $"<p>We received a request to reset your {_safeProduct} password. Click the button to choose a new one.</p>" +
                $"<p style=\"margin:24px 0\"><a href=\"{_safeLink}\" style=\"background:#1abb9c;color:#ffffff;text-decoration:none;padding:10px 18px;border-radius:4px;display:inline-block\">Choose a new password</a></p>" +
                $"<p style=\"font-size:13px;color:#73879c\">The link works once and expires in {_minutes} minutes. If the button does not work, copy this address into your browser:<br>{_safeLink}</p>" +
                "<p style=\"font-size:13px;color:#73879c\">If you did not ask for this, you can ignore this email; your password stays the same.</p>" +
                "</div></body></html>";

            return new EmailMessage(_email, $"Reset your {_productName} password", _text, _html);
        }
    }
}
