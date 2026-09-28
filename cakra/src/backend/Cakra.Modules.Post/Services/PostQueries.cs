using MediatR;

namespace Cakra.Modules.Post.Services;

/// <summary>
/// MediatR query to retrieve full post thread details (Architecture §7 — SCR-POST-001).
/// </summary>
public sealed record GetPostThreadDetailsQuery(Guid PostId) : IRequest<PostThreadDetailsDto?>;

/// <summary>
/// MediatR query to retrieve all active comments for a Post in chronological order (Architecture §7 — SCR-POST-001).
/// </summary>
public sealed record GetFullCommentsQuery(Guid PostId) : IRequest<IReadOnlyList<CommentDto>>;

/// <summary>
/// MediatR query to retrieve all active reactions for a Post in chronological order (Architecture §7 — SCR-POST-001).
/// </summary>
public sealed record GetReactionListQuery(Guid PostId) : IRequest<IReadOnlyList<ReactionDto>>;
