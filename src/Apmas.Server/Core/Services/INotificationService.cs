using Apmas.Server.Core.Models;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Service for sending notifications when agents need intervention or projects complete.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends an escalation notification when an agent needs human intervention.
    /// </summary>
    /// <param name="notification">The escalation details.</param>
    Task SendEscalationAsync(EscalationNotification notification);

    /// <summary>
    /// Sends a notification when a project completes.
    /// </summary>
    /// <param name="state">The final project state.</param>
    Task SendProjectCompleteAsync(ProjectState state);

    /// <summary>
    /// Sends a general alert notification.
    /// </summary>
    /// <param name="subject">The alert subject.</param>
    /// <param name="message">The alert message.</param>
    Task SendAlertAsync(string subject, string message);
}
