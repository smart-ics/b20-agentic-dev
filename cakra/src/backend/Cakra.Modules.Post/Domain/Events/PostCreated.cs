using Cakra.Core;

namespace Cakra.Modules.Post.Domain.Events;

/// <summary>
/// Domain event emitted when a human-authored or system-generated <see cref="Post"/> is created
/// (Architecture §12 Update Triggers; Post Domain §9).
/// Carries complete metadata required by <c>FeedProjectionHandler</c> in P6-S29.
/// </summary>
public sealed record PostCreated(
    Guid EventId,
    Guid PostId,
    string Title,
    string Content,
    Guid? AuthorPersonId,
    string? AuthorName,
    string Source,
    string Status,
    string Visibility,
    bool IsException,
    string? ExceptionType,
    string? ReferenceType,
    Guid? ReferenceId,
    string? ReferenceDisplay,
    Guid? CustomerId,
    string? CustomerName,
    Guid? ProductId,
    string? ProductName,
    Guid? RequestId,
    Guid? WorkPackageId,
    DateTime OccurredAtUtc,
    string? SourceEventType = null) : IDomainEvent
{
    /// <summary>Convenience alias matching <c>FeedItems.PostType</c> (Architecture §12).</summary>
    public string PostType => Source;

    /// <summary>Bounded excerpt (up to 500 chars) matching <c>FeedItems.ContentExcerpt</c> (Architecture §12).</summary>
    public string ContentExcerpt => Content.Length <= 500 ? Content : Content[..500];

    /// <summary>Convenience alias for <see cref="ContentExcerpt"/>.</summary>
    public string Summary => ContentExcerpt;
}
