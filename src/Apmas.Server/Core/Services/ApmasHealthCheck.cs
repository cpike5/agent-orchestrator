using Apmas.Server.Core.Enums;
using Apmas.Server.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Health check for monitoring APMAS system status.
/// </summary>
public class ApmasHealthCheck : IHealthCheck
{
    private readonly IStateStore _stateStore;
    private readonly ILogger<ApmasHealthCheck> _logger;

    /// <summary>
    /// Agent statuses considered unhealthy.
    /// </summary>
    private static readonly AgentStatus[] UnhealthyStatuses =
    [
        AgentStatus.Failed,
        AgentStatus.TimedOut,
        AgentStatus.Escalated
    ];

    public ApmasHealthCheck(
        IStateStore stateStore,
        ILogger<ApmasHealthCheck> logger)
    {
        _stateStore = stateStore;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();

        try
        {
            // Check database connectivity by fetching project state
            var projectState = await _stateStore.GetProjectStateAsync();
            if (projectState is null)
            {
                data["error"] = "No project state found";
                return HealthCheckResult.Unhealthy(
                    description: "Database accessible but no project initialized",
                    data: data);
            }

            data["projectPhase"] = projectState.Phase.ToString();
            data["projectName"] = projectState.Name;

            // Check for project failure
            if (projectState.Phase == ProjectPhase.Failed)
            {
                return HealthCheckResult.Unhealthy(
                    description: "Project is in failed state",
                    data: data);
            }

            // Get all agent states
            var allAgents = await _stateStore.GetAllAgentStatesAsync();

            // Count active agents (Running, Spawning, Paused)
            var activeAgents = allAgents
                .Where(a => a.Status is AgentStatus.Running or AgentStatus.Spawning or AgentStatus.Paused)
                .ToList();
            data["activeAgents"] = activeAgents.Count;

            // Find unhealthy agents
            var unhealthyAgents = allAgents
                .Where(a => UnhealthyStatuses.Contains(a.Status))
                .Select(a => new { a.Role, Status = a.Status.ToString(), a.LastError })
                .ToList();
            data["unhealthyAgents"] = unhealthyAgents;
            data["unhealthyAgentCount"] = unhealthyAgents.Count;

            // Find last activity timestamp
            var lastActivity = allAgents
                .Select(a => a.LastHeartbeat ?? a.SpawnedAt ?? a.CompletedAt)
                .Where(t => t.HasValue)
                .OrderByDescending(t => t)
                .FirstOrDefault() ?? projectState.StartedAt;
            data["lastActivity"] = lastActivity;

            // Determine health status
            if (unhealthyAgents.Count > 0)
            {
                _logger.LogWarning(
                    "Health check degraded: {UnhealthyCount} unhealthy agents detected",
                    unhealthyAgents.Count);

                return HealthCheckResult.Degraded(
                    description: $"{unhealthyAgents.Count} agent(s) in unhealthy state",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                description: "All systems operational",
                data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed with database error");
            data["error"] = ex.Message;

            return HealthCheckResult.Unhealthy(
                description: "Database connectivity error",
                exception: ex,
                data: data);
        }
    }
}
