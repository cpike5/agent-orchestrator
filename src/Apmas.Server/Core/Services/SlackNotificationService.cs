using System.Net.Http.Json;
using System.Text.Json;
using Apmas.Server.Configuration;
using Apmas.Server.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Notification service that sends messages to Slack via webhook.
/// </summary>
public class SlackNotificationService : INotificationService
{
    private readonly SlackNotificationOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SlackNotificationService> _logger;

    public SlackNotificationService(
        IOptions<NotificationOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<SlackNotificationService> logger)
    {
        _options = options.Value.Slack;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task SendEscalationAsync(EscalationNotification notification)
    {
        var blocks = BuildEscalationBlocks(notification);
        await SendSlackMessageAsync(blocks);

        _logger.LogInformation(
            "Slack escalation sent for agent {AgentRole}",
            notification.AgentRole);
    }

    public async Task SendProjectCompleteAsync(ProjectState state)
    {
        var blocks = BuildProjectCompleteBlocks(state);
        await SendSlackMessageAsync(blocks);

        _logger.LogInformation(
            "Slack project complete notification sent for {ProjectName}",
            state.Name);
    }

    public async Task SendAlertAsync(string subject, string message)
    {
        var blocks = new List<object>
        {
            new
            {
                type = "header",
                text = new { type = "plain_text", text = $":bell: {subject}", emoji = true }
            },
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text = message }
            }
        };

        await SendSlackMessageAsync(blocks);

        _logger.LogInformation("Slack alert sent: {Subject}", subject);
    }

    private async Task SendSlackMessageAsync(List<object> blocks)
    {
        if (string.IsNullOrEmpty(_options.WebhookUrl))
        {
            _logger.LogWarning("Slack webhook URL not configured. Skipping notification.");
            return;
        }

        var payload = new Dictionary<string, object>
        {
            ["username"] = _options.Username,
            ["icon_emoji"] = _options.IconEmoji,
            ["blocks"] = blocks
        };

        if (!string.IsNullOrEmpty(_options.Channel))
        {
            payload["channel"] = _options.Channel;
        }

        using var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsJsonAsync(_options.WebhookUrl, payload);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Failed to send Slack notification. Status: {StatusCode}, Response: {Response}",
                response.StatusCode,
                responseBody);
            throw new HttpRequestException(
                $"Slack webhook request failed with status {response.StatusCode}: {responseBody}");
        }
    }

    private static List<object> BuildEscalationBlocks(EscalationNotification notification)
    {
        var blocks = new List<object>
        {
            new
            {
                type = "header",
                text = new { type = "plain_text", text = ":rotating_light: Agent Escalation", emoji = true }
            },
            new
            {
                type = "section",
                fields = new[]
                {
                    new { type = "mrkdwn", text = $"*Agent Role:*\n{notification.AgentRole}" },
                    new { type = "mrkdwn", text = $"*Failure Count:*\n{notification.FailureCount}" }
                }
            }
        };

        if (!string.IsNullOrEmpty(notification.LastError))
        {
            var truncatedError = TruncateWithEllipsis(notification.LastError, 2000);
            blocks.Add(new
            {
                type = "section",
                text = new { type = "mrkdwn", text = $"*Last Error:*\n```{truncatedError}```" }
            });
        }

        if (notification.Checkpoint != null)
        {
            blocks.Add(new { type = "divider" });
            blocks.Add(new
            {
                type = "section",
                text = new
                {
                    type = "mrkdwn",
                    text = $"*Checkpoint:* {notification.Checkpoint.Summary}\n" +
                           $"Progress: {notification.Checkpoint.CompletedTaskCount}/{notification.Checkpoint.TotalTaskCount} " +
                           $"({notification.Checkpoint.PercentComplete:F1}%)"
                }
            });

            if (!string.IsNullOrEmpty(notification.Checkpoint.Notes))
            {
                blocks.Add(new
                {
                    type = "context",
                    elements = new[]
                    {
                        new { type = "mrkdwn", text = $"Notes: {notification.Checkpoint.Notes}" }
                    }
                });
            }
        }

        if (notification.Artifacts.Count > 0)
        {
            blocks.Add(new { type = "divider" });
            var artifactList = string.Join("\n", notification.Artifacts.Select(a => $"• {a}"));
            blocks.Add(new
            {
                type = "section",
                text = new { type = "mrkdwn", text = $"*Artifacts:*\n{artifactList}" }
            });
        }

        if (!string.IsNullOrEmpty(notification.Context))
        {
            blocks.Add(new { type = "divider" });
            var truncatedContext = TruncateWithEllipsis(notification.Context, 2000);
            blocks.Add(new
            {
                type = "section",
                text = new { type = "mrkdwn", text = $"*Context:*\n{truncatedContext}" }
            });
        }

        return blocks;
    }

    private static string TruncateWithEllipsis(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text;
        }

        return text.Substring(0, maxLength - 3) + "...";
    }

    private static List<object> BuildProjectCompleteBlocks(ProjectState state)
    {
        var duration = state.CompletedAt.HasValue
            ? state.CompletedAt.Value - state.StartedAt
            : TimeSpan.Zero;

        var durationText = duration.TotalHours >= 1
            ? $"{duration.TotalHours:F1} hours"
            : $"{duration.TotalMinutes:F0} minutes";

        return new List<object>
        {
            new
            {
                type = "header",
                text = new { type = "plain_text", text = ":white_check_mark: Project Complete", emoji = true }
            },
            new
            {
                type = "section",
                fields = new[]
                {
                    new { type = "mrkdwn", text = $"*Project:*\n{state.Name}" },
                    new { type = "mrkdwn", text = $"*Phase:*\n{state.Phase}" }
                }
            },
            new
            {
                type = "section",
                fields = new[]
                {
                    new { type = "mrkdwn", text = $"*Started:*\n{state.StartedAt:yyyy-MM-dd HH:mm} UTC" },
                    new { type = "mrkdwn", text = state.CompletedAt.HasValue
                        ? $"*Completed:*\n{state.CompletedAt:yyyy-MM-dd HH:mm} UTC"
                        : "*Completed:*\nN/A" }
                }
            },
            new
            {
                type = "context",
                elements = new[]
                {
                    new { type = "mrkdwn", text = $"Duration: {durationText} | Directory: {state.WorkingDirectory}" }
                }
            }
        };
    }
}
