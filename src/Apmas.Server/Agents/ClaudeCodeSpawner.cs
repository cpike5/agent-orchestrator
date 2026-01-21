using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Apmas.Server.Agents.Prompts;
using Apmas.Server.Configuration;
using Apmas.Server.Core.Models;
using Apmas.Server.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apmas.Server.Agents;

/// <summary>
/// Spawns and manages Claude Code agent processes.
/// </summary>
public class ClaudeCodeSpawner : IAgentSpawner, IDisposable
{
    private readonly ILogger<ClaudeCodeSpawner> _logger;
    private readonly ApmasOptions _apmasOptions;
    private readonly SpawnerOptions _spawnerOptions;
    private readonly IPromptFactory _promptFactory;
    private readonly IAgentStateManager _stateManager;
    private readonly ConcurrentDictionary<string, ManagedProcess> _processes = new();

    public ClaudeCodeSpawner(
        ILogger<ClaudeCodeSpawner> logger,
        IOptions<ApmasOptions> apmasOptions,
        IOptions<SpawnerOptions> spawnerOptions,
        IPromptFactory promptFactory,
        IAgentStateManager stateManager)
    {
        _logger = logger;
        _apmasOptions = apmasOptions.Value;
        _spawnerOptions = spawnerOptions.Value;
        _promptFactory = promptFactory;
        _stateManager = stateManager;
    }

