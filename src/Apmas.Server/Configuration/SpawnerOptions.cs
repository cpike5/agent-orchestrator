namespace Apmas.Server.Configuration;

/// <summary>
/// Configuration options for the agent spawner.
/// </summary>
public class SpawnerOptions
{
    /// <summary>
    /// Path to the Claude Code CLI executable.
    /// Defaults to "claude" (assumes it's in PATH).
    /// </summary>
    public string ClaudeCodePath { get; set; } = "claude";

    /// <summary>
    /// Model to use for spawned agents (e.g., "sonnet", "opus").
    /// </summary>
    public string Model { get; set; } = "sonnet";

    /// <summary>
    /// Maximum number of turns before the agent automatically stops.
    /// </summary>
    public int MaxTurns { get; set; } = 100;

    /// <summary>
    /// Path to the MCP server configuration file.
    /// If null, will be generated dynamically.
    /// </summary>
    public string? McpConfigPath { get; set; }

    /// <summary>
    /// Timeout in milliseconds for graceful termination before force kill.
    /// </summary>
    public int GracefulShutdownTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Whether to skip permissions check when spawning agents.
    /// </summary>
    public bool DangerouslySkipPermissions { get; set; } = true;

    /// <summary>
    /// Output format for Claude Code CLI.
    /// </summary>
    public string OutputFormat { get; set; } = "stream-json";

    /// <summary>
    /// Whether to use HTTP transport (connecting to running APMAS) instead of stdio (spawning new APMAS).
    /// When true, spawned agents will connect to the HTTP endpoint instead of spawning their own server.
    /// </summary>
    public bool UseHttpTransport { get; set; } = true;

    /// <summary>
    /// Whether to log agent stdout/stderr output.
    /// Set to false to reduce log noise from agent processes.
    /// </summary>
    public bool LogAgentOutput { get; set; } = true;

    /// <summary>
    /// Log level for agent stdout output. Options: "Debug", "Information", "Warning".
    /// Only applies when LogAgentOutput is true.
    /// </summary>
    public string AgentOutputLogLevel { get; set; } = "Debug";
}
