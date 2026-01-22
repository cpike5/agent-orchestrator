namespace Apmas.Server.Mcp.Http;

/// <summary>
/// Provides a signal for when the HTTP MCP server is ready to accept connections.
/// Used to coordinate startup between the HTTP server and the SupervisorService.
/// </summary>
public interface IHttpServerReadySignal
{
    /// <summary>
    /// Gets whether the HTTP server is ready to accept connections.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Gets whether the HTTP transport is enabled at all.
    /// If disabled, callers should not wait for readiness.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Waits for the HTTP server to be ready to accept connections.
    /// Returns immediately if the server is already ready or HTTP transport is disabled.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for readiness.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the server is ready, false if timed out.</returns>
    Task<bool> WaitForReadyAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signals that the HTTP server is ready to accept connections.
    /// Called by HttpMcpServerHost when the server starts listening.
    /// </summary>
    void SignalReady();
}

/// <summary>
/// Implementation of <see cref="IHttpServerReadySignal"/> using a TaskCompletionSource.
/// </summary>
public class HttpServerReadySignal : IHttpServerReadySignal
{
    private readonly TaskCompletionSource _readySignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _isReady;

    public bool IsReady => _isReady;

    public bool IsEnabled { get; set; } = true;

    public async Task<bool> WaitForReadyAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || _isReady)
        {
            return true;
        }

        try
        {
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await _readySignal.Task.WaitAsync(linkedCts.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred
            return false;
        }
    }

    public void SignalReady()
    {
        _isReady = true;
        _readySignal.TrySetResult();
    }
}
