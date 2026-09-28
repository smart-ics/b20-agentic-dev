namespace ICS.Modules.WorkPackage.Infrastructure;

using System.Data;
using System.Text;
using Dapper;
using ICS.Core.Data;
using ICS.Modules.WorkPackage.Application;
using ICS.Modules.WorkPackage.Domain;

/// <summary>
/// Dapper-based repository implementation for the Work Package aggregate and its request memberships.
/// Uses explicit parameterized SQL exclusively against the workpackage.* schema.
/// Architecture §11, §16, §17, §19.3, §20.
/// </summary>
internal sealed class WorkPackageRepository : IWorkPackageRepository
{
    /// <summary>
    /// The workpackage.* status values treated as "active" for Business Rule 9 purposes.
    /// A DRAFT package is not yet in progress, so it does not block another package.
    /// </summary>
    private const string ActiveStatusForRule9 = WorkPackageStatus.Active;

    private readonly IDbConnectionFactory _connectionFactory;

    public WorkPackageRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<WorkPackage?> GetByIdAsync(Guid workPackageId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT
                Id,
                Name,
                Objective,
                Status,
                OwnerPersonId,
                CustomerId,
                ProductId,
                CreatedAt,
                UpdatedAt,
                ClosedAt,
                CloseReason
            FROM [workpackage].[WorkPackages]
            WHERE Id = @Id;";

        var row = await connection.QuerySingleOrDefaultAsync<WorkPackageRow>(
            sql,
            new { Id = workPackageId });

        if (row is null)
        {
            return null;
        }

        var memberships = await GetMembershipsAsync(workPackageId, cancellationToken);
        return MapToAggregate(row, memberships);
    }

    public async Task AddAsync(WorkPackage workPackage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workPackage);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO [workpackage].[WorkPackages] (
                Id,
                Name,
                Objective,
                Status,
                OwnerPersonId,
                CustomerId,
                ProductId,
                CreatedAt,
                UpdatedAt,
                ClosedAt,
                CloseReason
            ) VALUES (
                @Id,
                @Name,
                @Objective,
                @Status,
                @OwnerPersonId,
                @CustomerId,
                @ProductId,
                @CreatedAt,
                @UpdatedAt,
                @ClosedAt,
                @CloseReason
            );";

