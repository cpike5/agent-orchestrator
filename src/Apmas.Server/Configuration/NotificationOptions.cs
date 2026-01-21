namespace Apmas.Server.Configuration;

/// <summary>
/// Configuration options for the notification service.
/// </summary>
public class NotificationOptions
{
    public const string SectionName = "Apmas:Notifications";

    /// <summary>
    /// The notification provider to use: Console, Email, or Slack.
    /// </summary>
    public NotificationProvider Provider { get; set; } = NotificationProvider.Console;

    /// <summary>
    /// Email notification settings.
    /// </summary>
    public EmailNotificationOptions Email { get; set; } = new();

    /// <summary>
    /// Slack notification settings.
    /// </summary>
    public SlackNotificationOptions Slack { get; set; } = new();
}

/// <summary>
/// Available notification providers.
/// </summary>
public enum NotificationProvider
{
    Console,
    Email,
    Slack
}

/// <summary>
/// Email notification configuration.
/// </summary>
public class EmailNotificationOptions
{
    /// <summary>
    /// SMTP server hostname.
    /// </summary>
    public string SmtpHost { get; set; } = "localhost";

    /// <summary>
    /// SMTP server port.
    /// </summary>
    public int SmtpPort { get; set; } = 25;

    /// <summary>
    /// Whether to use SSL/TLS for SMTP.
    /// </summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// SMTP username for authentication (optional).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// SMTP password for authentication (optional).
    /// WARNING: Do not store passwords in appsettings.json in production.
    /// Use User Secrets for development, or Azure Key Vault / environment variables for production.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Email address to send notifications from.
    /// </summary>
    public string FromAddress { get; set; } = "apmas@localhost";

    /// <summary>
    /// Display name for the sender.
    /// </summary>
    public string FromName { get; set; } = "APMAS";

    /// <summary>
    /// Email addresses to send notifications to.
    /// </summary>
    public List<string> ToAddresses { get; set; } = new();
}

/// <summary>
/// Slack notification configuration.
/// </summary>
public class SlackNotificationOptions
{
    /// <summary>
    /// Slack webhook URL for sending notifications.
    /// </summary>
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// Channel to post to (optional, uses webhook default if not specified).
    /// </summary>
    public string? Channel { get; set; }

    /// <summary>
    /// Username to display for the bot.
    /// </summary>
    public string Username { get; set; } = "APMAS";

    /// <summary>
    /// Emoji icon for the bot (e.g., ":robot_face:").
    /// </summary>
    public string IconEmoji { get; set; } = ":robot_face:";
}