    public async Task<SpawnResult> SpawnAgentAsync(
        string agentRole,
        string subagentType,
        string? checkpointContext = null)
    {
        // Validate parameters
        if (string.IsNullOrWhiteSpace(agentRole))
        {
            throw new ArgumentException("Agent role cannot be null or whitespace", nameof(agentRole));
        }

        if (string.IsNullOrWhiteSpace(subagentType))
        {
            throw new ArgumentException("Subagent type cannot be null or whitespace", nameof(subagentType));
        }

        // Check for duplicate process
        if (_processes.ContainsKey(agentRole))
        {
            throw new InvalidOperationException($"Agent with role '{agentRole}' is already running");
        }

        var taskId = Guid.NewGuid().ToString("D");
        string? tempPromptFile = null;
        Process? process = null;

        try
        {
            _logger.LogInformation(
                "Spawning agent {AgentRole} (subagent: {SubagentType}, taskId: {TaskId})",
                agentRole,
                subagentType,
                taskId);

            // Create temporary prompt file
            tempPromptFile = await CreatePromptFileAsync(agentRole, subagentType, checkpointContext);

            // Build MCP configuration
            var mcpConfig = BuildMcpConfig();

            // Build command line arguments
            var arguments = BuildCommandLineArguments(taskId, tempPromptFile, mcpConfig, agentRole);

            // Start the process
            var processStartInfo = new ProcessStartInfo
            {
                FileName = _spawnerOptions.ClaudeCodePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                WorkingDirectory = _apmasOptions.WorkingDirectory
            };

            process = new Process { StartInfo = processStartInfo };

            // Set up async output/error handling
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.LogDebug("[{AgentRole}] stdout: {Output}", agentRole, e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.LogWarning("[{AgentRole}] stderr: {Error}", agentRole, e.Data);
                }
            };

            // Start the process
            if (!process.Start())
            {
                process.Dispose();
                return SpawnResult.Failed(taskId, "Failed to start process");
            }

            // Wrap post-start setup in try-catch to ensure cleanup on failure
            try
            {
                // Begin async read operations
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var managedProcess = new ManagedProcess
                {
                    Process = process,
                    AgentRole = agentRole,
                    TaskId = taskId,
                    StartedAt = DateTime.UtcNow,
                    TempPromptFile = tempPromptFile
                };

                _processes[agentRole] = managedProcess;

                _logger.LogInformation(
                    "Successfully spawned agent {AgentRole} with PID {ProcessId}",
                    agentRole,
                    process.Id);

                return SpawnResult.Succeeded(taskId, process.Id);
            }
            catch
            {
                // If post-start setup fails, kill the process and clean up
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    process.Dispose();
                }
                catch (Exception killEx)
                {
                    _logger.LogWarning(killEx, "Failed to kill process during cleanup for agent {AgentRole}", agentRole);
                }

                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to spawn agent {AgentRole}", agentRole);

            // Clean up temp file on failure
            if (tempPromptFile != null)
            {
                try
                {
                    File.Delete(tempPromptFile);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogWarning(deleteEx, "Failed to delete temp prompt file {Path}", tempPromptFile);
                }
            }

            // Ensure process is disposed if it wasn't already added to _processes
            if (process != null && !_processes.ContainsKey(agentRole))
            {
                try
                {
                    process.Dispose();
                }
                catch (Exception disposeEx)
                {
                    _logger.LogWarning(disposeEx, "Failed to dispose process during cleanup for agent {AgentRole}", agentRole);
                }
            }

            return SpawnResult.Failed(taskId, ex.Message);
        }
    }

    public async Task<bool> TerminateAgentAsync(string agentRole)
    {
        if (!_processes.TryRemove(agentRole, out var managedProcess))
        {
            _logger.LogWarning("Cannot terminate agent {AgentRole}: not found in process registry", agentRole);
            return false;
        }

        try
        {
            var process = managedProcess.Process;

            if (process.HasExited)
            {
                _logger.LogInformation("Agent {AgentRole} has already exited with code {ExitCode}",
                    agentRole,
                    process.ExitCode);
                CleanupManagedProcess(managedProcess);
                return true;
            }

            _logger.LogInformation("Terminating agent {AgentRole} (PID {ProcessId})", agentRole, process.Id);

            // Try graceful shutdown first
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await TryGracefulShutdownWindowsAsync(process, agentRole);
            }
            else
            {
                // On Unix-like systems, send SIGTERM
                process.Kill(entireProcessTree: false);
            }

            // Wait for graceful shutdown
            var gracefulShutdownTimeout = TimeSpan.FromMilliseconds(_spawnerOptions.GracefulShutdownTimeoutMs);
            var exited = await WaitForExitAsync(process, gracefulShutdownTimeout);

            if (!exited)
            {
                _logger.LogWarning(
                    "Agent {AgentRole} did not exit gracefully within {TimeoutMs}ms, forcing termination",
                    agentRole,
                    _spawnerOptions.GracefulShutdownTimeoutMs);

                process.Kill(entireProcessTree: true);
                await WaitForExitAsync(process, TimeSpan.FromSeconds(5));
            }

            CleanupManagedProcess(managedProcess);

            _logger.LogInformation("Successfully terminated agent {AgentRole}", agentRole);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error terminating agent {AgentRole}", agentRole);
            CleanupManagedProcess(managedProcess);
            return false;
        }
    }

    public Task<AgentProcessInfo?> GetAgentProcessAsync(string agentRole)
    {
        if (!_processes.TryGetValue(agentRole, out var managedProcess))
        {
            return Task.FromResult<AgentProcessInfo?>(null);
        }

        var process = managedProcess.Process;

        try
        {
            var status = process.HasExited
                ? AgentProcessStatus.Exited
                : AgentProcessStatus.Running;

            var info = new AgentProcessInfo
            {
                ProcessId = process.Id,
                AgentRole = agentRole,
                StartedAt = managedProcess.StartedAt,
                Status = status,
                ExitCode = process.HasExited ? process.ExitCode : null
            };

            return Task.FromResult<AgentProcessInfo?>(info);
        }
        catch (InvalidOperationException ex)
        {
            // Process may have exited between TryGetValue and property access
            _logger.LogWarning(ex, "Process for agent {AgentRole} exited during status check", agentRole);
            return Task.FromResult<AgentProcessInfo?>(null);
        }
    }

    private async Task<string> CreatePromptFileAsync(
        string agentRole,
        string subagentType,
        string? checkpointContext)
    {
        // Get project state for prompt generation
        var projectState = await _stateManager.GetProjectStateAsync();

        // Generate prompt content from C# prompt class
        string promptContent;
        try
        {
            promptContent = _promptFactory.GeneratePrompt(subagentType, projectState, checkpointContext);
        }
        catch (ArgumentException ex) when (ex.ParamName == nameof(subagentType))
        {
            _logger.LogError("No prompt template found for subagent type {SubagentType}. " +
                "Ensure a prompt class with matching SubagentType property is registered.",
                subagentType);
            throw new InvalidOperationException(
                $"No prompt template registered for subagent type '{subagentType}'", ex);
        }

        // Create temp file with the generated prompt
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, promptContent);

        _logger.LogDebug("Created temp prompt file {TempFile} for agent {AgentRole} (subagent: {SubagentType})",
            tempFile, agentRole, subagentType);

        return tempFile;
    }

    private string BuildMcpConfig()
    {
        // If an MCP config path is specified, load it
        if (!string.IsNullOrEmpty(_spawnerOptions.McpConfigPath))
        {
            var configPath = Path.IsPathRooted(_spawnerOptions.McpConfigPath)
                ? _spawnerOptions.McpConfigPath
                : Path.Combine(_apmasOptions.WorkingDirectory, _spawnerOptions.McpConfigPath);

            return File.ReadAllText(configPath);
        }

        // Build MCP config dynamically
        var serverPath = Path.Combine(AppContext.BaseDirectory, "Apmas.Server.dll");

        var config = new
        {
            mcpServers = new
            {
                apmas = new
                {
                    type = "stdio",
                    command = "dotnet",
                    args = new[] { serverPath },
                    env = new Dictionary<string, string>()
                }
            }
        };

        return JsonSerializer.Serialize(config);
    }

    private string BuildCommandLineArguments(
        string taskId,
        string promptFile,
        string mcpConfig,
        string agentRole)
    {
        var args = new StringBuilder();

        // Project mode
        args.Append("-p ");

        // Model
        args.Append($"--model {_spawnerOptions.Model} ");

        // Skip permissions
        if (_spawnerOptions.DangerouslySkipPermissions)
        {
            args.Append("--dangerously-skip-permissions ");
        }

        // Append system prompt file
        args.Append($"--append-system-prompt-file \"{promptFile}\" ");

        // MCP config (inline JSON)
        var escapedMcpConfig = mcpConfig.Replace("\"", "\\\"");
        args.Append($"--mcp-config \"{escapedMcpConfig}\" ");

        // Output format
        args.Append($"--output-format {_spawnerOptions.OutputFormat} ");

        // Max turns
        args.Append($"--max-turns {_spawnerOptions.MaxTurns} ");

        // Session ID
        args.Append($"--session-id {taskId} ");

        // Initial message
        args.Append($"\"Begin your assigned task as the {agentRole} agent.\"");

        return args.ToString();
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private async Task TryGracefulShutdownWindowsAsync(Process process, string agentRole)
    {
        try
        {
            // Disable our own CTRL+C handler temporarily
            NativeMethods.SetConsoleCtrlHandler(IntPtr.Zero, true);

            // Attach to the target process's console
            if (NativeMethods.AttachConsole((uint)process.Id))
            {
                // Send CTRL+C event
                NativeMethods.GenerateConsoleCtrlEvent(NativeMethods.CtrlType.CTRL_C_EVENT, 0);

                // Detach from the console
                NativeMethods.FreeConsole();
            }
            else
            {
                _logger.LogWarning("Failed to attach to console for agent {AgentRole}, will force kill", agentRole);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during graceful shutdown for agent {AgentRole}", agentRole);
        }
        finally
        {
            // Re-enable our own CTRL+C handler
            NativeMethods.SetConsoleCtrlHandler(IntPtr.Zero, false);
        }

        await Task.CompletedTask;
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private void CleanupManagedProcess(ManagedProcess managedProcess)
    {
        // Clean up temp prompt file
        if (!string.IsNullOrEmpty(managedProcess.TempPromptFile))
        {
            try
            {
                File.Delete(managedProcess.TempPromptFile);
                _logger.LogDebug("Deleted temp prompt file {Path}", managedProcess.TempPromptFile);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temp prompt file {Path}", managedProcess.TempPromptFile);
            }
        }

        // Dispose process
        try
        {
            managedProcess.Process.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing process for agent {AgentRole}", managedProcess.AgentRole);
        }
    }

    public void Dispose()
    {
        foreach (var kvp in _processes)
        {
            try
            {
                var managedProcess = kvp.Value;
                if (!managedProcess.Process.HasExited)
                {
                    _logger.LogWarning("Terminating agent {AgentRole} during disposal", kvp.Key);
                    managedProcess.Process.Kill(entireProcessTree: true);
                }
                CleanupManagedProcess(managedProcess);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing agent {AgentRole}", kvp.Key);
            }
        }

        _processes.Clear();
    }

    private class ManagedProcess
    {
        public required Process Process { get; init; }
        public required string AgentRole { get; init; }
        public required string TaskId { get; init; }
        public required DateTime StartedAt { get; init; }
        public string? TempPromptFile { get; init; }
    }
}
