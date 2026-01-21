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
    /// Path to the directory containing agent prompt templates.
    /// Relative to the working directory.
    /// </summary>
    public string PromptsDirectory { get; set; } = "Agents/Prompts";

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
}
