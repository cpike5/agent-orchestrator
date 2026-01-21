using System.Net;
using System.Net.Mail;
using System.Text;
using Apmas.Server.Configuration;
using Apmas.Server.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Notification service that sends emails via SMTP.
/// </summary>
public class EmailNotificationService : INotificationService
{
    private readonly EmailNotificationOptions _options;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IOptions<NotificationOptions> options,
        ILogger<EmailNotificationService> logger)
    {
        _options = options.Value.Email;
        _logger = logger;
    }

    public async Task SendEscalationAsync(EscalationNotification notification)
    {
        var subject = $"[APMAS] Escalation: Agent '{notification.AgentRole}' needs intervention";
        var body = BuildEscalationBody(notification);

        await SendEmailAsync(subject, body);

        _logger.LogInformation(
            "Escalation email sent for agent {AgentRole}",
            notification.AgentRole);
    }

    public async Task SendProjectCompleteAsync(ProjectState state)
    {
        var subject = $"[APMAS] Project '{state.Name}' completed";
        var body = BuildProjectCompleteBody(state);

        await SendEmailAsync(subject, body);

        _logger.LogInformation(
            "Project complete email sent for {ProjectName}",
            state.Name);
    }

    public async Task SendAlertAsync(string subject, string message)
    {
        var fullSubject = $"[APMAS] {subject}";

        await SendEmailAsync(fullSubject, message);

        _logger.LogInformation("Alert email sent: {Subject}", subject);
    }

    private async Task SendEmailAsync(string subject, string body)
    {
        if (_options.ToAddresses.Count == 0)
        {
            _logger.LogWarning("No email recipients configured. Skipping notification.");
            return;
        }

        try
        {
            using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                EnableSsl = _options.UseSsl
            };

            if (!string.IsNullOrEmpty(_options.Username) && !string.IsNullOrEmpty(_options.Password))
            {
                client.Credentials = new NetworkCredential(_options.Username, _options.Password);
            }

            var from = new MailAddress(_options.FromAddress, _options.FromName);
            using var message = new MailMessage
            {
                From = from,
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            var validRecipientCount = 0;
            foreach (var address in _options.ToAddresses)
            {
                try
                {
                    message.To.Add(address);
                    validRecipientCount++;
                }
                catch (FormatException ex)
                {
                    _logger.LogWarning(ex, "Invalid email address format: {Address}", address);
                }
            }

            if (validRecipientCount == 0)
            {
                _logger.LogWarning("No valid email recipients after validation. Skipping notification.");
                return;
            }

            await client.SendMailAsync(message);
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex,
                "Failed to send email via SMTP. Host: {Host}, Port: {Port}, SSL: {Ssl}",
                _options.SmtpHost,
                _options.SmtpPort,
                _options.UseSsl);
            throw;
        }
    }

    private static string BuildEscalationBody(EscalationNotification notification)
    {
        var sb = new StringBuilder();
        sb.AppendLine("AGENT ESCALATION");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine();
        sb.AppendLine($"Agent Role:    {notification.AgentRole}");
        sb.AppendLine($"Failure Count: {notification.FailureCount}");

        if (!string.IsNullOrEmpty(notification.LastError))
        {
            sb.AppendLine();
            sb.AppendLine("Last Error:");
            sb.AppendLine(notification.LastError);
        }

        if (notification.Checkpoint != null)
        {
            sb.AppendLine();
            sb.AppendLine("Checkpoint Information:");
            sb.AppendLine($"  Summary:  {notification.Checkpoint.Summary}");
            sb.AppendLine($"  Progress: {notification.Checkpoint.CompletedTaskCount}/{notification.Checkpoint.TotalTaskCount} tasks ({notification.Checkpoint.PercentComplete:F1}%)");
            if (!string.IsNullOrEmpty(notification.Checkpoint.Notes))
            {
                sb.AppendLine($"  Notes:    {notification.Checkpoint.Notes}");
            }
        }

        if (notification.Artifacts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Artifacts:");
            foreach (var artifact in notification.Artifacts)
            {
                sb.AppendLine($"  - {artifact}");
            }
        }

        if (!string.IsNullOrEmpty(notification.Context))
        {
            sb.AppendLine();
            sb.AppendLine("Context:");
            sb.AppendLine(notification.Context);
        }

        sb.AppendLine();
        sb.AppendLine(new string('=', 50));
        sb.AppendLine("This is an automated message from APMAS.");

        return sb.ToString();
    }

    private static string BuildProjectCompleteBody(ProjectState state)
    {
        var sb = new StringBuilder();
        var duration = state.CompletedAt.HasValue
            ? state.CompletedAt.Value - state.StartedAt
            : TimeSpan.Zero;

        sb.AppendLine("PROJECT COMPLETE");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine();
        sb.AppendLine($"Project:   {state.Name}");
        sb.AppendLine($"Directory: {state.WorkingDirectory}");
        sb.AppendLine($"Phase:     {state.Phase}");
        sb.AppendLine($"Started:   {state.StartedAt:yyyy-MM-dd HH:mm:ss} UTC");

        if (state.CompletedAt.HasValue)
        {
            sb.AppendLine($"Completed: {state.CompletedAt:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"Duration:  {duration.TotalHours:F1} hours");
        }

        sb.AppendLine();
        sb.AppendLine(new string('=', 50));
        sb.AppendLine("This is an automated message from APMAS.");

        return sb.ToString();
    }
}
