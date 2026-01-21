namespace Apmas.Server.Core.Services;

/// <summary>
/// Handles agent timeouts with progressive retry strategies.
/// </summary>
public interface ITimeoutHandler
{
    /// <summary>
    /// Handles a timeout for the specified agent by applying the appropriate retry strategy
    /// based on the agent's current retry count:
    /// - First timeout: Restart with checkpoint context
    /// - Second timeout: Restart with reduced scope
    /// - Third+ timeout: Escalate to human
    /// </summary>
    /// <param name="agentRole">The role of the timed-out agent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleTimeoutAsync(string agentRole, CancellationToken cancellationToken = default);
}
