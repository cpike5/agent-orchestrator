namespace Apmas.Server.Core.Enums;

/// <summary>
/// High-level phases of the project lifecycle.
/// </summary>
public enum ProjectPhase
{
    /// <summary>Project is being set up.</summary>
    Initializing,

    /// <summary>Planning phase (architecture, design).</summary>
    Planning,

    /// <summary>Active development.</summary>
    Building,

    /// <summary>Testing phase.</summary>
    Testing,

    /// <summary>Code review phase.</summary>
    Reviewing,

    /// <summary>Finalizing deliverables.</summary>
    Completing,

    /// <summary>Project completed successfully.</summary>
    Completed,

    /// <summary>Project failed.</summary>
    Failed,

    /// <summary>Project paused (awaiting human input).</summary>
    Paused
}
