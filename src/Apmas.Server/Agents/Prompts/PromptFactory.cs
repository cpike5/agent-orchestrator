using Apmas.Server.Core.Models;
using Microsoft.Extensions.Logging;

namespace Apmas.Server.Agents.Prompts;

/// <summary>
/// Factory that resolves prompt templates by subagent type.
/// Prompts are registered via DI and looked up by their SubagentType property.
/// </summary>
public class PromptFactory : IPromptFactory
{
    private readonly IReadOnlyDictionary<string, BaseAgentPrompt> _promptsBySubagentType;
    private readonly ILogger<PromptFactory> _logger;

    public PromptFactory(
        IEnumerable<BaseAgentPrompt> prompts,
        ILogger<PromptFactory> logger)
    {
        _logger = logger;

        // Build dictionary keyed by SubagentType (case-insensitive)
        var dict = new Dictionary<string, BaseAgentPrompt>(StringComparer.OrdinalIgnoreCase);
        foreach (var prompt in prompts)
        {
            if (dict.TryAdd(prompt.SubagentType, prompt))
            {
                _logger.LogDebug("Registered prompt {PromptType} for subagent type {SubagentType}",
                    prompt.GetType().Name, prompt.SubagentType);
            }
            else
            {
                _logger.LogWarning("Duplicate prompt registration for subagent type {SubagentType}, ignoring {PromptType}",
                    prompt.SubagentType, prompt.GetType().Name);
            }
        }

        _promptsBySubagentType = dict;
        _logger.LogInformation("Initialized PromptFactory with {Count} prompt templates", dict.Count);
    }

    public string GeneratePrompt(string subagentType, ProjectState projectState, string? additionalContext = null)
    {
        ArgumentNullException.ThrowIfNull(projectState);

        if (string.IsNullOrWhiteSpace(subagentType))
        {
            throw new ArgumentException("Subagent type cannot be null or whitespace", nameof(subagentType));
        }

        if (!_promptsBySubagentType.TryGetValue(subagentType, out var prompt))
        {
            throw new ArgumentException(
                $"No prompt template registered for subagent type '{subagentType}'. " +
                $"Available types: {string.Join(", ", _promptsBySubagentType.Keys.Order())}",
                nameof(subagentType));
        }

        _logger.LogDebug("Generating prompt for subagent type {SubagentType} using {PromptType}",
            subagentType, prompt.GetType().Name);

        return prompt.Generate(projectState, additionalContext);
    }

    public bool HasPromptFor(string subagentType)
    {
        return !string.IsNullOrWhiteSpace(subagentType) &&
               _promptsBySubagentType.ContainsKey(subagentType);
    }
}
