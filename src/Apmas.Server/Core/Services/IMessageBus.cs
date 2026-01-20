using Apmas.Server.Core.Models;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Inter-agent messaging system with guaranteed delivery and real-time subscriptions.
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Publishes a message to the bus and persists it.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    Task PublishAsync(AgentMessage message);

    /// <summary>
    /// Gets all messages for a specific agent, optionally filtered by time.
    /// Includes messages directly addressed to the agent and messages addressed to "all".
    /// </summary>
    /// <param name="agentRole">The role of the agent.</param>
    /// <param name="since">Optional time filter to get messages after this timestamp.</param>
    Task<IReadOnlyList<AgentMessage>> GetMessagesForAgentAsync(string agentRole, DateTime? since = null);

    /// <summary>
    /// Gets all messages in the system, optionally limited.
    /// </summary>
    /// <param name="limit">Maximum number of messages to return, or null for all.</param>
    Task<IReadOnlyList<AgentMessage>> GetAllMessagesAsync(int? limit = null);

    /// <summary>
    /// Subscribes to real-time message updates.
    /// </summary>
    /// <param name="agentRole">Filter to messages for this agent role (including "all" broadcasts), or null for all messages.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async enumerable of messages.</returns>
    IAsyncEnumerable<AgentMessage> SubscribeAsync(string? agentRole = null, CancellationToken ct = default);
}
