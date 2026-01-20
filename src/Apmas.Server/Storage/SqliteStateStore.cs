using Apmas.Server.Configuration;
using Apmas.Server.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apmas.Server.Storage;

/// <summary>
/// SQLite implementation of IStateStore using EF Core.
/// </summary>
public class SqliteStateStore : IStateStore
{
    private readonly IDbContextFactory<ApmasDbContext> _contextFactory;
    private readonly ILogger<SqliteStateStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SqliteStateStore(
        IDbContextFactory<ApmasDbContext> contextFactory,
        ILogger<SqliteStateStore> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<ProjectState?> GetProjectStateAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ProjectStates.FirstOrDefaultAsync();
    }

    public async Task SaveProjectStateAsync(ProjectState state)
    {
        await _lock.WaitAsync();
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.ProjectStates.FirstOrDefaultAsync();
            if (existing != null)
            {
                existing.Name = state.Name;
                existing.WorkingDirectory = state.WorkingDirectory;
                existing.Phase = state.Phase;
                existing.StartedAt = state.StartedAt;
                existing.CompletedAt = state.CompletedAt;
            }
            else
            {
                context.ProjectStates.Add(state);
            }

            await context.SaveChangesAsync();
            _logger.LogDebug("Saved project state for {Name}", state.Name);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<AgentState?> GetAgentStateAsync(string role)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AgentStates.FirstOrDefaultAsync(a => a.Role == role);
    }

    public async Task SaveAgentStateAsync(AgentState state)
    {
        await _lock.WaitAsync();
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.AgentStates.FirstOrDefaultAsync(a => a.Role == state.Role);
            if (existing != null)
            {
                existing.Status = state.Status;
                existing.SubagentType = state.SubagentType;
                existing.SpawnedAt = state.SpawnedAt;
                existing.CompletedAt = state.CompletedAt;
                existing.TimeoutAt = state.TimeoutAt;
                existing.TaskId = state.TaskId;
                existing.RetryCount = state.RetryCount;
                existing.ArtifactsJson = state.ArtifactsJson;
                existing.DependenciesJson = state.DependenciesJson;
                existing.LastMessage = state.LastMessage;
                existing.LastError = state.LastError;
                existing.EstimatedContextUsage = state.EstimatedContextUsage;
            }
            else
            {
                context.AgentStates.Add(state);
            }

            await context.SaveChangesAsync();
            _logger.LogDebug("Saved agent state for {Role}", state.Role);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<AgentState>> GetAllAgentStatesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AgentStates.ToListAsync();
    }

    public async Task<Checkpoint?> GetLatestCheckpointAsync(string role)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Checkpoints
            .Where(c => c.AgentRole == role)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task SaveCheckpointAsync(Checkpoint checkpoint)
    {
        await _lock.WaitAsync();
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.Checkpoints.Add(checkpoint);
            await context.SaveChangesAsync();
            _logger.LogDebug("Saved checkpoint for {Role}", checkpoint.AgentRole);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<AgentMessage>> GetMessagesAsync(
        string? role = null,
        DateTime? since = null,
        int? limit = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var query = context.Messages.AsQueryable();

        if (role != null)
        {
            query = query.Where(m => m.From == role || m.To == role);
        }

        if (since != null)
        {
            query = query.Where(m => m.Timestamp > since);
        }

        query = query.OrderByDescending(m => m.Timestamp);

        if (limit != null)
        {
            query = query.Take(limit.Value);
        }

        return await query.ToListAsync();
    }

    public async Task SaveMessageAsync(AgentMessage message)
    {
        await _lock.WaitAsync();
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.Messages.Add(message);
            await context.SaveChangesAsync();
            _logger.LogDebug("Saved message {Id} from {From} to {To}", message.Id, message.From, message.To);
        }
        finally
        {
            _lock.Release();
        }
    }
}
