using Cakra.Modules.Request.Domain;

namespace Cakra.Modules.Request;

/// <summary>
/// Read-only data transfer object representing a <see cref="RequestSubTask"/> checklist item
/// (Architecture CR-006 §4 TD-008).
/// </summary>
public record RequestSubTaskDto(
    Guid Id,
    Guid RequestId,
    string Title,
    bool IsCompleted,
    Guid? AssigneePersonId,
    string? AssigneeName,
    DateTime? CompletedAt,
    Guid? CompletedByPersonId,
    string? CompletedByName,
    int SortOrder,
    DateTime CreatedAt,
    DateTime? UpdatedAt = null)
{
    internal static RequestSubTaskDto FromDomain(RequestSubTask subTask)
    {
        ArgumentNullException.ThrowIfNull(subTask);

        return new RequestSubTaskDto(
            subTask.Id,
            subTask.RequestId,
            subTask.Title,
            subTask.IsCompleted,
            subTask.AssigneePersonId,
            null,
            subTask.CompletedAt,
            subTask.CompletedByPersonId,
            null,
            subTask.SortOrder,
            subTask.CreatedAt,
            subTask.UpdatedAt);
    }
}
