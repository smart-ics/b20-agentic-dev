using System.Data;
using System.Threading.Channels;
using Cakra.Core;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Customer;
using Cakra.Modules.Organization;
using Cakra.Modules.Post.Domain;
using Cakra.Modules.Product;
using Cakra.Modules.Request;
using Cakra.Modules.WorkPackage;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cakra.Modules.Post.Services;

/// <summary>
/// Idempotent administrative rebuilder and background worker for the <c>post.FeedItems</c> materialized
/// read model (Architecture §12 Rebuild Strategy, §19.3 Dapper, §19.7 Background Processing via
/// <see cref="System.Threading.Channels"/>).
/// Truncates <c>post.FeedItems</c> and regenerates all rows from authoritative <c>post.Posts</c>,
/// <c>post.PostReferences</c>, <c>post.Comments</c>, and <c>post.Reactions</c> without data loss.
/// </summary>
public sealed class FeedProjectionRebuilder : BackgroundService, IFeedProjectionRebuilder
{
    static FeedProjectionRebuilder()
    {
        SqlMapper.AddTypeMap(typeof(DateTime), DbType.DateTime2);
        SqlMapper.AddTypeMap(typeof(DateTime?), DbType.DateTime2);
    }

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly IOrganizationQueryService? _organizationQueryService;
    private readonly ICustomerQueryService? _customerQueryService;
    private readonly IProductQueryService? _productQueryService;
    private readonly IRequestQueryService? _requestQueryService;
    private readonly IWorkPackageQueryService? _workPackageQueryService;
    private readonly ISystemClock? _clock;
    private readonly ILogger<FeedProjectionRebuilder> _logger;
    private readonly Channel<RebuildWorkItem> _rebuildChannel;

    public FeedProjectionRebuilder(
        IDbConnectionFactory connectionFactory,
        IServiceScopeFactory? scopeFactory = null,
        ISystemClock? clock = null,
        ILogger<FeedProjectionRebuilder>? logger = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger ?? NullLogger<FeedProjectionRebuilder>.Instance;
        _rebuildChannel = Channel.CreateUnbounded<RebuildWorkItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public FeedProjectionRebuilder(
        IDbConnectionFactory connectionFactory,
        IOrganizationQueryService? organizationQueryService,
        ICustomerQueryService? customerQueryService,
        IProductQueryService? productQueryService,
        IRequestQueryService? requestQueryService = null,
        IWorkPackageQueryService? workPackageQueryService = null,
        ISystemClock? clock = null,
        ILogger<FeedProjectionRebuilder>? logger = null)
        : this(connectionFactory, scopeFactory: null, clock: clock, logger: logger)
    {
        _organizationQueryService = organizationQueryService;
        _customerQueryService = customerQueryService;
        _productQueryService = productQueryService;
        _requestQueryService = requestQueryService;
        _workPackageQueryService = workPackageQueryService;
    }

    /// <inheritdoc />
    public int RebuildAll() => RebuildAllAsync().GetAwaiter().GetResult();

    /// <inheritdoc />
    public async ValueTask<Task<int>> EnqueueRebuildAsync(CancellationToken cancellationToken = default)
    {
        var workItem = new RebuildWorkItem(
            new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously));

        await _rebuildChannel.Writer.WriteAsync(workItem, cancellationToken);
        return workItem.CompletionSource.Task;
    }

    /// <summary>
    /// Non-blocking fire-and-forget enqueue of a background feed projection rebuild.
    /// </summary>
    public bool TryEnqueueRebuild()
    {
        var workItem = new RebuildWorkItem(
            new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously));

        return _rebuildChannel.Writer.TryWrite(workItem);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FeedProjectionRebuilder background channel worker started");

        try
        {
            await foreach (var workItem in _rebuildChannel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    var rebuiltCount = await RebuildAllAsync(stoppingToken);
                    workItem.CompletionSource.TrySetResult(rebuiltCount);
                }
                catch (OperationCanceledException oce) when (stoppingToken.IsCancellationRequested)
                {
                    workItem.CompletionSource.TrySetCanceled(oce.CancellationToken);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error while executing queued FeedProjectionRebuilder task");
                    workItem.CompletionSource.TrySetException(ex);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal graceful shutdown
        }
    }

