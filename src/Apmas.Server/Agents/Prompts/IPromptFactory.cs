using Apmas.Server.Core.Models;

namespace Apmas.Server.Agents.Prompts;

/// <summary>
/// Factory for resolving agent prompt templates by subagent type.
/// </summary>
public interface IPromptFactory
{
    /// <summary>
    /// Generates the prompt content for the specified subagent type.
    /// </summary>
    /// <param name="subagentType">The Claude Code subagent type (e.g., "systems-architect").</param>
    /// <param name="projectState">The current project state.</param>
    /// <param name="additionalContext">Optional checkpoint/recovery context.</param>
    /// <returns>The generated prompt string.</returns>
    /// <exception cref="ArgumentException">Thrown when subagentType is not recognized.</exception>
    string GeneratePrompt(string subagentType, ProjectState projectState, string? additionalContext = null);

    /// <summary>
    /// Checks if a prompt template exists for the specified subagent type.
    /// </summary>
    /// <param name="subagentType">The subagent type to check.</param>
    /// <returns>True if a prompt is registered for this type, false otherwise.</returns>
    bool HasPromptFor(string subagentType);
}