        await connection.ExecuteAsync(sql, new
        {
            Id = workPackage.Id,
            workPackage.Name,
            workPackage.Objective,
            workPackage.Status,
            workPackage.OwnerPersonId,
            workPackage.CustomerId,
            workPackage.ProductId,
            workPackage.CreatedAt,
            workPackage.UpdatedAt,
            workPackage.ClosedAt,
            workPackage.CloseReason
        });
    }

    public async Task UpdateAsync(WorkPackage workPackage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workPackage);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            UPDATE [workpackage].[WorkPackages]
            SET
                Name = @Name,
                Objective = @Objective,
                Status = @Status,
                OwnerPersonId = @OwnerPersonId,
                CustomerId = @CustomerId,
                ProductId = @ProductId,
                UpdatedAt = @UpdatedAt,
                ClosedAt = @ClosedAt,
                CloseReason = @CloseReason
            WHERE Id = @Id;";

        await connection.ExecuteAsync(sql, new
        {
            Id = workPackage.Id,
            workPackage.Name,
            workPackage.Objective,
            workPackage.Status,
            workPackage.OwnerPersonId,
            workPackage.CustomerId,
            workPackage.ProductId,
            workPackage.UpdatedAt,
            workPackage.ClosedAt,
            workPackage.CloseReason
        });
    }

    public async Task<bool> IsRequestInActiveWorkPackageAsync(
        Guid requestId,
        Guid? excludingWorkPackageId = null,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            return false;
        }

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // Cross-aggregate enforcement of Business Rule 9: an active membership link in an
        // ACTIVE Work Package (optionally excluding the package being modified).
        const string sql = @"
            SELECT CAST(1 AS BIT)
            FROM [workpackage].[WorkPackageRequests] AS wpr
            INNER JOIN [workpackage].[WorkPackages] AS wp
                ON wp.Id = wpr.WorkPackageId
            WHERE wpr.RequestId = @RequestId
              AND wpr.IsActive = 1
              AND wp.Status = @ActiveStatus
              AND (@ExcludingWorkPackageId IS NULL OR wpr.WorkPackageId <> @ExcludingWorkPackageId);";

        var result = await connection.QuerySingleOrDefaultAsync<bool?>(sql, new
        {
            RequestId = requestId,
            ActiveStatus = ActiveStatusForRule9,
            ExcludingWorkPackageId = excludingWorkPackageId
        });

        return result ?? false;
    }

    public async Task<int> AddRequestMembershipAsync(
        WorkPackage workPackage,
        WorkPackageRequest membership,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workPackage);
        ArgumentNullException.ThrowIfNull(membership);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Re-read the aggregate row under the transaction so a concurrent status change is observed.
        const string readStatusSql = @"
            SELECT Status
            FROM [workpackage].[WorkPackages] WITH (UPDLOCK, ROWLOCK)
            WHERE Id = @Id;";

        var currentStatus = await connection.QuerySingleOrDefaultAsync<string>(
            readStatusSql,
            new { Id = workPackage.Id },
            transaction);

        if (currentStatus is null)
        {
            throw new WorkPackageNotFoundException(
                workPackage.Id,
                $"Work Package '{workPackage.Id}' does not exist.");
        }

        // Guard the active-link uniqueness at the storage level as well as in the aggregate.
        const string insertSql = @"
            IF EXISTS (
                SELECT 1
                FROM [workpackage].[WorkPackageRequests]
                WHERE WorkPackageId = @WorkPackageId
                  AND RequestId = @RequestId
                  AND IsActive = 1
            )
            BEGIN
                SELECT 0;
            END
            ELSE
            BEGIN
                INSERT INTO [workpackage].[WorkPackageRequests] (
                    Id,
                    WorkPackageId,
                    RequestId,
                    AddedAt,
                    RemovedAt,
                    IsActive
                ) VALUES (
                    @Id,
                    @WorkPackageId,
                    @RequestId,
                    @AddedAt,
                    @RemovedAt,
                    @IsActive
                );

                UPDATE [workpackage].[WorkPackages]
                SET UpdatedAt = @AddedAt
                WHERE Id = @WorkPackageId;

                SELECT 1;
            END;";

        var affected = await connection.QuerySingleAsync<int>(
            insertSql,
            new
            {
                membership.Id,
                membership.WorkPackageId,
                membership.RequestId,
                membership.AddedAt,
                membership.RemovedAt,
                membership.IsActive
            },
            transaction);

        await transaction.CommitAsync(cancellationToken);
        return affected;
    }

    public async Task<int> DeactivateRequestMembershipAsync(
        Guid workPackageId,
        Guid requestId,
        DateTime removedAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Historical membership rows are preserved (Business Rule 15); only IsActive flips.
        // The membership row count is captured before the second UPDATE resets @@ROWCOUNT.
        const string sql = @"
            UPDATE [workpackage].[WorkPackageRequests]
            SET IsActive = 0,
                RemovedAt = @RemovedAt
            WHERE WorkPackageId = @WorkPackageId
              AND RequestId = @RequestId
              AND IsActive = 1;

            DECLARE @DeactivatedCount INT = @@ROWCOUNT;

            UPDATE [workpackage].[WorkPackages]
            SET UpdatedAt = @RemovedAt
            WHERE Id = @WorkPackageId;

            SELECT @DeactivatedCount;";

        var affected = await connection.QuerySingleAsync<int>(
            sql,
            new
            {
                WorkPackageId = workPackageId,
                RequestId = requestId,
                RemovedAt = removedAt
            },
            transaction);

        await transaction.CommitAsync(cancellationToken);
        return affected;
    }

    public async Task AddStateHistoryAsync(
        Guid workPackageId,
        string? fromStatus,
        string toStatus,
        Guid actorPersonId,
        string? reason,
        DateTime changedAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO [workpackage].[WorkPackageStateHistories] (
                StateHistoryId,
                WorkPackageId,
                FromStatus,
                ToStatus,
                ActorPersonId,
                Reason,
                ChangedAt
            ) VALUES (
                @StateHistoryId,
                @WorkPackageId,
                @FromStatus,
                @ToStatus,
                @ActorPersonId,
                @Reason,
                @ChangedAt
            );";

        await connection.ExecuteAsync(sql, new
        {
            StateHistoryId = Guid.NewGuid(),
            WorkPackageId = workPackageId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ActorPersonId = actorPersonId,
            Reason = reason,
            ChangedAt = changedAt
        });
    }

    public async Task<IReadOnlyList<WorkPackage>> ListAsync(
        WorkPackageGridFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var (whereClause, parameters) = BuildWhereClause(filter);

        var sql = new StringBuilder(@"
            SELECT
                wp.Id,
                wp.Name,
                wp.Objective,
                wp.Status,
                wp.OwnerPersonId,
                wp.CustomerId,
                wp.ProductId,
                wp.CreatedAt,
                wp.UpdatedAt,
                wp.ClosedAt,
                wp.CloseReason
            FROM [workpackage].[WorkPackages] AS wp
            ")
            .Append(whereClause)
            .Append(" ORDER BY wp.CreatedAt DESC;");

        var rows = await connection.QueryAsync<WorkPackageRow>(sql.ToString(), parameters);
        return rows.Select(r => MapToAggregate(r)).ToList();
    }

    public async Task<(int TotalCount, IReadOnlyList<WorkPackage> Items)> GetFilteredGridAsync(
        WorkPackageGridFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var (whereClause, parameters) = BuildWhereClause(filter);

        var countSql = $@"
            SELECT COUNT(*)
            FROM [workpackage].[WorkPackages] AS wp
            {whereClause};";

        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

        var pageSql = new StringBuilder(@"
            SELECT
                wp.Id,
                wp.Name,
                wp.Objective,
                wp.Status,
                wp.OwnerPersonId,
                wp.CustomerId,
                wp.ProductId,
                wp.CreatedAt,
                wp.UpdatedAt,
                wp.ClosedAt,
                wp.CloseReason
            FROM [workpackage].[WorkPackages] AS wp
            ")
            .Append(whereClause)
            .Append(" ORDER BY wp.CreatedAt DESC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;");

        parameters.Add("Skip", Math.Max(0, filter.Skip));
        parameters.Add("Take", filter.Take <= 0 ? 50 : filter.Take);

        var rows = await connection.QueryAsync<WorkPackageRow>(pageSql.ToString(), parameters);
        var items = rows.Select(r => MapToAggregate(r)).ToList();

        return (totalCount, items);
    }

    public async Task<IReadOnlyList<WorkPackageRequest>> GetMembershipsAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT
                Id,
                WorkPackageId,
                RequestId,
                AddedAt,
                RemovedAt,
                IsActive
            FROM [workpackage].[WorkPackageRequests]
            WHERE WorkPackageId = @WorkPackageId
            ORDER BY AddedAt ASC;";

        var rows = await connection.QueryAsync<WorkPackageRequestRow>(
            sql,
            new { WorkPackageId = workPackageId });

        return rows.Select(r => MapToMembership(r)).ToList();
    }

    public async Task<(WorkPackageRequest Membership, string HolderStatus)?> GetActiveMembershipForRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            return null;
        }

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // The holding package's status is required to distinguish a package that still holds the
        // request (DRAFT / ACTIVE) from one that has been closed and therefore released it.
        const string sql = @"
            SELECT TOP (1)
                wpr.Id,
                wpr.WorkPackageId,
                wpr.RequestId,
                wpr.AddedAt,
                wpr.RemovedAt,
                wpr.IsActive,
                wp.Status AS HolderStatus
            FROM [workpackage].[WorkPackageRequests] AS wpr
            INNER JOIN [workpackage].[WorkPackages] AS wp
                ON wp.Id = wpr.WorkPackageId
            WHERE wpr.RequestId = @RequestId
              AND wpr.IsActive = 1
            ORDER BY wpr.AddedAt DESC;";

        var row = await connection.QuerySingleOrDefaultAsync<WorkPackageRequestWithHolderRow>(
            sql,
            new { RequestId = requestId });

        if (row is null)
        {
            return null;
        }

        return (MapToMembership(new WorkPackageRequestRow
        {
            Id = row.Id,
            WorkPackageId = row.WorkPackageId,
            RequestId = row.RequestId,
            AddedAt = row.AddedAt,
            RemovedAt = row.RemovedAt,
            IsActive = row.IsActive
        }), row.HolderStatus);
    }

    public async Task<IReadOnlyList<WorkPackageStateHistoryDto>> GetStateHistoryAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT
                StateHistoryId,
                WorkPackageId,
                FromStatus,
                ToStatus,
                ActorPersonId,
                Reason,
                ChangedAt
            FROM [workpackage].[WorkPackageStateHistories]
            WHERE WorkPackageId = @WorkPackageId
            ORDER BY ChangedAt ASC;";

        var rows = await connection.QueryAsync<WorkPackageStateHistoryRow>(
            sql,
            new { WorkPackageId = workPackageId });

        return rows
            .Select(r => new WorkPackageStateHistoryDto(
                r.StateHistoryId,
                r.WorkPackageId,
                r.FromStatus,
                r.ToStatus,
                r.ActorPersonId,
                ActorName: null,
                r.Reason,
                r.ChangedAt))
            .ToList();
    }

    private static (string WhereClause, DynamicParameters Parameters) BuildWhereClause(WorkPackageGridFilterDto filter)
    {
        var whereClause = new StringBuilder("WHERE 1=1");
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            whereClause.Append(" AND (wp.Name LIKE @SearchPattern OR wp.Objective LIKE @SearchPattern)");
            parameters.Add("SearchPattern", $"%{filter.SearchTerm.Trim()}%");
        }

        if (filter.OwnerPersonId.HasValue && filter.OwnerPersonId.Value != Guid.Empty)
        {
            whereClause.Append(" AND wp.OwnerPersonId = @OwnerPersonId");
            parameters.Add("OwnerPersonId", filter.OwnerPersonId.Value);
        }

        if (filter.CustomerId.HasValue && filter.CustomerId.Value != Guid.Empty)
        {
            whereClause.Append(" AND wp.CustomerId = @CustomerId");
            parameters.Add("CustomerId", filter.CustomerId.Value);
        }

        if (filter.ProductId.HasValue && filter.ProductId.Value != Guid.Empty)
        {
            whereClause.Append(" AND wp.ProductId = @ProductId");
            parameters.Add("ProductId", filter.ProductId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Statuses))
        {
            var statusList = filter.Statuses
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToUpperInvariant())
                .Where(s => WorkPackageStatus.IsValid(s))
                .ToList();

            if (statusList.Count > 0)
            {
                whereClause.Append(" AND wp.Status IN @Statuses");
                parameters.Add("Statuses", statusList);
            }
        }

        if (filter.FromDate.HasValue)
        {
            whereClause.Append(" AND wp.CreatedAt >= @FromDate");
            parameters.Add("FromDate", filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            whereClause.Append(" AND wp.CreatedAt <= @ToDate");
            parameters.Add("ToDate", filter.ToDate.Value);
        }

        return (whereClause.ToString(), parameters);
    }

    private static WorkPackage MapToAggregate(WorkPackageRow row, IEnumerable<WorkPackageRequest>? memberships = null) =>
        new(
            row.Id,
            row.Name,
            row.Objective,
            row.Status,
            row.OwnerPersonId,
            row.CustomerId,
            row.ProductId,
            row.CreatedAt,
            row.UpdatedAt,
            row.ClosedAt,
            row.CloseReason,
            memberships);

    private static WorkPackageRequest MapToMembership(WorkPackageRequestRow row) =>
        new(row.Id, row.WorkPackageId, row.RequestId, row.AddedAt, row.RemovedAt, row.IsActive);

    private sealed class WorkPackageRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Objective { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public Guid OwnerPersonId { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? ProductId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public string? CloseReason { get; set; }
    }

    private sealed class WorkPackageRequestRow
    {
        public Guid Id { get; set; }
        public Guid WorkPackageId { get; set; }
        public Guid RequestId { get; set; }
        public DateTime AddedAt { get; set; }
        public DateTime? RemovedAt { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Membership row joined with the status of the Work Package currently holding it.
    /// </summary>
    private sealed class WorkPackageRequestWithHolderRow
    {
        public Guid Id { get; set; }
        public Guid WorkPackageId { get; set; }
        public Guid RequestId { get; set; }
        public DateTime AddedAt { get; set; }
        public DateTime? RemovedAt { get; set; }
        public bool IsActive { get; set; }
        public string HolderStatus { get; set; } = string.Empty;
    }

    private sealed class WorkPackageStateHistoryRow    {
        public Guid StateHistoryId { get; set; }
        public Guid WorkPackageId { get; set; }
        public string? FromStatus { get; set; }
        public string ToStatus { get; set; } = string.Empty;
        public Guid ActorPersonId { get; set; }
        public string? Reason { get; set; }
        public DateTime ChangedAt { get; set; }
    }
}
