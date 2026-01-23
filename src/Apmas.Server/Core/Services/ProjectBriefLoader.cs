using Apmas.Server.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Service for loading project brief files.
/// </summary>
public interface IProjectBriefLoader
{
    /// <summary>
    /// Loads the project brief from the configured file path.
    /// </summary>
    /// <param name="workingDirectory">The project working directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The brief content, or null if no brief file exists and it's not required.</returns>
    /// <exception cref="FileNotFoundException">Thrown when Required is true and the file doesn't exist.</exception>
    Task<string?> LoadBriefAsync(string workingDirectory, CancellationToken ct = default);
}

/// <summary>
/// Default implementation of <see cref="IProjectBriefLoader"/>.
/// </summary>
public class ProjectBriefLoader : IProjectBriefLoader
{
    private readonly ProjectBriefOptions _options;
    private readonly ILogger<ProjectBriefLoader> _logger;

    public ProjectBriefLoader(
        IOptions<ProjectBriefOptions> options,
        ILogger<ProjectBriefLoader> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> LoadBriefAsync(string workingDirectory, CancellationToken ct = default)
    {
        var fullPath = Path.IsPathRooted(_options.FilePath)
            ? _options.FilePath
            : Path.Combine(workingDirectory, _options.FilePath);

        if (!File.Exists(fullPath))
        {
            if (_options.Required)
            {
                _logger.LogError("Required project brief not found at {Path}", fullPath);
                throw new FileNotFoundException($"Required project brief not found: {fullPath}", fullPath);
            }

            _logger.LogInformation("No project brief found at {Path}, continuing without brief", fullPath);
            return null;
        }

        var fileInfo = new FileInfo(fullPath);
        var maxBytes = _options.MaxSizeKb * 1024;

        if (fileInfo.Length > maxBytes)
        {
            _logger.LogWarning(
                "Project brief at {Path} exceeds {MaxKb}KB limit ({ActualKb}KB), truncating",
                fullPath,
                _options.MaxSizeKb,
                fileInfo.Length / 1024);

            // Read only up to the limit
            var buffer = new byte[maxBytes];
            await using var stream = File.OpenRead(fullPath);
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, maxBytes), ct);

            // Find the last complete line to avoid cutting mid-line
            var content = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
            var lastNewline = content.LastIndexOf('\n');
            if (lastNewline > 0)
            {
                content = content[..(lastNewline + 1)];
            }

            content += "\n\n[... truncated due to size limit ...]";

            _logger.LogInformation("Loaded truncated project brief from {Path} ({Length} chars)", fullPath, content.Length);
            return content;
        }

        var briefContent = await File.ReadAllTextAsync(fullPath, ct);
        _logger.LogInformation("Loaded project brief from {Path} ({Length} chars)", fullPath, briefContent.Length);

        return briefContent;
    }
}
