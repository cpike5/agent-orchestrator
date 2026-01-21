namespace Apmas.Server.Core.Services;

/// <summary>
/// Resolves agent dependencies and validates the dependency graph.
/// </summary>
public interface IDependencyResolver
{
    /// <summary>
    /// Gets all agents in Pending status whose dependencies have all completed.
    /// </summary>
    /// <returns>List of agent roles ready to spawn.</returns>
    Task<IReadOnlyList<string>> GetReadyAgentsAsync();

    /// <summary>
    /// Gets the list of dependency roles for a given agent role.
    /// </summary>
    /// <param name="role">The agent role to look up.</param>
    /// <returns>List of dependency roles, or empty if none.</returns>
    IReadOnlyList<string> GetDependencies(string role);

    /// <summary>
    /// Validates the dependency graph at startup.
    /// Checks for circular dependencies and missing agent definitions.
    /// </summary>
    /// <returns>A validation result containing any errors found.</returns>
    DependencyValidationResult ValidateDependencyGraph();
}

/// <summary>
/// Result of dependency graph validation.
/// </summary>
public record DependencyValidationResult
{
    /// <summary>
    /// True if the dependency graph is valid (no errors).
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// List of validation errors (circular dependencies, missing definitions).
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// List of validation warnings (non-blocking issues).
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
