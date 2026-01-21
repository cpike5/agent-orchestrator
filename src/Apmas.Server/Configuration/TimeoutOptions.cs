namespace Apmas.Server.Configuration;

/// <summary>
/// Timeout configuration for agent lifecycle management.
/// </summary>
public class TimeoutOptions
{
    /// <summary>
    /// Default timeout in minutes for agent tasks.
    /// </summary>
    public int DefaultMinutes { get; set; } = 30;

    /// <summary>
    /// Expected heartbeat interval in minutes.
    /// Agents should call apmas_heartbeat at this frequency.
    /// </summary>
    public int HeartbeatIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Time in minutes without a heartbeat before an agent is considered unhealthy.
    /// </summary>
    public int HeartbeatTimeoutMinutes { get; set; } = 10;

    /// <summary>
    /// Maximum number of retry attempts before escalating to human.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Supervisor polling interval in seconds.
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Per-agent timeout overrides in minutes.
    /// Key is the agent role name.
    /// </summary>
    public Dictionary<string, int> AgentOverrides { get; set; } = new();

    /// <summary>
    /// Gets the timeout for a specific agent role.
    /// </summary>
    public TimeSpan GetTimeoutFor(string agentRole) =>
        TimeSpan.FromMinutes(
            AgentOverrides.TryGetValue(agentRole, out var minutes)
                ? minutes
                : DefaultMinutes);

    /// <summary>
    /// Gets the default timeout as a TimeSpan.
    /// </summary>
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(DefaultMinutes);

    /// <summary>
    /// Gets the heartbeat interval as a TimeSpan.
    /// </summary>
    public TimeSpan HeartbeatInterval => TimeSpan.FromMinutes(HeartbeatIntervalMinutes);

    /// <summary>
    /// Gets the heartbeat timeout as a TimeSpan.
    /// </summary>
    public TimeSpan HeartbeatTimeout => TimeSpan.FromMinutes(HeartbeatTimeoutMinutes);
}
