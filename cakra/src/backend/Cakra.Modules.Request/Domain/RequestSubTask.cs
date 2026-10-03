using Cakra.Core;
using Cakra.Modules.Request.Domain.Exceptions;

namespace Cakra.Modules.Request.Domain;

/// <summary>
/// Child entity representing a granular operational sub-task checklist item within
/// the <see cref="Request"/> aggregate root boundary (Architecture CR-006 §4 TD-001).
/// </summary>
public sealed class RequestSubTask : EntityBase
{
    /// <summary>Identifier of the parent Request.</summary>
    public Guid RequestId { get; private set; }

    /// <summary>Descriptive title of the sub-task item.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>True if the sub-task is completed; otherwise false.</summary>
    public bool IsCompleted { get; private set; }

    /// <summary>Optional PersonId of the collaborator assigned to complete this sub-task.</summary>
    public Guid? AssigneePersonId { get; private set; }

    /// <summary>Timestamp when the sub-task was marked completed, or null if pending.</summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>PersonId of the actor who completed this sub-task, or null if pending.</summary>
    public Guid? CompletedByPersonId { get; private set; }

    /// <summary>Zero-based or sequence display sort order.</summary>
    public int SortOrder { get; private set; }

    private RequestSubTask() { }

    internal RequestSubTask(
        Guid id,
        Guid requestId,
        string title,
        Guid? assigneePersonId,
        int sortOrder,
        DateTime now)
    {
        if (requestId == Guid.Empty)
            throw new RequestDomainValidationException("RequestId cannot be empty.", nameof(requestId));

        if (string.IsNullOrWhiteSpace(title))
            throw new RequestDomainValidationException("Title cannot be empty.", nameof(title));

        var trimmedTitle = title.Trim();
        if (trimmedTitle.Length > 255)
            throw new RequestDomainValidationException("Title cannot exceed 255 characters.", nameof(title));

        if (assigneePersonId.HasValue && assigneePersonId.Value == Guid.Empty)
            throw new RequestDomainValidationException("AssigneePersonId cannot be an empty GUID.", nameof(assigneePersonId));

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        RequestId = requestId;
        Title = trimmedTitle;
        AssigneePersonId = assigneePersonId;
        SortOrder = sortOrder;
        IsCompleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    internal void MarkCompleted(Guid completedByPersonId, DateTime now)
    {
        if (completedByPersonId == Guid.Empty)
            throw new RequestDomainValidationException("CompletedByPersonId cannot be empty.", nameof(completedByPersonId));

        IsCompleted = true;
        CompletedByPersonId = completedByPersonId;
        CompletedAt = now;
        UpdatedAt = now;
    }

    internal void Reopen(DateTime now)
    {
        IsCompleted = false;
        CompletedByPersonId = null;
        CompletedAt = null;
        UpdatedAt = now;
    }

    /// <summary>
    /// Rehydrates a <see cref="RequestSubTask"/> entity from persistence storage.
    /// </summary>
    public static RequestSubTask Rehydrate(
        Guid id,
        Guid requestId,
        string title,
        bool isCompleted,
        Guid? assigneePersonId,
        DateTime? completedAt,
        Guid? completedByPersonId,
        int sortOrder,
        DateTime createdAt,
        DateTime? updatedAt = null)
    {
        return new RequestSubTask
        {
            Id = id,
            RequestId = requestId,
            Title = title,
            IsCompleted = isCompleted,
            AssigneePersonId = assigneePersonId,
            CompletedAt = completedAt,
            CompletedByPersonId = completedByPersonId,
            SortOrder = sortOrder,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
