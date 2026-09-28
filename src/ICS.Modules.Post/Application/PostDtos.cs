namespace ICS.Modules.Post.Application;

/// <summary>
/// DTO representing a Post for query services and UI.
/// </summary>
public sealed record PostDto(
    Guid PostId,
    string Title,
    string Content,
    string Source,
    string Status,
    string Visibility,
    Guid? AuthorPersonId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ArchivedAt);

/// <summary>
/// DTO representing a Comment for query services and UI.
/// </summary>
public sealed record PostCommentDto(
    Guid CommentId,
    Guid PostId,
    Guid AuthorPersonId,
    string Content,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<PostReactionDto>? Reactions);

/// <summary>
/// DTO representing a Reaction for query services and UI.
/// </summary>
public sealed record PostReactionDto(
    Guid ReactionId,
    Guid? PostId,
    Guid? CommentId,
    Guid PersonId,
    string Type,
    DateTime CreatedAt);

/// <summary>
/// DTO representing a Post thread details (Post + Comments + Reactions).
/// </summary>
public sealed record PostThreadDto(
    Guid PostId,
    string Title,
    string Content,
    string Source,
    string Status,
    string Visibility,
    Guid? AuthorPersonId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ArchivedAt,
    IReadOnlyList<PostCommentDto> Comments,
    IReadOnlyList<PostReactionDto> Reactions);