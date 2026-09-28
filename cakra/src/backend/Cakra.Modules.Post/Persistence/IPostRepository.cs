using Cakra.Core;
using Cakra.Modules.Post.Domain;

namespace Cakra.Modules.Post.Persistence;

/// <summary>
/// Internal repository contract for the <see cref="Domain.Post"/> aggregate root, its discussion
/// <see cref="Comment"/> entities, <see cref="Reaction"/> entities, and <see cref="PostReference"/> links
/// (Architecture §6, §7, §17, §19.3, §20, §21).
/// Internal to the Post module to enforce strict vertical slice boundaries.
/// </summary>
internal interface IPostRepository : IRepository<Domain.Post>
{
    Task AddCommentAsync(
        Comment comment,
        DateTime postUpdatedAtUtc,
        CancellationToken cancellationToken = default);

    Task AddReactionAsync(
        Reaction reaction,
        DateTime postUpdatedAtUtc,
        CancellationToken cancellationToken = default);

    Task UpdateReactionAsync(
        Reaction reaction,
        DateTime postUpdatedAtUtc,
        CancellationToken cancellationToken = default);

    Task AddReferenceAsync(
        PostReference reference,
        CancellationToken cancellationToken = default);
}
