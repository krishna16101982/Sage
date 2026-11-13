using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SageAdminApi.Models;

namespace SageAdminApi.AppFunctions
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
        public static string MaskEmail(string _email)
        {
            int _at = _email?.IndexOf('@') ?? -1;
            return _at <= 0 ? "***" : _email[0] + "***" + _email[_at..];
        }
    }
}
