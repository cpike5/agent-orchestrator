namespace Apmas.Server.Core.Models;

/// <summary>
/// Represents an escalation notification when an agent needs human intervention.
/// </summary>
/// <param name="AgentRole">The role of the agent that needs intervention.</param>
/// <param name="FailureCount">Number of times the agent has failed.</param>
/// <param name="LastError">The most recent error message, if any.</param>
/// <param name="Checkpoint">The last checkpoint saved by the agent, if any.</param>
/// <param name="Artifacts">List of artifact file paths produced by the agent.</param>
/// <param name="Context">Additional context about the agent's state and what went wrong.</param>
public record EscalationNotification(
    string AgentRole,
    int FailureCount,
    string? LastError,
    Checkpoint? Checkpoint,
    IReadOnlyList<string> Artifacts,
    string? Context
)
{
    /// <summary>
    /// Creates an escalation notification from an agent state.
    /// </summary>
    public static EscalationNotification FromAgentState(
        AgentState agent,
        Checkpoint? checkpoint,
        string? additionalContext = null)
    {
        var artifacts = new List<string>();
        if (!string.IsNullOrEmpty(agent.ArtifactsJson))
        {
            try
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<List<string>>(agent.ArtifactsJson);
                if (parsed != null)
                {
                    artifacts.AddRange(parsed);
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Ignore JSON parsing errors
            }
        }

        return new EscalationNotification(
            AgentRole: agent.Role,
            FailureCount: agent.RetryCount,
            LastError: agent.LastError,
            Checkpoint: checkpoint,
            Artifacts: artifacts,
            Context: additionalContext ?? agent.LastMessage
        );
    }
}
