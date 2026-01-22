using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Apmas.Server.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Service for publishing real-time events to dashboard clients.
/// Aggregates events from MessageBus and agent state changes.
/// </summary>
public class DashboardEventService : IHostedService, IDashboardEventPublisher
{
    private readonly IMessageBus _messageBus;
    private readonly ILogger<DashboardEventService> _logger;
    private readonly Channel<DashboardEvent> _eventChannel;
    private CancellationTokenSource? _messageBusSubscriptionCts;
    private Task? _messageBusSubscriptionTask;

    public DashboardEventService(
        IMessageBus messageBus,
        ILogger<DashboardEventService> logger)
    {
        _messageBus = messageBus;
        _logger = logger;

        // Use unbounded channel for event broadcasting
        _eventChannel = Channel.CreateUnbounded<DashboardEvent>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = false
        });
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting DashboardEventService and subscribing to MessageBus");

        // Start background task to subscribe to MessageBus and republish messages as dashboard events
        _messageBusSubscriptionCts = new CancellationTokenSource();
        _messageBusSubscriptionTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var message in _messageBus.SubscribeAsync(agentRole: null, ct: _messageBusSubscriptionCts.Token))
                {
                    // Republish message as a dashboard event
                    await PublishMessageAsync(message);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                _logger.LogInformation("MessageBus subscription cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MessageBus subscription for DashboardEventService");
            }
        }, _messageBusSubscriptionCts.Token);

        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping DashboardEventService");

        // Cancel the MessageBus subscription
        _messageBusSubscriptionCts?.Cancel();

        // Wait for the subscription task to complete
        if (_messageBusSubscriptionTask != null)
        {
            try
            {
                await _messageBusSubscriptionTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }

        _messageBusSubscriptionCts?.Dispose();
        _eventChannel.Writer.Complete();
    }

    public async Task PublishAgentUpdateAsync(AgentState agentState)
    {
        ArgumentNullException.ThrowIfNull(agentState);

        var dashboardEvent = new DashboardEvent
        {
            Type = DashboardEventTypes.AgentUpdate,
            Timestamp = DateTime.UtcNow,
            Data = new
            {
                Role = agentState.Role,
                Status = agentState.Status.ToString(),
                SubagentType = agentState.SubagentType,
                SpawnedAt = agentState.SpawnedAt,
                CompletedAt = agentState.CompletedAt,
                TimeoutAt = agentState.TimeoutAt,
                RetryCount = agentState.RetryCount,
                LastMessage = agentState.LastMessage,
                LastError = agentState.LastError,
                LastHeartbeat = agentState.LastHeartbeat,
                EstimatedContextUsage = agentState.EstimatedContextUsage,
                ElapsedTime = agentState.SpawnedAt.HasValue
                    ? DateTime.UtcNow - agentState.SpawnedAt.Value
                    : (TimeSpan?)null
            }
        };

        await PublishEventAsync(dashboardEvent);

        _logger.LogDebug("Published agent update for {AgentRole} (Status: {Status})",
            agentState.Role, agentState.Status);
    }

    public async Task PublishMessageAsync(AgentMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var dashboardEvent = new DashboardEvent
        {
            Type = DashboardEventTypes.Message,
            Timestamp = message.Timestamp,
            Data = new
            {
                Id = message.Id,
                From = message.From,
                To = message.To,
                MessageType = message.Type.ToString(),
                Content = message.Content,
                Timestamp = message.Timestamp,
                ArtifactsJson = message.ArtifactsJson,
                MetadataJson = message.MetadataJson
            }
        };

        await PublishEventAsync(dashboardEvent);

        _logger.LogDebug("Published message {MessageId} from {From} to {To}",
            message.Id, message.From, message.To);
    }

    public async Task PublishCheckpointAsync(Checkpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        var dashboardEvent = new DashboardEvent
        {
            Type = DashboardEventTypes.Checkpoint,
            Timestamp = checkpoint.CreatedAt,
            Data = new
            {
                Id = checkpoint.Id,
                AgentRole = checkpoint.AgentRole,
                CreatedAt = checkpoint.CreatedAt,
                Summary = checkpoint.Summary,
                CompletedTaskCount = checkpoint.CompletedTaskCount,
                TotalTaskCount = checkpoint.TotalTaskCount,
                PercentComplete = checkpoint.PercentComplete,
                CompletedItemsJson = checkpoint.CompletedItemsJson,
                PendingItemsJson = checkpoint.PendingItemsJson,
                ActiveFilesJson = checkpoint.ActiveFilesJson,
                Notes = checkpoint.Notes,
                EstimatedContextUsage = checkpoint.EstimatedContextUsage
            }
        };

        await PublishEventAsync(dashboardEvent);

        _logger.LogDebug("Published checkpoint for {AgentRole} ({PercentComplete:F1}% complete)",
            checkpoint.AgentRole, checkpoint.PercentComplete);
    }

    public async Task PublishProjectUpdateAsync(ProjectState projectState)
    {
        ArgumentNullException.ThrowIfNull(projectState);

        var dashboardEvent = new DashboardEvent
        {
            Type = DashboardEventTypes.ProjectUpdate,
            Timestamp = DateTime.UtcNow,
            Data = new
            {
                Id = projectState.Id,
                Name = projectState.Name,
                WorkingDirectory = projectState.WorkingDirectory,
                Phase = projectState.Phase.ToString(),
                StartedAt = projectState.StartedAt,
                CompletedAt = projectState.CompletedAt,
                ElapsedTime = DateTime.UtcNow - projectState.StartedAt
            }
        };

        await PublishEventAsync(dashboardEvent);

        _logger.LogDebug("Published project update for {ProjectName} (Phase: {Phase})",
            projectState.Name, projectState.Phase);
    }

    public async IAsyncEnumerable<DashboardEvent> SubscribeAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("New dashboard event subscription started");

        await foreach (var dashboardEvent in _eventChannel.Reader.ReadAllAsync(ct))
        {
            yield return dashboardEvent;
        }

        _logger.LogInformation("Dashboard event subscription ended");
    }

    private async Task PublishEventAsync(DashboardEvent dashboardEvent)
    {
        try
        {
            await _eventChannel.Writer.WriteAsync(dashboardEvent);
        }
        catch (Exception ex)
        {
            // Log but don't throw - subscribers can still process other events
            _logger.LogWarning(ex, "Failed to broadcast dashboard event of type {EventType}", dashboardEvent.Type);
        }
    }
}
