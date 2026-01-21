namespace Apmas.Server.Configuration;

/// <summary>
/// Configuration for task decomposition and context management.
/// </summary>
public class DecompositionOptions
{
    /// <summary>
    /// Safe context window size in tokens.
    /// Tasks exceeding this will be decomposed into smaller subtasks.
    /// </summary>
    public int SafeContextTokens { get; set; } = 50_000;

    /// <summary>
    /// Estimated token count per file.
    /// Used to estimate total context size for a task.
    /// </summary>
    public int TokensPerFile { get; set; } = 15_000;
}
