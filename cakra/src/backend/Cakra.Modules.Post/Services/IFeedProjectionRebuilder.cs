namespace Cakra.Modules.Post.Services;

/// <summary>
/// Contract for the idempotent administrative routine that truncates and regenerates
/// <c>post.FeedItems</c> from authoritative <c>post.Posts</c>, <c>post.PostReferences</c>,
/// <c>post.Comments</c>, and <c>post.Reactions</c> tables (Architecture §12 Rebuild Strategy, §19.7).
/// </summary>
public interface IFeedProjectionRebuilder
{
    /// <summary>
    /// Truncates <c>post.FeedItems</c> and regenerates all feed projection rows from authoritative
    /// Post domain tables and cross-module reference query services.
    /// </summary>
    /// <returns>The number of <c>post.FeedItems</c> rows regenerated.</returns>
    Task<int> RebuildAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronous convenience wrapper for <see cref="RebuildAllAsync"/> (Architecture §12 Rebuild Strategy).
    /// </summary>
    /// <returns>The number of <c>post.FeedItems</c> rows regenerated.</returns>
    int RebuildAll() => RebuildAllAsync().GetAwaiter().GetResult();

    /// <summary>
    /// Enqueues an asynchronous rebuild work item onto the in-process <see cref="System.Threading.Channels.Channel{T}"/>
    /// queue processed by the background service (Architecture §19.7).
    /// </summary>
    /// <returns>A task that completes with the rebuilt item count once the background worker finishes the rebuild.</returns>
    ValueTask<Task<int>> EnqueueRebuildAsync(CancellationToken cancellationToken = default);
}
