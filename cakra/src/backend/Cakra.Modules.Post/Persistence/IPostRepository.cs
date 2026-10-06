using Cakra.Core;
using Cakra.Modules.Post.Domain;

namespace Cakra.Modules.Post.Persistence;

/// <summary>
/// Repository contract for the <see cref="Domain.Post"/> aggregate root, its discussion
/// <see cref="Comment"/> entities, <see cref="Reaction"/> entities, and <see cref="PostReference"/> links
/// (Architecture §6, §7, §17, §19.3, §20, §21; CR-018 / P2-S03).
/// </summary>
public interface IPostRepository : IRepository<Domain.Post>
{
    Task<IReadOnlyList<Domain.Post>> GetByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);

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
