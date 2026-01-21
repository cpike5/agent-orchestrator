namespace Apmas.Server.Configuration;

/// <summary>
/// Configuration options for APMAS metrics and observability.
/// </summary>
public class MetricsOptions
{
    /// <summary>
    /// Whether metrics collection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// OpenTelemetry exporter configuration.
    /// </summary>
    public OpenTelemetryOptions OpenTelemetry { get; set; } = new();
}

/// <summary>
/// OpenTelemetry exporter configuration.
/// </summary>
public class OpenTelemetryOptions
{
    /// <summary>
    /// Whether OpenTelemetry export is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// OTLP endpoint for exporting metrics.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Protocol to use for OTLP export (grpc or http/protobuf).
    /// </summary>
    public string Protocol { get; set; } = "grpc";
}
