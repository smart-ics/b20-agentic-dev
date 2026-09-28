using Cakra.Core;

namespace Cakra.Modules.Post.Domain.Events;

/// <summary>
/// Domain event emitted when an active <see cref="Reaction"/> is soft-removed from a <see cref="Post"/>
/// (Architecture §12 Update Triggers; Post Domain §9; UC-FCOL-002).
/// </summary>
public sealed record ReactionRemoved(
    Guid EventId,
    Guid ReactionId,
    Guid PostId,
    Guid PersonId,
    string ReactionType,
    IReadOnlyDictionary<string, int> ReactionCounts,
    DateTime OccurredAtUtc,
    Guid? CommentId = null) : IDomainEvent;
