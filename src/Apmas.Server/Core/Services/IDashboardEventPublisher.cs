using Apmas.Server.Core.Models;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Service for publishing real-time events to dashboard clients.
/// Aggregates events from MessageBus and agent state changes.
/// </summary>
public interface IDashboardEventPublisher
{
    /// <summary>
    /// Publishes an agent update event (status change, heartbeat, retry count, etc.).
    /// </summary>
    /// <param name="agentState">The agent state to publish.</param>
    Task PublishAgentUpdateAsync(AgentState agentState);

    /// <summary>
    /// Publishes a message event (from MessageBus).
    /// </summary>
    /// <param name="message">The message to publish.</param>
    Task PublishMessageAsync(AgentMessage message);

    /// <summary>
    /// Publishes a checkpoint event.
    /// </summary>
    /// <param name="checkpoint">The checkpoint to publish.</param>
    Task PublishCheckpointAsync(Checkpoint checkpoint);

    /// <summary>
    /// Publishes a project state update event.
    /// </summary>
    /// <param name="projectState">The project state to publish.</param>
    Task PublishProjectUpdateAsync(ProjectState projectState);

    /// <summary>
    /// Subscribes to all dashboard events.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async enumerable of dashboard events.</returns>
    IAsyncEnumerable<DashboardEvent> SubscribeAsync(CancellationToken ct = default);
}
