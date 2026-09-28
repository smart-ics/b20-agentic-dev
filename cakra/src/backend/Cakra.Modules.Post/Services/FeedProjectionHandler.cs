using System.Data;
using Cakra.Core;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Customer;
using Cakra.Modules.Organization;
using Cakra.Modules.Post.Domain;
using Cakra.Modules.Post.Domain.Events;
using Cakra.Modules.Product;
using Cakra.Modules.Request;
using Cakra.Modules.Request.Domain.Events;
using Cakra.Modules.WorkPackage;
using Dapper;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cakra.Modules.Post.Services;

/// <summary>
/// In-process MediatR notification handler maintaining the <c>post.FeedItems</c> materialized read model
/// synchronously within the originating command's execution scope (Architecture §6, §7, §12, §15, §16, §18, §19.2, §19.3, §20, §21).
/// Subscribes to all six Post domain events (<see cref="PostCreated"/>, <see cref="CommentAdded"/>,
/// <see cref="ReactionAdded"/>, <see cref="ReactionRemoved"/>, <see cref="PostVisibilityChanged"/>,
/// <see cref="PostArchived"/>) as well as Request exception events (<see cref="RequestEscalated"/>,
/// <see cref="RequestRejected"/>, <see cref="RequestStalled"/>).
/// </summary>
public sealed class FeedProjectionHandler :
    INotificationHandler<PostCreated>,
    INotificationHandler<CommentAdded>,
    INotificationHandler<ReactionAdded>,
    INotificationHandler<ReactionRemoved>,
    INotificationHandler<PostVisibilityChanged>,
    INotificationHandler<PostArchived>,
    INotificationHandler<RequestEscalated>,
    INotificationHandler<RequestRejected>,
    INotificationHandler<RequestStalled>
{
    static FeedProjectionHandler()
    {
        SqlMapper.AddTypeMap(typeof(DateTime), DbType.DateTime2);
        SqlMapper.AddTypeMap(typeof(DateTime?), DbType.DateTime2);
    }

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IOrganizationQueryService? _organizationQueryService;
    private readonly ICustomerQueryService? _customerQueryService;
    private readonly IProductQueryService? _productQueryService;
    private readonly IRequestQueryService? _requestQueryService;
    private readonly IWorkPackageQueryService? _workPackageQueryService;
    private readonly ISystemClock? _clock;
    private readonly ILogger<FeedProjectionHandler> _logger;

    public FeedProjectionHandler(
        IDbConnectionFactory connectionFactory,
        IOrganizationQueryService? organizationQueryService = null,
        ICustomerQueryService? customerQueryService = null,
        IProductQueryService? productQueryService = null,
        IRequestQueryService? requestQueryService = null,
        IWorkPackageQueryService? workPackageQueryService = null,
        ISystemClock? clock = null,
        ILogger<FeedProjectionHandler>? logger = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _organizationQueryService = organizationQueryService;
        _customerQueryService = customerQueryService;
        _productQueryService = productQueryService;
        _requestQueryService = requestQueryService;
        _workPackageQueryService = workPackageQueryService;
        _clock = clock;
        _logger = logger ?? NullLogger<FeedProjectionHandler>.Instance;
    }

    private DateTime UtcNow => _clock?.UtcNow ?? DateTime.UtcNow;

    /// <summary>
    /// Handles <see cref="PostCreated"/> by inserting (or idempotently updating) the corresponding
    /// <c>post.FeedItems</c> row with enriched author, customer, product, and reference attributes
    /// (Architecture §12 Update Triggers).
    /// </summary>
    public async Task Handle(PostCreated notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification.PostId == Guid.Empty)
        {
            return;
        }

        var authorPersonId = NormalizeOptionalGuid(notification.AuthorPersonId);
        var customerId = NormalizeOptionalGuid(notification.CustomerId);
        var productId = NormalizeOptionalGuid(notification.ProductId);
        var requestId = NormalizeOptionalGuid(notification.RequestId);
        var workPackageId = NormalizeOptionalGuid(notification.WorkPackageId);

        var authorName = TrimOrNull(notification.AuthorName);
        if (authorName is null && authorPersonId.HasValue && _organizationQueryService is not null)
        {
            var person = await _organizationQueryService.GetPersonByIdAsync(authorPersonId.Value, cancellationToken);
            authorName = TrimOrNull(person?.FullName);
        }

        var postType = string.IsNullOrWhiteSpace(notification.PostType)
            ? PostSourceNames.HumanAuthored
            : notification.PostType.Trim().ToUpperInvariant();

        if (authorName is null && string.Equals(postType, PostSourceNames.SystemGenerated, StringComparison.OrdinalIgnoreCase))
        {
            authorName = "SYSTEM";
        }

        var isException = notification.IsException;
        var exceptionType = PostExceptionTypes.NormalizeAndValidate(notification.ExceptionType)
            ?? InferExceptionTypeFromSourceEvent(notification.SourceEventType);
        if (exceptionType is not null)
        {
            isException = true;
        }

        string? requestTitle = null;

        if (requestId.HasValue && _requestQueryService is not null)
        {
            var requestDto = await _requestQueryService.GetRequestByIdAsync(requestId.Value, cancellationToken);
            if (requestDto is not null)
            {
                requestTitle = TrimOrNull(requestDto.Title);
                customerId ??= NormalizeOptionalGuid(requestDto.CustomerId);
                productId ??= NormalizeOptionalGuid(requestDto.ProductId);
                workPackageId ??= NormalizeOptionalGuid(requestDto.WorkPackageId);

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
        }

        string? workPackageName = null;
        if (workPackageId.HasValue && _workPackageQueryService is not null && (customerId is null || productId is null || notification.ReferenceDisplay is null))
        {
            var wpDto = await _workPackageQueryService.GetWorkPackageByIdAsync(workPackageId.Value, cancellationToken);
            if (wpDto is not null)
            {
                workPackageName = TrimOrNull(wpDto.Name);
                customerId ??= NormalizeOptionalGuid(wpDto.CustomerId);
                productId ??= NormalizeOptionalGuid(wpDto.ProductId);
            }
        }

        var customerName = TrimOrNull(notification.CustomerName);
        if (customerName is null && customerId.HasValue && _customerQueryService is not null)
        {
            var customer = await _customerQueryService.GetCustomerByIdAsync(customerId.Value, cancellationToken);
            customerName = TrimOrNull(customer?.CustomerName);
        }

        var productName = TrimOrNull(notification.ProductName);
        if (productName is null && productId.HasValue && _productQueryService is not null)
        {
            var product = await _productQueryService.GetProductByIdAsync(productId.Value, cancellationToken);
            productName = TrimOrNull(product?.Name);
        }

        var (referenceType, referenceId, referenceDisplay) = ResolvePrimaryReference(
            notification.ReferenceType,
            notification.ReferenceId,
            notification.ReferenceDisplay,
            requestId,
            requestTitle,
            workPackageId,
            workPackageName,
            customerId,
            customerName,
            productId,
            productName);

        var effectiveIsException = isException || exceptionType is not null;
        var status = string.IsNullOrWhiteSpace(notification.Status)
            ? PostStatusNames.Active
            : notification.Status.Trim().ToUpperInvariant();
        var visibility = string.IsNullOrWhiteSpace(notification.Visibility)
            ? PostVisibilityNames.Visible
            : notification.Visibility.Trim().ToUpperInvariant();

        var title = Truncate(notification.Title ?? string.Empty, 255);
        var contentExcerpt = Truncate(notification.Content ?? string.Empty, 500);
        var createdAt = notification.OccurredAtUtc == default ? UtcNow : notification.OccurredAtUtc;

        const string upsertFeedItemSql = """
            IF EXISTS (SELECT 1 FROM [post].[FeedItems] WHERE [PostId] = @PostId)
            BEGIN
                UPDATE [post].[FeedItems]
                SET
                    [AuthorPersonId] = @AuthorPersonId,
                    [AuthorName] = @AuthorName,
                    [PostType] = @PostType,
                    [Source] = @Source,
                    [Title] = @Title,
                    [ContentExcerpt] = @ContentExcerpt,
                    [Summary] = @Summary,
                    [Status] = @Status,
                    [Visibility] = @Visibility,
                    [IsException] = @IsException,
                    [ExceptionType] = @ExceptionType,
                    [ReferenceType] = @ReferenceType,
                    [ReferenceId] = @ReferenceId,
                    [ReferenceDisplay] = @ReferenceDisplay,
                    [CustomerId] = @CustomerId,
                    [CustomerName] = @CustomerName,
                    [ProductId] = @ProductId,
                    [ProductName] = @ProductName,
                    [RequestId] = @RequestId,
                    [WorkPackageId] = @WorkPackageId,
                    [UpdatedAt] = @UpdatedAt,
                    [LastActivityAt] = @LastActivityAt
                WHERE [PostId] = @PostId;
            END
            ELSE
            BEGIN
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
                    0,
                    NULL,
                    N'{}',
                    @CreatedAt,
                    @UpdatedAt,
                    @LastActivityAt
                );
            END;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            upsertFeedItemSql,
            new
            {
                FeedItemId = Guid.NewGuid(),
                notification.PostId,
                AuthorPersonId = authorPersonId,
                AuthorName = TruncateNullable(authorName, 150),
                PostType = postType,
                Source = postType,
                Title = title,
                ContentExcerpt = contentExcerpt,
                Summary = contentExcerpt,
                Status = status,
                Visibility = visibility,
                IsException = effectiveIsException,
                ExceptionType = exceptionType,
                ReferenceType = TruncateNullable(referenceType, 50),
                ReferenceId = referenceId,
                ReferenceDisplay = TruncateNullable(referenceDisplay, 255),
                CustomerId = customerId,
                CustomerName = TruncateNullable(customerName, 150),
                ProductId = productId,
                ProductName = TruncateNullable(productName, 150),
                RequestId = requestId,
                WorkPackageId = workPackageId,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                LastActivityAt = createdAt
            },
            cancellationToken: cancellationToken));

        _logger.LogInformation(
            "FeedProjectionHandler projected PostCreated for PostId {PostId} (CustomerId={CustomerId}, ProductId={ProductId}, RequestId={RequestId}, IsException={IsException})",
            notification.PostId,
            customerId,
            productId,
            requestId,
            effectiveIsException);
    }

    /// <summary>
    /// Handles <see cref="CommentAdded"/> by incrementing <c>CommentCount</c> and updating
    /// <c>LatestCommentExcerpt</c>, <c>UpdatedAt</c>, and <c>LastActivityAt</c> (Architecture §12 Update Triggers).
    /// </summary>
    public async Task Handle(CommentAdded notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification.PostId == Guid.Empty)
        {
            return;
        }

        var latestExcerpt = Truncate(notification.Content ?? string.Empty, 300);
        var occurredAt = notification.OccurredAtUtc == default ? UtcNow : notification.OccurredAtUtc;

        const string updateCommentProjectionSql = """
            UPDATE [post].[FeedItems]
            SET
                [CommentCount] = [CommentCount] + 1,
                [LatestCommentExcerpt] = @LatestCommentExcerpt,
                [UpdatedAt] = CASE
                    WHEN @OccurredAtUtc > [UpdatedAt] THEN @OccurredAtUtc
                    ELSE DATEADD(millisecond, 1, [UpdatedAt])
                END,
                [LastActivityAt] = CASE
                    WHEN @OccurredAtUtc > [LastActivityAt] THEN @OccurredAtUtc
                    ELSE DATEADD(millisecond, 1, [LastActivityAt])
                END
            WHERE [PostId] = @PostId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            updateCommentProjectionSql,
            new
            {
                notification.PostId,
                LatestCommentExcerpt = latestExcerpt,
                OccurredAtUtc = occurredAt
            },
            cancellationToken: cancellationToken));

        _logger.LogInformation(
            "FeedProjectionHandler projected CommentAdded ({CommentId}) for PostId {PostId}",
            notification.CommentId,
            notification.PostId);
    }

    /// <summary>
    /// Handles <see cref="ReactionAdded"/> by atomically updating <c>ReactionCountsJson</c>,
    /// <c>UpdatedAt</c>, and <c>LastActivityAt</c> (Architecture §12 Update Triggers).
    /// </summary>
    public async Task Handle(ReactionAdded notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification.PostId == Guid.Empty)
        {
            return;
        }

        var occurredAt = notification.OccurredAtUtc == default ? UtcNow : notification.OccurredAtUtc;
        await UpdateReactionCountsProjectionAsync(
            notification.PostId,
            notification.ReactionCounts,
            occurredAt,
            cancellationToken);

        _logger.LogInformation(
            "FeedProjectionHandler projected ReactionAdded ({ReactionType}) for PostId {PostId}",
            notification.ReactionType,
            notification.PostId);
    }

    /// <summary>
    /// Handles <see cref="ReactionRemoved"/> by atomically updating <c>ReactionCountsJson</c>,
    /// <c>UpdatedAt</c>, and <c>LastActivityAt</c> (Architecture §12 Update Triggers).
    /// </summary>
    public async Task Handle(ReactionRemoved notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification.PostId == Guid.Empty)
        {
            return;
        }

        var occurredAt = notification.OccurredAtUtc == default ? UtcNow : notification.OccurredAtUtc;
        await UpdateReactionCountsProjectionAsync(
            notification.PostId,
            notification.ReactionCounts,
            occurredAt,
            cancellationToken);

        _logger.LogInformation(
            "FeedProjectionHandler projected ReactionRemoved ({ReactionType}) for PostId {PostId}",
            notification.ReactionType,
            notification.PostId);
    }

    /// <summary>
    /// Handles <see cref="PostVisibilityChanged"/> by updating <c>Visibility</c> (<c>VISIBLE</c> or <c>HIDDEN</c>)
    /// on <c>post.FeedItems</c> (Architecture §12 Update Triggers).
    /// </summary>
    public async Task Handle(PostVisibilityChanged notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification.PostId == Guid.Empty)
        {
            return;
        }

        var newVisibility = string.IsNullOrWhiteSpace(notification.NewVisibility)
            ? PostVisibilityNames.Visible
            : notification.NewVisibility.Trim().ToUpperInvariant();
        var occurredAt = notification.OccurredAtUtc == default ? UtcNow : notification.OccurredAtUtc;

        const string updateVisibilitySql = """
            UPDATE [post].[FeedItems]
            SET
                [Visibility] = @Visibility,
                [UpdatedAt] = CASE
                    WHEN @OccurredAtUtc > [UpdatedAt] THEN @OccurredAtUtc
                    ELSE DATEADD(millisecond, 1, [UpdatedAt])
                END,
                [LastActivityAt] = CASE
                    WHEN @OccurredAtUtc > [LastActivityAt] THEN @OccurredAtUtc
                    ELSE DATEADD(millisecond, 1, [LastActivityAt])
                END
            WHERE [PostId] = @PostId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            updateVisibilitySql,
            new
            {
                notification.PostId,
                Visibility = newVisibility,
                OccurredAtUtc = occurredAt
            },
            cancellationToken: cancellationToken));

        _logger.LogInformation(
            "FeedProjectionHandler projected PostVisibilityChanged to {Visibility} for PostId {PostId}",
            newVisibility,
            notification.PostId);
    }

    /// <summary>
    /// Handles <see cref="PostArchived"/> by setting <c>Status = 'ARCHIVED'</c> on <c>post.FeedItems</c>
    /// (Architecture §12 Update Triggers; §20, §21 Permanent Data Retention).
    /// </summary>
    public async Task Handle(PostArchived notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification.PostId == Guid.Empty)
        {
            return;
        }

        var newStatus = string.IsNullOrWhiteSpace(notification.NewStatus)
            ? PostStatusNames.Archived
            : notification.NewStatus.Trim().ToUpperInvariant();
        var occurredAt = notification.OccurredAtUtc == default ? UtcNow : notification.OccurredAtUtc;

        const string updateStatusSql = """
            UPDATE [post].[FeedItems]
            SET
                [Status] = @Status,
                [UpdatedAt] = CASE
                    WHEN @OccurredAtUtc > [UpdatedAt] THEN @OccurredAtUtc
                    ELSE DATEADD(millisecond, 1, [UpdatedAt])
                END,
                [LastActivityAt] = CASE
                    WHEN @OccurredAtUtc > [LastActivityAt] THEN @OccurredAtUtc
                    ELSE DATEADD(millisecond, 1, [LastActivityAt])
                END
            WHERE [PostId] = @PostId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            updateStatusSql,
            new
            {
                notification.PostId,
                Status = newStatus,
                OccurredAtUtc = occurredAt
            },
            cancellationToken: cancellationToken));

        _logger.LogInformation(
            "FeedProjectionHandler projected PostArchived ({Status}) for PostId {PostId}",
            newStatus,
            notification.PostId);
    }

    /// <summary>
    /// Handles <see cref="RequestEscalated"/> by marking any associated <c>post.FeedItems</c> and <c>post.Posts</c>
    /// as exceptional with <c>IsException = 1</c> and <c>ExceptionType = 'ESCALATION'</c>, or recording a
    /// <c>SYSTEM_GENERATED</c> exception post and feed item when no feed item exists yet for the Request
    /// (Architecture §12, §18).
    /// </summary>
    public async Task Handle(RequestEscalated notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification.RequestId == Guid.Empty)
        {
            return;
        }

        var occurredAt = notification.OccurredAtUtc == default ? UtcNow : notification.OccurredAtUtc;
        await MarkRequestFeedItemsAsExceptionAsync(
            notification.RequestId,
            PostExceptionTypes.Escalation,
            NormalizeOptionalGuid(notification.EscalatedByPersonId),
            notification.EscalationReason,
            nameof(RequestEscalated),
            occurredAt,
            cancellationToken);
    }

    /// <summary>
    /// Handles <see cref="RequestRejected"/> by marking any associated <c>post.FeedItems</c> and <c>post.Posts</c>
    /// as exceptional with <c>IsException = 1</c> and <c>ExceptionType = 'REJECTION'</c>, or recording a
    /// <c>SYSTEM_GENERATED</c> exception post and feed item when no feed item exists yet for the Request
    /// (Architecture §12, §18).
    /// </summary>
    public async Task Handle(RequestRejected notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification.RequestId == Guid.Empty)
        {
            return;
        }

        var occurredAt = notification.OccurredAtUtc == default ? UtcNow : notification.OccurredAtUtc;
        await MarkRequestFeedItemsAsExceptionAsync(
            notification.RequestId,
            PostExceptionTypes.Rejection,
            NormalizeOptionalGuid(notification.RejectedByPersonId),
            notification.RejectionReason,
            nameof(RequestRejected),
            occurredAt,
            cancellationToken);
    }

    /// <summary>
    /// Handles <see cref="RequestStalled"/> by marking any associated <c>post.FeedItems</c> and <c>post.Posts</c>
    /// as exceptional with <c>IsException = 1</c> and <c>ExceptionType = 'STALLED'</c>, or recording a
    /// <c>SYSTEM_GENERATED</c> exception post and feed item when no feed item exists yet for the Request
    /// (Architecture §12, §18; <c>UC-AWR-003</c>).
    /// </summary>
    public async Task Handle(RequestStalled notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification.RequestId == Guid.Empty)
        {
            return;
        }

        var occurredAt = notification.OccurredAtUtc == default ? UtcNow : notification.OccurredAtUtc;
        await MarkRequestFeedItemsAsExceptionAsync(
            notification.RequestId,
            PostExceptionTypes.Stalled,
            NormalizeOptionalGuid(notification.ActorPersonId),
            notification.StalledReason,
            nameof(RequestStalled),
            occurredAt,
            cancellationToken);
    }

    private async Task UpdateReactionCountsProjectionAsync(
        Guid postId,
        IReadOnlyDictionary<string, int>? eventReactionCounts,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        const string totalReactionsForPostSql = """
            SELECT COUNT(1)
            FROM [post].[Reactions]
            WHERE [PostId] = @PostId;
            """;

        const string activeReactionCountsSql = """
            SELECT
                UPPER([ReactionType]) AS [ReactionType],
                COUNT(1) AS [Count]
            FROM [post].[Reactions]
            WHERE [PostId] = @PostId
              AND [IsActive] = 1
              AND [RemovedAt] IS NULL
            GROUP BY UPPER([ReactionType])
            ORDER BY UPPER([ReactionType]) ASC;
            """;

        const string updateFeedReactionCountsSql = """
            UPDATE [post].[FeedItems]
            SET
                [ReactionCountsJson] = @ReactionCountsJson,
                [UpdatedAt] = CASE
                    WHEN @OccurredAtUtc > [UpdatedAt] THEN @OccurredAtUtc
                    ELSE DATEADD(millisecond, 1, [UpdatedAt])
                END,
                [LastActivityAt] = CASE
                    WHEN @OccurredAtUtc > [LastActivityAt] THEN @OccurredAtUtc
                    ELSE DATEADD(millisecond, 1, [LastActivityAt])
                END
            WHERE [PostId] = @PostId;
            """;

        using var connection = _connectionFactory.CreateConnection();

        var totalRowsInDb = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(totalReactionsForPostSql, new { PostId = postId }, cancellationToken: cancellationToken));

        string reactionCountsJson;
        if (totalRowsInDb > 0)
        {
            var rows = await connection.QueryAsync<ReactionCountRow>(
                new CommandDefinition(activeReactionCountsSql, new { PostId = postId }, cancellationToken: cancellationToken));

            var dbCounts = rows.ToDictionary(
                r => r.ReactionType,
                r => r.Count,
                StringComparer.OrdinalIgnoreCase);

            reactionCountsJson = FeedItemDto.SerializeReactionCounts(dbCounts);
        }
        else
        {
            reactionCountsJson = FeedItemDto.SerializeReactionCounts(eventReactionCounts);
        }

        await connection.ExecuteAsync(new CommandDefinition(
            updateFeedReactionCountsSql,
            new
            {
                PostId = postId,
                ReactionCountsJson = reactionCountsJson,
                OccurredAtUtc = occurredAtUtc
            },
            cancellationToken: cancellationToken));
    }

    private async Task MarkRequestFeedItemsAsExceptionAsync(
        Guid requestId,
        string exceptionType,
        Guid? actorPersonId,
        string? reason,
        string sourceEventType,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        const string markExceptionAndCountSql = """
            DECLARE @ExistingCount INT = 0;

            IF OBJECT_ID(N'[post].[Posts]', N'U') IS NOT NULL
            BEGIN
                UPDATE [post].[Posts]
                SET
                    [IsException] = 1,
                    [ExceptionType] = @ExceptionType,
                    [UpdatedAt] = @OccurredAtUtc
                WHERE [RequestId] = @RequestId
                   OR [Id] IN (
                       SELECT [PostId]
                       FROM [post].[PostReferences]
                       WHERE [ReferenceType] = N'REQUEST'
                         AND [ReferenceId] = @RequestId
                         AND [RemovedAt] IS NULL
                   );
            END;

            IF OBJECT_ID(N'[post].[FeedItems]', N'U') IS NOT NULL
            BEGIN
                UPDATE [post].[FeedItems]
                SET
                    [IsException] = 1,
                    [ExceptionType] = @ExceptionType,
                    [UpdatedAt] = CASE
                        WHEN @OccurredAtUtc > [UpdatedAt] THEN @OccurredAtUtc
                        ELSE DATEADD(millisecond, 1, [UpdatedAt])
                    END,
                    [LastActivityAt] = CASE
                        WHEN @OccurredAtUtc > [LastActivityAt] THEN @OccurredAtUtc
                        ELSE DATEADD(millisecond, 1, [LastActivityAt])
                    END
                WHERE [RequestId] = @RequestId
                   OR ([ReferenceType] = N'REQUEST' AND [ReferenceId] = @RequestId);

                SELECT @ExistingCount = COUNT(1)
                FROM [post].[FeedItems]
                WHERE [RequestId] = @RequestId
                   OR ([ReferenceType] = N'REQUEST' AND [ReferenceId] = @RequestId);
            END
            ELSE
            BEGIN
                SET @ExistingCount = -1;
            END;

            SELECT @ExistingCount;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var existingCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            markExceptionAndCountSql,
            new
            {
                RequestId = requestId,
                ExceptionType = exceptionType,
                OccurredAtUtc = occurredAtUtc
            },
            cancellationToken: cancellationToken));

        if (existingCount == 0)
        {
            await CreateSystemExceptionPostAndFeedItemAsync(
                connection,
                requestId,
                exceptionType,
                actorPersonId,
                reason,
                sourceEventType,
                occurredAtUtc,
                cancellationToken);
        }

        _logger.LogInformation(
            "FeedProjectionHandler marked FeedItems for RequestId {RequestId} with ExceptionType {ExceptionType} (ExistingCount={ExistingCount})",
            requestId,
            exceptionType,
            existingCount);
    }

    private async Task CreateSystemExceptionPostAndFeedItemAsync(
        IDbConnection connection,
        Guid requestId,
        string exceptionType,
        Guid? actorPersonId,
        string? reason,
        string sourceEventType,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        string? requestTitle = null;
        Guid? customerId = null;
        string? customerName = null;
        Guid? productId = null;
        string? productName = null;
        Guid? workPackageId = null;

        if (_requestQueryService is not null)
        {
            var requestDto = await _requestQueryService.GetRequestByIdAsync(requestId, cancellationToken);
            if (requestDto is not null)
            {
                requestTitle = TrimOrNull(requestDto.Title);
                customerId = NormalizeOptionalGuid(requestDto.CustomerId);
                customerName = TrimOrNull(requestDto.CustomerName);
                productId = NormalizeOptionalGuid(requestDto.ProductId);
                productName = TrimOrNull(requestDto.ProductName);
                workPackageId = NormalizeOptionalGuid(requestDto.WorkPackageId);
                actorPersonId ??= NormalizeOptionalGuid(requestDto.OwnerPersonId);
            }
        }

        if (customerName is null && customerId.HasValue && _customerQueryService is not null)
        {
            var customer = await _customerQueryService.GetCustomerByIdAsync(customerId.Value, cancellationToken);
            customerName = TrimOrNull(customer?.CustomerName);
        }

        if (productName is null && productId.HasValue && _productQueryService is not null)
        {
            var product = await _productQueryService.GetProductByIdAsync(productId.Value, cancellationToken);
            productName = TrimOrNull(product?.Name);
        }

        string? authorName = null;
        if (actorPersonId.HasValue && _organizationQueryService is not null)
        {
            var person = await _organizationQueryService.GetPersonByIdAsync(actorPersonId.Value, cancellationToken);
            authorName = TrimOrNull(person?.FullName);
        }

        authorName ??= "SYSTEM";

        var label = exceptionType switch
        {
            PostExceptionTypes.Escalation => "Request Escalated",
            PostExceptionTypes.Rejection => "Request Rejected",
            PostExceptionTypes.Stalled => "Request Stalled",
            _ => "Operational Exception"
        };

        var title = Truncate(
            requestTitle is not null ? $"{label}: {requestTitle}" : $"{label} ({requestId:D})",
            255);

        var content = !string.IsNullOrWhiteSpace(reason)
            ? reason.Trim()
            : $"{label} for request {(requestTitle ?? requestId.ToString("D"))}.";
        var contentExcerpt = Truncate(content, 500);
        var referenceDisplay = TruncateNullable(requestTitle ?? requestId.ToString("D"), 255);

        var postId = Guid.NewGuid();
        var feedItemId = Guid.NewGuid();

        const string insertSystemExceptionPostAndFeedItemSql = """
            IF OBJECT_ID(N'[post].[Posts]', N'U') IS NOT NULL
               AND OBJECT_ID(N'[post].[FeedItems]', N'U') IS NOT NULL
            BEGIN
                INSERT INTO [post].[Posts] (
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
                    [ArchivedAt],
                    [ArchivedByPersonId],
                    [CreatedAt],
                    [UpdatedAt]
                ) VALUES (
                    @PostId,
                    @Title,
                    @Content,
                    @AuthorPersonId,
                    N'SYSTEM_GENERATED',
                    @SourceEventType,
                    N'ACTIVE',
                    N'VISIBLE',
                    1,
                    @ExceptionType,
                    @CustomerId,
                    @ProductId,
                    @RequestId,
                    @WorkPackageId,
                    NULL,
                    NULL,
                    @OccurredAtUtc,
                    @OccurredAtUtc
                );

                IF OBJECT_ID(N'[post].[PostReferences]', N'U') IS NOT NULL
                BEGIN
                    INSERT INTO [post].[PostReferences] (
                        [Id],
                        [PostId],
                        [ReferenceType],
                        [ReferenceId],
                        [ReferenceDisplay],
                        [RemovedAt],
                        [CreatedAt],
                        [UpdatedAt]
                    ) VALUES (
                        NEWID(),
                        @PostId,
                        N'REQUEST',
                        @RequestId,
                        @ReferenceDisplay,
                        NULL,
                        @OccurredAtUtc,
                        @OccurredAtUtc
                    );

                    IF @CustomerId IS NOT NULL
                    BEGIN
                        INSERT INTO [post].[PostReferences] (
                            [Id],
                            [PostId],
                            [ReferenceType],
                            [ReferenceId],
                            [ReferenceDisplay],
                            [RemovedAt],
                            [CreatedAt],
                            [UpdatedAt]
                        ) VALUES (
                            NEWID(),
                            @PostId,
                            N'CUSTOMER',
                            @CustomerId,
                            @CustomerName,
                            NULL,
                            @OccurredAtUtc,
                            @OccurredAtUtc
                        );
                    END;

                    IF @ProductId IS NOT NULL
                    BEGIN
                        INSERT INTO [post].[PostReferences] (
                            [Id],
                            [PostId],
                            [ReferenceType],
                            [ReferenceId],
                            [ReferenceDisplay],
                            [RemovedAt],
                            [CreatedAt],
                            [UpdatedAt]
                        ) VALUES (
                            NEWID(),
                            @PostId,
                            N'PRODUCT',
                            @ProductId,
                            @ProductName,
                            NULL,
                            @OccurredAtUtc,
                            @OccurredAtUtc
                        );
                    END;

                    IF @WorkPackageId IS NOT NULL
                    BEGIN
                        INSERT INTO [post].[PostReferences] (
                            [Id],
                            [PostId],
                            [ReferenceType],
                            [ReferenceId],
                            [ReferenceDisplay],
                            [RemovedAt],
                            [CreatedAt],
                            [UpdatedAt]
                        ) VALUES (
                            NEWID(),
                            @PostId,
                            N'WORK_PACKAGE',
                            @WorkPackageId,
                            NULL,
                            NULL,
                            @OccurredAtUtc,
                            @OccurredAtUtc
                        );
                    END;
                END;

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
                    N'SYSTEM_GENERATED',
                    N'SYSTEM_GENERATED',
                    @Title,
                    @ContentExcerpt,
                    @ContentExcerpt,
                    N'ACTIVE',
                    N'VISIBLE',
                    1,
                    @ExceptionType,
                    N'REQUEST',
                    @RequestId,
                    @ReferenceDisplay,
                    @CustomerId,
                    @CustomerName,
                    @ProductId,
                    @ProductName,
                    @RequestId,
                    @WorkPackageId,
                    0,
                    NULL,
                    N'{}',
                    @OccurredAtUtc,
                    @OccurredAtUtc,
                    @OccurredAtUtc
                );
            END;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            insertSystemExceptionPostAndFeedItemSql,
            new
            {
                FeedItemId = feedItemId,
                PostId = postId,
                Title = title,
                Content = content,
                ContentExcerpt = contentExcerpt,
                AuthorPersonId = actorPersonId,
                AuthorName = TruncateNullable(authorName, 150),
                SourceEventType = TruncateNullable(sourceEventType, 100),
                ExceptionType = exceptionType,
                ReferenceDisplay = referenceDisplay,
                CustomerId = customerId,
                CustomerName = TruncateNullable(customerName, 150),
                ProductId = productId,
                ProductName = TruncateNullable(productName, 150),
                RequestId = requestId,
                WorkPackageId = workPackageId,
                OccurredAtUtc = occurredAtUtc
            },
            cancellationToken: cancellationToken));
    }

    internal static string? InferExceptionTypeFromSourceEvent(string? sourceEventType)
    {
        if (string.IsNullOrWhiteSpace(sourceEventType))
        {
            return null;
        }

        var normalized = sourceEventType.Trim().ToUpperInvariant();
        if (normalized.Contains("ESCALAT", StringComparison.Ordinal))
        {
            return PostExceptionTypes.Escalation;
        }

        if (normalized.Contains("REJECT", StringComparison.Ordinal))
        {
            return PostExceptionTypes.Rejection;
        }

        if (normalized.Contains("STALL", StringComparison.Ordinal))
        {
            return PostExceptionTypes.Stalled;
        }

        return null;
    }

    internal static (string? ReferenceType, Guid? ReferenceId, string? ReferenceDisplay) ResolvePrimaryReference(
        string? explicitReferenceType,
        Guid? explicitReferenceId,
        string? explicitReferenceDisplay,
        Guid? requestId,
        string? requestTitle,
        Guid? workPackageId,
        string? workPackageName,
        Guid? customerId,
        string? customerName,
        Guid? productId,
        string? productName)
    {
        var normalizedExplicitId = NormalizeOptionalGuid(explicitReferenceId);
        var normalizedExplicitType = TrimOrNull(explicitReferenceType)?.ToUpperInvariant();
        var normalizedDisplay = TrimOrNull(explicitReferenceDisplay);

        if (normalizedExplicitType is not null && normalizedExplicitId.HasValue)
        {
            normalizedDisplay ??= normalizedExplicitType switch
            {
                PostReferenceTypes.Request => requestTitle,
                PostReferenceTypes.WorkPackage => workPackageName,
                PostReferenceTypes.Customer => customerName,
                PostReferenceTypes.Product => productName,
                _ => null
            };
            return (normalizedExplicitType, normalizedExplicitId, normalizedDisplay);
        }

        if (requestId.HasValue)
        {
            return (PostReferenceTypes.Request, requestId, normalizedDisplay ?? requestTitle);
        }

        if (workPackageId.HasValue)
        {
            return (PostReferenceTypes.WorkPackage, workPackageId, normalizedDisplay ?? workPackageName);
        }

        if (customerId.HasValue)
        {
            return (PostReferenceTypes.Customer, customerId, normalizedDisplay ?? customerName);
        }

        if (productId.HasValue)
        {
            return (PostReferenceTypes.Product, productId, normalizedDisplay ?? productName);
        }

        return (null, null, null);
    }

    internal static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    internal static string? TruncateNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? TrimOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Guid? NormalizeOptionalGuid(Guid? value)
        => value.HasValue && value.Value != Guid.Empty ? value.Value : null;

    private sealed class ReactionCountRow
    {
        public string ReactionType { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
