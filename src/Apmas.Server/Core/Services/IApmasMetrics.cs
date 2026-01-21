namespace Apmas.Server.Core.Services;

/// <summary>
/// Provides metrics for monitoring APMAS performance and observability.
/// </summary>
public interface IApmasMetrics : IDisposable
{
    /// <summary>
    /// Records that an agent was spawned.
    /// </summary>
    /// <param name="role">The role identifier of the agent.</param>
    void RecordAgentSpawned(string role);

    /// <summary>
    /// Records that an agent completed successfully.
    /// </summary>
    /// <param name="role">The role identifier of the agent.</param>
    void RecordAgentCompleted(string role);

    /// <summary>
    /// Records that an agent failed.
    /// </summary>
    /// <param name="role">The role identifier of the agent.</param>
    /// <param name="reason">The reason for failure.</param>
    void RecordAgentFailed(string role, string reason);

    /// <summary>
    /// Records that an agent timed out.
    /// </summary>
    /// <param name="role">The role identifier of the agent.</param>
    void RecordAgentTimedOut(string role);

    /// <summary>
    /// Records that a message was sent.
    /// </summary>
    /// <param name="messageType">The type of message sent.</param>
    void RecordMessageSent(string messageType);

    /// <summary>
    /// Records that a checkpoint was saved.
    /// </summary>
    /// <param name="role">The role identifier of the agent.</param>
    void RecordCheckpointSaved(string role);

    /// <summary>
    /// Records the duration of an agent's execution.
    /// </summary>
    /// <param name="role">The role identifier of the agent.</param>
    /// <param name="durationSeconds">The duration in seconds.</param>
    void RecordAgentDuration(string role, double durationSeconds);

    /// <summary>
    /// Records the interval between heartbeats.
    /// </summary>
    /// <param name="intervalSeconds">The interval in seconds.</param>
    void RecordHeartbeatInterval(double intervalSeconds);

    /// <summary>
    /// Updates cached values for observable gauges.
    /// Call this method when agent or project state changes.
    /// </summary>
    Task UpdateCachedMetricsAsync();
}
