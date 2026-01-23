using System.ComponentModel.DataAnnotations;

namespace Apmas.Server.Configuration;

/// <summary>
/// Configuration options for loading project brief files.
/// </summary>
public class ProjectBriefOptions
{
    /// <summary>
    /// Path to the project brief file, relative to WorkingDirectory.
    /// </summary>
    public string FilePath { get; set; } = "PROJECT-BRIEF.md";

    /// <summary>
    /// Whether to require the brief file exists before starting.
    /// If true, the server will fail to start if the file is missing.
    /// </summary>
    public bool Required { get; set; } = false;

    /// <summary>
    /// Maximum size in KB to read from the brief file.
    /// Files exceeding this limit will be truncated.
    /// </summary>
    [Range(1, 1024, ErrorMessage = "MaxSizeKb must be between 1 and 1024")]
    public int MaxSizeKb { get; set; } = 100;
}