    /// <inheritdoc />
    public async Task<int> RebuildAllAsync(CancellationToken cancellationToken = default)
    {
        const string postsSql = """
            SELECT
                [Id],
                [Title],
                [Content],
                [AuthorPersonId],
                [Source],
                [SourceEventType],
                [Status],
                [Visibility],
                [IsException],
                [ExceptionType],
                [CustomerId],
                [ProductId],
                [RequestId],
                [WorkPackageId],
                [CreatedAt],
                [UpdatedAt]
            FROM [post].[Posts]
            ORDER BY [CreatedAt] ASC, [Id] ASC;
            """;

        const string referencesSql = """
            SELECT
                [Id],
                [PostId],
                [ReferenceType],
                [ReferenceId],
                [ReferenceDisplay],
                [CreatedAt]
            FROM [post].[PostReferences]
            WHERE [RemovedAt] IS NULL
            ORDER BY [CreatedAt] ASC, [Id] ASC;
            """;

        const string commentsSql = """
            SELECT
                [Id],
                [PostId],
                [AuthorPersonId],
                [Content],
                [Status],
                [CreatedAt],
                [UpdatedAt]
            FROM [post].[Comments]
            WHERE [Status] = N'ACTIVE'
            ORDER BY [CreatedAt] ASC, [Id] ASC;
            """;

        const string reactionsSql = """
            SELECT
                [Id],
                [PostId],
                [CommentId],
                [PersonId],
                [ReactionType],
                [IsActive],
                [RemovedAt],
                [CreatedAt],
                [UpdatedAt]
            FROM [post].[Reactions]
            WHERE [IsActive] = 1
              AND [RemovedAt] IS NULL
            ORDER BY [CreatedAt] ASC, [Id] ASC;
            """;

        const string truncateFeedItemsSql = """
            TRUNCATE TABLE [post].[FeedItems];
            """;

        const string insertFeedItemSql = """
            INSERT INTO [post].[FeedItems] (
                [FeedItemId],
                [PostId],
                [AuthorPersonId],
                [AuthorName],
                [PostType],
                [Source],
                [Title],
                [ContentExcerpt],
                [Summary],
                [Status],
                [Visibility],
                [IsException],
                [ExceptionType],
                [ReferenceType],
                [ReferenceId],
                [ReferenceDisplay],
                [CustomerId],
                [CustomerName],
                [ProductId],
                [ProductName],
                [RequestId],
                [WorkPackageId],
                [CommentCount],
                [LatestCommentExcerpt],
                [ReactionCountsJson],
                [CreatedAt],
                [UpdatedAt],
                [LastActivityAt]
            ) VALUES (
                @FeedItemId,
                @PostId,
                @AuthorPersonId,
                @AuthorName,
                @PostType,
                @Source,
                @Title,
                @ContentExcerpt,
                @Summary,
                @Status,
                @Visibility,
                @IsException,
                @ExceptionType,
                @ReferenceType,
                @ReferenceId,
                @ReferenceDisplay,
                @CustomerId,
                @CustomerName,
                @ProductId,
                @ProductName,
                @RequestId,
                @WorkPackageId,
                @CommentCount,
                @LatestCommentExcerpt,
                @ReactionCountsJson,
                @CreatedAt,
                @UpdatedAt,
                @LastActivityAt
            );
            """;

        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        var postRows = (await connection.QueryAsync<PostSourceRow>(
            new CommandDefinition(postsSql, cancellationToken: cancellationToken))).AsList();

        var referenceRows = (await connection.QueryAsync<PostReferenceSourceRow>(
            new CommandDefinition(referencesSql, cancellationToken: cancellationToken))).AsList();

        var commentRows = (await connection.QueryAsync<CommentSourceRow>(
            new CommandDefinition(commentsSql, cancellationToken: cancellationToken))).AsList();

        var reactionRows = (await connection.QueryAsync<ReactionSourceRow>(
            new CommandDefinition(reactionsSql, cancellationToken: cancellationToken))).AsList();

        using var scope = _scopeFactory?.CreateScope();
        var orgQuery = _organizationQueryService ?? scope?.ServiceProvider.GetService<IOrganizationQueryService>();
        var customerQuery = _customerQueryService ?? scope?.ServiceProvider.GetService<ICustomerQueryService>();
        var productQuery = _productQueryService ?? scope?.ServiceProvider.GetService<IProductQueryService>();
        var requestQuery = _requestQueryService ?? scope?.ServiceProvider.GetService<IRequestQueryService>();
        var workPackageQuery = _workPackageQueryService ?? scope?.ServiceProvider.GetService<IWorkPackageQueryService>();

        var personNameCache = new Dictionary<Guid, string?>();
        var customerNameCache = new Dictionary<Guid, string?>();
        var productNameCache = new Dictionary<Guid, string?>();
        var requestCache = new Dictionary<Guid, RequestDto?>();
        var workPackageCache = new Dictionary<Guid, WorkPackageDto?>();

        async Task<string?> ResolvePersonNameAsync(Guid? personId)
        {
            if (!personId.HasValue || personId.Value == Guid.Empty || orgQuery is null)
            {
                return null;
            }

            if (personNameCache.TryGetValue(personId.Value, out var cached))
            {
                return cached;
            }

            var person = await orgQuery.GetPersonByIdAsync(personId.Value, cancellationToken);
            var fullName = string.IsNullOrWhiteSpace(person?.FullName) ? null : person.FullName.Trim();
            personNameCache[personId.Value] = fullName;
            return fullName;
        }

        async Task<RequestDto?> ResolveRequestAsync(Guid? requestId)
        {
            if (!requestId.HasValue || requestId.Value == Guid.Empty || requestQuery is null)
            {
                return null;
            }

            if (requestCache.TryGetValue(requestId.Value, out var cached))
            {
                return cached;
            }

            var req = await requestQuery.GetRequestByIdAsync(requestId.Value, cancellationToken);
            requestCache[requestId.Value] = req;
            return req;
        }

        async Task<WorkPackageDto?> ResolveWorkPackageAsync(Guid? workPackageId)
        {
            if (!workPackageId.HasValue || workPackageId.Value == Guid.Empty || workPackageQuery is null)
            {
                return null;
            }

            if (workPackageCache.TryGetValue(workPackageId.Value, out var cached))
            {
                return cached;
            }

            var wp = await workPackageQuery.GetWorkPackageByIdAsync(workPackageId.Value, cancellationToken);
            workPackageCache[workPackageId.Value] = wp;
            return wp;
        }

        async Task<string?> ResolveCustomerNameAsync(Guid? customerId)
        {
            if (!customerId.HasValue || customerId.Value == Guid.Empty || customerQuery is null)
            {
                return null;
            }

            if (customerNameCache.TryGetValue(customerId.Value, out var cached))
            {
                return cached;
            }

            var customer = await customerQuery.GetCustomerByIdAsync(customerId.Value, cancellationToken);
            var name = string.IsNullOrWhiteSpace(customer?.CustomerName) ? null : customer.CustomerName.Trim();
            customerNameCache[customerId.Value] = name;
            return name;
        }

        async Task<string?> ResolveProductNameAsync(Guid? productId)
        {
            if (!productId.HasValue || productId.Value == Guid.Empty || productQuery is null)
            {
                return null;
            }

            if (productNameCache.TryGetValue(productId.Value, out var cached))
            {
                return cached;
            }

            var product = await productQuery.GetProductByIdAsync(productId.Value, cancellationToken);
            var name = string.IsNullOrWhiteSpace(product?.Name) ? null : product.Name.Trim();
            productNameCache[productId.Value] = name;
            return name;
        }

        var referencesByPost = referenceRows
            .GroupBy(r => r.PostId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var commentsByPost = commentRows
            .GroupBy(c => c.PostId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var reactionsByPost = reactionRows
            .GroupBy(r => r.PostId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var insertParameters = new List<object>(postRows.Count);

        foreach (var post in postRows)
        {
            var postRefs = referencesByPost.TryGetValue(post.Id, out var refs)
                ? refs
                : new List<PostReferenceSourceRow>();

            var postComments = commentsByPost.TryGetValue(post.Id, out var comments)
                ? comments
                : new List<CommentSourceRow>();

            var postReactions = reactionsByPost.TryGetValue(post.Id, out var reactions)
                ? reactions
                : new List<ReactionSourceRow>();

            var postType = string.IsNullOrWhiteSpace(post.Source)
                ? PostSourceNames.HumanAuthored
                : post.Source.Trim().ToUpperInvariant();

            var authorName = await ResolvePersonNameAsync(post.AuthorPersonId);
            if (authorName is null && string.Equals(postType, PostSourceNames.SystemGenerated, StringComparison.OrdinalIgnoreCase))
            {
                authorName = "SYSTEM";
            }

            var requestId = post.RequestId ?? postRefs.FirstOrDefault(r => r.ReferenceType == PostReferenceTypes.Request)?.ReferenceId;
            var workPackageId = post.WorkPackageId ?? postRefs.FirstOrDefault(r => r.ReferenceType == PostReferenceTypes.WorkPackage)?.ReferenceId;
            var customerId = post.CustomerId ?? postRefs.FirstOrDefault(r => r.ReferenceType == PostReferenceTypes.Customer)?.ReferenceId;
            var productId = post.ProductId ?? postRefs.FirstOrDefault(r => r.ReferenceType == PostReferenceTypes.Product)?.ReferenceId;

            var isException = post.IsException;
            var exceptionType = PostExceptionTypes.NormalizeAndValidate(post.ExceptionType)
                ?? FeedProjectionHandler.InferExceptionTypeFromSourceEvent(post.SourceEventType);
            if (exceptionType is not null)
            {
                isException = true;
            }

            string? requestTitle = null;
            var requestDto = await ResolveRequestAsync(requestId);
            if (requestDto is not null)
            {
                requestTitle = string.IsNullOrWhiteSpace(requestDto.Title) ? null : requestDto.Title.Trim();
                customerId ??= requestDto.CustomerId;
                productId ??= requestDto.ProductId;
                workPackageId ??= requestDto.WorkPackageId;

                if (string.Equals(requestDto.Status, "ESCALATED", StringComparison.OrdinalIgnoreCase))
                {
                    isException = true;
                    exceptionType ??= PostExceptionTypes.Escalation;
                }
                else if (string.Equals(requestDto.Status, "REJECTED", StringComparison.OrdinalIgnoreCase))
                {
                    isException = true;
                    exceptionType ??= PostExceptionTypes.Rejection;
                }
            }

            string? workPackageName = null;
            var wpDto = await ResolveWorkPackageAsync(workPackageId);
            if (wpDto is not null)
            {
                workPackageName = string.IsNullOrWhiteSpace(wpDto.Name) ? null : wpDto.Name.Trim();
                customerId ??= wpDto.CustomerId;
                productId ??= wpDto.ProductId;
            }

            var customerName = await ResolveCustomerNameAsync(customerId);
            var productName = await ResolveProductNameAsync(productId);

            var primaryRefRow = postRefs.FirstOrDefault(r => r.ReferenceType == PostReferenceTypes.Request)
                ?? postRefs.FirstOrDefault(r => r.ReferenceType == PostReferenceTypes.WorkPackage)
                ?? postRefs.FirstOrDefault(r => r.ReferenceType == PostReferenceTypes.Customer)
                ?? postRefs.FirstOrDefault(r => r.ReferenceType == PostReferenceTypes.Product)
                ?? postRefs.FirstOrDefault();

            var (referenceType, referenceId, referenceDisplay) = FeedProjectionHandler.ResolvePrimaryReference(
                primaryRefRow?.ReferenceType,
                primaryRefRow?.ReferenceId,
                primaryRefRow?.ReferenceDisplay,
                requestId,
                requestTitle,
                workPackageId,
                workPackageName,
                customerId,
                customerName,
                productId,
                productName);

            var commentCount = postComments.Count;
            var latestComment = postComments.Count > 0 ? postComments[^1] : null;
            var latestCommentExcerpt = latestComment is not null
                ? FeedProjectionHandler.Truncate(latestComment.Content, 300)
                : null;

            var reactionCounts = postReactions
                .GroupBy(r => r.ReactionType.Trim().ToUpperInvariant(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            var reactionCountsJson = FeedItemDto.SerializeReactionCounts(reactionCounts);

            var lastActivityAt = post.UpdatedAt ?? post.CreatedAt;
            if (latestComment is not null)
            {
                var commentTime = latestComment.UpdatedAt ?? latestComment.CreatedAt;
                if (commentTime > lastActivityAt)
                {
                    lastActivityAt = commentTime;
                }
            }

            foreach (var reaction in postReactions)
            {
                var reactionTime = reaction.UpdatedAt ?? reaction.CreatedAt;
                if (reactionTime > lastActivityAt)
                {
                    lastActivityAt = reactionTime;
                }
            }

            if ((commentCount > 0 || postReactions.Count > 0) && lastActivityAt <= post.CreatedAt)
            {
                lastActivityAt = post.CreatedAt.AddMilliseconds(1);
            }

            var title = FeedProjectionHandler.Truncate(post.Title, 255);
            var contentExcerpt = FeedProjectionHandler.Truncate(post.Content, 500);
            var status = string.IsNullOrWhiteSpace(post.Status)
                ? PostStatusNames.Active
                : post.Status.Trim().ToUpperInvariant();
            var visibility = string.IsNullOrWhiteSpace(post.Visibility)
                ? PostVisibilityNames.Visible
                : post.Visibility.Trim().ToUpperInvariant();
            var effectiveIsException = isException || exceptionType is not null;

            insertParameters.Add(new
            {
                FeedItemId = Guid.NewGuid(),
                PostId = post.Id,
                AuthorPersonId = post.AuthorPersonId,
                AuthorName = FeedProjectionHandler.TruncateNullable(authorName, 150),
                PostType = postType,
                Source = postType,
                Title = title,
                ContentExcerpt = contentExcerpt,
                Summary = contentExcerpt,
                Status = status,
                Visibility = visibility,
                IsException = effectiveIsException,
                ExceptionType = exceptionType,
                ReferenceType = FeedProjectionHandler.TruncateNullable(referenceType, 50),
                ReferenceId = referenceId,
                ReferenceDisplay = FeedProjectionHandler.TruncateNullable(referenceDisplay, 255),
                CustomerId = customerId,
                CustomerName = FeedProjectionHandler.TruncateNullable(customerName, 150),
                ProductId = productId,
                ProductName = FeedProjectionHandler.TruncateNullable(productName, 150),
                RequestId = requestId,
                WorkPackageId = workPackageId,
                CommentCount = commentCount,
                LatestCommentExcerpt = latestCommentExcerpt,
                ReactionCountsJson = reactionCountsJson,
                CreatedAt = post.CreatedAt,
                UpdatedAt = lastActivityAt,
                LastActivityAt = lastActivityAt
            });
        }

        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(
            truncateFeedItemsSql,
            transaction: transaction,
            cancellationToken: cancellationToken));

        foreach (var param in insertParameters)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                insertFeedItemSql,
                param,
                transaction: transaction,
                cancellationToken: cancellationToken));
        }

        transaction.Commit();

        _logger.LogInformation(
            "FeedProjectionRebuilder.RebuildAllAsync completed: regenerated {RebuiltCount} FeedItems rows from {PostCount} Posts",
            insertParameters.Count,
            postRows.Count);

        return insertParameters.Count;
    }

    private sealed record RebuildWorkItem(TaskCompletionSource<int> CompletionSource);

    private sealed class PostSourceRow
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public Guid? AuthorPersonId { get; set; }
        public string Source { get; set; } = PostSourceNames.HumanAuthored;
        public string? SourceEventType { get; set; }
        public string Status { get; set; } = PostStatusNames.Active;
        public string Visibility { get; set; } = PostVisibilityNames.Visible;
        public bool IsException { get; set; }
        public string? ExceptionType { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? ProductId { get; set; }
        public Guid? RequestId { get; set; }
        public Guid? WorkPackageId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    private sealed class PostReferenceSourceRow
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        public string ReferenceType { get; set; } = string.Empty;
        public Guid ReferenceId { get; set; }
        public string? ReferenceDisplay { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private sealed class CommentSourceRow
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        public Guid AuthorPersonId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string Status { get; set; } = Comment.StatusActive;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    private sealed class ReactionSourceRow
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        public Guid? CommentId { get; set; }
        public Guid PersonId { get; set; }
        public string ReactionType { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime? RemovedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
