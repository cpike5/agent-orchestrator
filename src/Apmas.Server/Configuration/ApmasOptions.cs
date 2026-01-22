namespace Apmas.Server.Configuration;

/// <summary>
/// Root configuration options for APMAS.
/// </summary>
public class ApmasOptions
{
    public const string SectionName = "Apmas";

    /// <summary>
    /// Name of the project being managed.
    /// </summary>
    public string ProjectName { get; set; } = "untitled-project";

    /// <summary>
    /// Working directory for the project.
    /// </summary>
    public string WorkingDirectory { get; set; } = Environment.CurrentDirectory;

    /// <summary>
    /// Directory for APMAS runtime data (state, logs, checkpoints).
    /// Relative to WorkingDirectory.
    /// </summary>
    public string DataDirectory { get; set; } = ".apmas";

    /// <summary>
    /// Timeout configuration.
    /// </summary>
    public TimeoutOptions Timeouts { get; set; } = new();

    /// <summary>
    /// Agent configuration.
    /// </summary>
    public AgentOptions Agents { get; set; } = new();

    /// <summary>
    /// Agent spawner configuration.
    /// </summary>
    public SpawnerOptions Spawner { get; set; } = new();

    /// <summary>
    /// HTTP transport configuration for spawned agents.
    /// </summary>
    public HttpTransportOptions HttpTransport { get; set; } = new();

    /// <summary>
    /// Task decomposition configuration.
    /// </summary>
    public DecompositionOptions Decomposition { get; set; } = new();

    /// <summary>
    /// Notification configuration.
    /// </summary>
    public NotificationOptions Notifications { get; set; } = new();

    /// <summary>
    /// Metrics and observability configuration.
    /// </summary>
    public MetricsOptions Metrics { get; set; } = new();

    /// <summary>
    /// Gets the full path to the data directory.
    /// </summary>
    public string GetDataDirectoryPath() =>
        Path.IsPathRooted(DataDirectory)
            ? DataDirectory
            : Path.Combine(WorkingDirectory, DataDirectory);
}
