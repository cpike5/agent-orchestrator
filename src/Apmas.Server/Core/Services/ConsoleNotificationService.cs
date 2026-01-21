using Apmas.Server.Core.Models;
using Microsoft.Extensions.Logging;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Notification service that outputs to the console.
/// This is the default implementation that works out of the box.
/// </summary>
public class ConsoleNotificationService : INotificationService
{
    private readonly ILogger<ConsoleNotificationService> _logger;

    public ConsoleNotificationService(ILogger<ConsoleNotificationService> logger)
    {
        _logger = logger;
    }

    public Task SendEscalationAsync(EscalationNotification notification)
    {
        var separator = new string('=', 60);

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(separator);
        Console.WriteLine("ESCALATION: Agent Needs Human Intervention");
        Console.WriteLine(separator);
        Console.ResetColor();

        Console.WriteLine($"Agent Role:    {notification.AgentRole}");
        Console.WriteLine($"Failure Count: {notification.FailureCount}");

        if (!string.IsNullOrEmpty(notification.LastError))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Last Error:    {notification.LastError}");
            Console.ResetColor();
        }

        if (notification.Checkpoint != null)
        {
            Console.WriteLine();
            Console.WriteLine("Checkpoint Information:");
            Console.WriteLine($"  Summary:  {notification.Checkpoint.Summary}");
            Console.WriteLine($"  Progress: {notification.Checkpoint.CompletedTaskCount}/{notification.Checkpoint.TotalTaskCount} tasks ({notification.Checkpoint.PercentComplete:F1}%)");
            if (!string.IsNullOrEmpty(notification.Checkpoint.Notes))
            {
                Console.WriteLine($"  Notes:    {notification.Checkpoint.Notes}");
            }
        }

        if (notification.Artifacts.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Artifacts:");
            foreach (var artifact in notification.Artifacts)
            {
                Console.WriteLine($"  - {artifact}");
            }
        }

        if (!string.IsNullOrEmpty(notification.Context))
        {
            Console.WriteLine();
            Console.WriteLine("Context:");
            Console.WriteLine($"  {notification.Context}");
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(separator);
        Console.ResetColor();
        Console.WriteLine();

        _logger.LogWarning(
            "Escalation for agent {AgentRole}: {FailureCount} failures. Last error: {LastError}",
            notification.AgentRole,
            notification.FailureCount,
            notification.LastError);

        return Task.CompletedTask;
    }

    public Task SendProjectCompleteAsync(ProjectState state)
    {
        var separator = new string('=', 60);
        var duration = state.CompletedAt.HasValue
            ? state.CompletedAt.Value - state.StartedAt
            : TimeSpan.Zero;

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(separator);
        Console.WriteLine("PROJECT COMPLETE");
        Console.WriteLine(separator);
        Console.ResetColor();

        Console.WriteLine($"Project:   {state.Name}");
        Console.WriteLine($"Directory: {state.WorkingDirectory}");
        Console.WriteLine($"Phase:     {state.Phase}");
        Console.WriteLine($"Started:   {state.StartedAt:yyyy-MM-dd HH:mm:ss}");

        if (state.CompletedAt.HasValue)
        {
            Console.WriteLine($"Completed: {state.CompletedAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Duration:  {duration.TotalHours:F1} hours");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(separator);
        Console.ResetColor();
        Console.WriteLine();

        _logger.LogInformation(
            "Project {ProjectName} completed. Duration: {Duration}",
            state.Name,
            duration);

        return Task.CompletedTask;
    }

    public Task SendAlertAsync(string subject, string message)
    {
        var separator = new string('-', 60);

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(separator);
        Console.WriteLine($"ALERT: {subject}");
        Console.WriteLine(separator);
        Console.ResetColor();

        Console.WriteLine(message);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(separator);
        Console.ResetColor();
        Console.WriteLine();

        _logger.LogInformation("Alert: {Subject} - {Message}", subject, message);

        return Task.CompletedTask;
    }
}
