using System.Diagnostics;
using System.Diagnostics.Metrics;
using Apmas.Server.Core.Enums;
using Microsoft.Extensions.Logging;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Provides metrics for monitoring APMAS performance and observability using System.Diagnostics.Metrics.
/// </summary>
public class ApmasMetrics : IApmasMetrics
{
    private readonly IAgentStateManager _agentStateManager;
    private readonly ILogger<ApmasMetrics> _logger;
    private readonly Meter _meter;

    // Counters
    private readonly Counter<long> _agentsSpawnedCounter;
    private readonly Counter<long> _agentsCompletedCounter;
    private readonly Counter<long> _agentsFailedCounter;
    private readonly Counter<long> _agentsTimedOutCounter;
    private readonly Counter<long> _messagesSentCounter;
    private readonly Counter<long> _checkpointsSavedCounter;

    // Histograms
    private readonly Histogram<double> _agentDurationHistogram;
    private readonly Histogram<double> _heartbeatIntervalHistogram;

    // Cached values for observable gauges
    private int _cachedActiveAgentCount;
    private double _cachedProjectProgress;

    // Disposal flag
    private bool _disposed;

    public ApmasMetrics(
        IAgentStateManager agentStateManager,
        ILogger<ApmasMetrics> logger)
    {
        _agentStateManager = agentStateManager;
        _logger = logger;
        _meter = new Meter("Apmas.Server");

        // Initialize counters
        _agentsSpawnedCounter = _meter.CreateCounter<long>(
            "apmas.agents.spawned",
            description: "Number of agents spawned by role");

        _agentsCompletedCounter = _meter.CreateCounter<long>(
            "apmas.agents.completed",
            description: "Number of agents completed by role");

        _agentsFailedCounter = _meter.CreateCounter<long>(
            "apmas.agents.failed",
            description: "Number of agents failed by role and reason");

        _agentsTimedOutCounter = _meter.CreateCounter<long>(
            "apmas.agents.timedout",
            description: "Number of agents timed out by role");

        _messagesSentCounter = _meter.CreateCounter<long>(
            "apmas.messages.sent",
            description: "Number of messages sent by type");

        _checkpointsSavedCounter = _meter.CreateCounter<long>(
            "apmas.checkpoints.saved",
            description: "Number of checkpoints saved by role");

        // Initialize histograms
        _agentDurationHistogram = _meter.CreateHistogram<double>(
            "apmas.agents.duration",
            unit: "seconds",
            description: "Duration of agent execution by role");

        _heartbeatIntervalHistogram = _meter.CreateHistogram<double>(
            "apmas.heartbeat.interval",
            unit: "seconds",
            description: "Interval between heartbeats");

        // Initialize gauges (observable)
        _meter.CreateObservableGauge(
            "apmas.agents.active",
            observeValue: () => GetActiveAgentCount(),
            description: "Current number of active agents");

        _meter.CreateObservableGauge(
            "apmas.project.progress",
            unit: "percent",
            observeValue: () => GetProjectProgress(),
            description: "Current project progress percentage");

        _logger.LogInformation("ApmasMetrics initialized with meter {MeterName}", _meter.Name);
    }

    public void RecordAgentSpawned(string role)
    {
        var tags = new TagList { { "role", role } };
        _agentsSpawnedCounter.Add(1, tags);
    }

    public void RecordAgentCompleted(string role)
    {
        var tags = new TagList { { "role", role } };
        _agentsCompletedCounter.Add(1, tags);
    }

    public void RecordAgentFailed(string role, string reason)
    {
        var tags = new TagList
        {
            { "role", role },
            { "reason", reason }
        };
        _agentsFailedCounter.Add(1, tags);
    }

    public void RecordAgentTimedOut(string role)
    {
        var tags = new TagList { { "role", role } };
        _agentsTimedOutCounter.Add(1, tags);
    }

    public void RecordMessageSent(string messageType)
    {
        var tags = new TagList { { "type", messageType } };
        _messagesSentCounter.Add(1, tags);
    }

    public void RecordCheckpointSaved(string role)
    {
        var tags = new TagList { { "role", role } };
        _checkpointsSavedCounter.Add(1, tags);
    }

    public void RecordAgentDuration(string role, double durationSeconds)
    {
        var tags = new TagList { { "role", role } };
        _agentDurationHistogram.Record(durationSeconds, tags);
    }

    public void RecordHeartbeatInterval(double intervalSeconds)
    {
        _heartbeatIntervalHistogram.Record(intervalSeconds);
    }

    public async Task UpdateCachedMetricsAsync()
    {
        try
        {
            var activeAgents = await _agentStateManager.GetActiveAgentsAsync();
            _cachedActiveAgentCount = activeAgents.Count;

            var projectState = await _agentStateManager.GetProjectStateAsync();

            // Estimate progress based on project phase
            _cachedProjectProgress = projectState.Phase switch
            {
                ProjectPhase.Initializing => 0.0,
                ProjectPhase.Planning => 10.0,
                ProjectPhase.Building => 40.0,
                ProjectPhase.Testing => 70.0,
                ProjectPhase.Reviewing => 85.0,
                ProjectPhase.Completing => 95.0,
                ProjectPhase.Completed => 100.0,
                ProjectPhase.Failed => 0.0,
                ProjectPhase.Paused => 50.0,
                _ => 0.0
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update cached metrics");
        }
    }

    private int GetActiveAgentCount()
    {
        return _cachedActiveAgentCount;
    }

    private double GetProjectProgress()
    {
        return _cachedProjectProgress;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _meter.Dispose();
        _logger.LogInformation("ApmasMetrics disposed");
        _disposed = true;
    }
}
