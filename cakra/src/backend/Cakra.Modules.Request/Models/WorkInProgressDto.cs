namespace Cakra.Modules.Request;

/// <summary>
/// Read model representing a task in progress or paused under operational WIP tracking
/// (Architecture CR-021 §4 TD-003, §5, §11).
/// </summary>
public sealed record TaskWorkInProgressDto
{
    /// <summary>Unique identifier of the request.</summary>
    public Guid RequestId { get; init; }

    /// <summary>Brief summary or subject of the operational demand.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Detailed description and operational context of the demand.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Classification of the demand (e.g. GENERAL, Bug, Feature, Support).</summary>
    public string RequestType { get; init; } = string.Empty;

    /// <summary>Current authoritative lifecycle status string (IN_PROGRESS, PAUSED).</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Priority level (LOW, NORMAL, HIGH, URGENT).</summary>
    public string Priority { get; init; } = "NORMAL";

    /// <summary>CustomerId associated with the request, or <c>null</c> if unassigned.</summary>
    public Guid? CustomerId { get; init; }

    /// <summary>Enriched Customer name, or <c>null</c> if unassigned.</summary>
    public string? CustomerName { get; init; }

    /// <summary>Enriched Customer business code, or <c>null</c> if unassigned.</summary>
    public string? CustomerCode { get; init; }

    /// <summary>ProductId associated with the request, or <c>null</c> if unassigned.</summary>
    public Guid? ProductId { get; init; }

    /// <summary>WorkPackageId associated with the request, or <c>null</c> if unassigned.</summary>
    public Guid? WorkPackageId { get; init; }

    /// <summary>Cumulative elapsed seconds spent in IN_PROGRESS status across all intervals.</summary>
    public double TotalInProgressSeconds { get; init; }

    /// <summary>Cumulative elapsed hours spent in IN_PROGRESS status rounded to 2 decimal places.</summary>
    public double TotalInProgressHours { get; init; }

    /// <summary>Cumulative formatted elapsed duration string (e.g. "3h 45m").</summary>
    public string TotalInProgressFormatted { get; init; } = "0h 0m";

    /// <summary>Timestamp when the request was originally created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Timestamp when the request was last updated, or <c>null</c>.</summary>
    public DateTime? UpdatedAt { get; init; }

    /// <summary>Timestamp when work was most recently started on this request, or <c>null</c>.</summary>
    public DateTime? LastStartedAt { get; init; }
}

/// <summary>
/// Read model grouping active WIP tasks (single active in-progress task and paused tasks)
/// for a person under operational WIP tracking (Architecture CR-021 §4 TD-003, §5, §11).
/// </summary>
public sealed record PersonWorkInProgressDto
{
    /// <summary>Unique identifier of the person (owner).</summary>
    public Guid PersonId { get; init; }

    /// <summary>Enriched full name of the person.</summary>
    public string PersonName { get; init; } = string.Empty;

    /// <summary>Enriched email address of the person, or <c>null</c>.</summary>
    public string? Email { get; init; }

    /// <summary>The single active task currently IN_PROGRESS, or <c>null</c> if the person has no task currently in progress.</summary>
    public TaskWorkInProgressDto? InProgressTask { get; init; }

    /// <summary>List of tasks currently in PAUSED status assigned to this person, ordered by most recently updated first.</summary>
    public IReadOnlyList<TaskWorkInProgressDto> PausedTasks { get; init; } = Array.Empty<TaskWorkInProgressDto>();

    /// <summary>Total count of active tasks (in-progress plus paused) assigned to this person.</summary>
    public int TotalActiveTasksCount => (InProgressTask is not null ? 1 : 0) + PausedTasks.Count;
}
