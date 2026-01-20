namespace Apmas.Server.Core.Enums;

/// <summary>
/// Represents the current phase of the project.
/// </summary>
public enum ProjectPhase
{
    /// <summary>Project is being set up.</summary>
    Initializing,

    /// <summary>Architecture and design planning.</summary>
    Planning,

    /// <summary>Active development.</summary>
    Building,

    /// <summary>Testing phase.</summary>
    Testing,

    /// <summary>Code review in progress.</summary>
    Reviewing,

    /// <summary>Finalizing deliverables.</summary>
    Completing,

    /// <summary>Project successfully completed.</summary>
    Completed,

    /// <summary>Project failed.</summary>
    Failed,

    /// <summary>Project paused, awaiting human intervention.</summary>
    Paused
}
