using System.Data;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.WorkPackage.Domain;
using Dapper;

namespace Cakra.Modules.WorkPackage.Persistence;

/// <summary>
/// Dapper implementation of <see cref="IWorkPackageRepository"/> using explicit parameterized SQL
/// exclusively against <c>workpackage.WorkPackages</c> and <c>workpackage.WorkPackageRequests</c>
/// (Architecture §6, §11, §17, §19.3, §20, §21).
/// </summary>
internal sealed class WorkPackageRepository : IWorkPackageRepository
{
    static WorkPackageRepository()
    {
        SqlMapper.AddTypeMap(typeof(DateTime), DbType.DateTime2);
        SqlMapper.AddTypeMap(typeof(DateTime?), DbType.DateTime2);
    }

    private readonly IDbConnectionFactory _connectionFactory;

    public WorkPackageRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Domain.WorkPackage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        const string packageSql = """
            SELECT
                [Id],
                [Name],
                [Objective],
                [Status],
                [OwnerPersonId],
                [CustomerId],
                [ProductId],
                [ClosedReason],
                [ClosedAt],
                [CreatedAt],
                [UpdatedAt]
            FROM [workpackage].[WorkPackages]
            WHERE [Id] = @Id;
            """;

        const string membershipsSql = """
            SELECT
                [Id],
                [WorkPackageId],
                [RequestId],
                [SortOrder],
                [AddedAt],
                [RemovedAt],
                [CreatedAt],
                [UpdatedAt]
            FROM [workpackage].[WorkPackageRequests]
            WHERE [WorkPackageId] = @WorkPackageId
            ORDER BY [SortOrder] ASC, [AddedAt] ASC, [CreatedAt] ASC, [Id] ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<WorkPackageRow>(
            new CommandDefinition(packageSql, new { Id = id }, cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        var membershipRows = await connection.QueryAsync<WorkPackageRequestRow>(
            new CommandDefinition(membershipsSql, new { WorkPackageId = id }, cancellationToken: cancellationToken));

        var memberships = membershipRows.Select(m => m.ToDomain()).ToList();
        return row.ToDomain(memberships);
    }

    public async Task<IReadOnlyList<Domain.WorkPackage>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string packagesSql = """
            SELECT
                [Id],
                [Name],
                [Objective],
                [Status],
                [OwnerPersonId],
                [CustomerId],
                [ProductId],
                [ClosedReason],
                [ClosedAt],
                [CreatedAt],
                [UpdatedAt]
            FROM [workpackage].[WorkPackages]
            ORDER BY [CreatedAt] DESC, [Id] ASC;
            """;

        const string membershipsSql = """
            SELECT
                [Id],
                [WorkPackageId],
                [RequestId],
                [SortOrder],
                [AddedAt],
                [RemovedAt],
                [CreatedAt],
                [UpdatedAt]
            FROM [workpackage].[WorkPackageRequests]
            ORDER BY [WorkPackageId] ASC, [SortOrder] ASC, [AddedAt] ASC, [CreatedAt] ASC, [Id] ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();

        var packageRows = (await connection.QueryAsync<WorkPackageRow>(
            new CommandDefinition(packagesSql, cancellationToken: cancellationToken))).AsList();

        if (packageRows.Count == 0)
        {
            return Array.Empty<Domain.WorkPackage>();
        }

        var membershipRows = await connection.QueryAsync<WorkPackageRequestRow>(
            new CommandDefinition(membershipsSql, cancellationToken: cancellationToken));

        var membershipsByPackage = membershipRows
            .GroupBy(m => m.WorkPackageId)
            .ToDictionary(g => g.Key, g => (IEnumerable<WorkPackageRequest>)g.Select(m => m.ToDomain()).ToList());

        return packageRows
            .Select(r => r.ToDomain(membershipsByPackage.TryGetValue(r.Id, out var list) ? list : null))
            .ToList();
    }

    public async Task AddAsync(Domain.WorkPackage entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
        }

        if (entity.CreatedAt == default)
        {
            entity.CreatedAt = DateTime.UtcNow;
        }

        const string sql = """
            INSERT INTO [workpackage].[WorkPackages] (
                [Id],
                [Name],
                [Objective],
                [Status],
                [OwnerPersonId],
                [CustomerId],
                [ProductId],
                [ClosedReason],
                [ClosedAt],
                [CreatedAt],
                [UpdatedAt]
            ) VALUES (
                @Id,
                @Name,
                @Objective,
                @Status,
                @OwnerPersonId,
                @CustomerId,
                @ProductId,
                @ClosedReason,
                @ClosedAt,
                @CreatedAt,
                @UpdatedAt
            );
            """;

        const string insertMembershipSql = """
            INSERT INTO [workpackage].[WorkPackageRequests] (
                [Id],
                [WorkPackageId],
                [RequestId],
                [SortOrder],
                [AddedAt],
                [RemovedAt],
                [CreatedAt],
                [UpdatedAt]
            ) VALUES (
                @Id,
                @WorkPackageId,
                @RequestId,
                @SortOrder,
                @AddedAt,
                @RemovedAt,
                @CreatedAt,
                @UpdatedAt
            );
            """;

        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            entity.Id,
            entity.Name,
            entity.Objective,
            Status = entity.Status.ToName(),
            entity.OwnerPersonId,
            entity.CustomerId,
            entity.ProductId,
            entity.ClosedReason,
            entity.ClosedAt,
            entity.CreatedAt,
            entity.UpdatedAt
        }, transaction: transaction, cancellationToken: cancellationToken));

        foreach (var membership in entity.Requests)
        {
            await connection.ExecuteAsync(new CommandDefinition(insertMembershipSql, new
            {
                membership.Id,
                membership.WorkPackageId,
                membership.RequestId,
                membership.SortOrder,
                membership.AddedAt,
                membership.RemovedAt,
                membership.CreatedAt,
                membership.UpdatedAt
            }, transaction: transaction, cancellationToken: cancellationToken));
        }

        transaction.Commit();
    }

    public async Task UpdateAsync(Domain.WorkPackage entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.UpdatedAt ??= DateTime.UtcNow;

        const string sql = """
            UPDATE [workpackage].[WorkPackages]
            SET
                [Name] = @Name,
                [Objective] = @Objective,
                [Status] = @Status,
                [OwnerPersonId] = @OwnerPersonId,
                [CustomerId] = @CustomerId,
                [ProductId] = @ProductId,
                [ClosedReason] = @ClosedReason,
                [ClosedAt] = @ClosedAt,
                [UpdatedAt] = @UpdatedAt
            WHERE [Id] = @Id;
            """;

        const string getExistingMembershipIdsSql = """
            SELECT [Id]
            FROM [workpackage].[WorkPackageRequests]
            WHERE [WorkPackageId] = @WorkPackageId;
            """;

        const string insertMembershipSql = """
            INSERT INTO [workpackage].[WorkPackageRequests] (
                [Id],
                [WorkPackageId],
                [RequestId],
                [SortOrder],
                [AddedAt],
                [RemovedAt],
                [CreatedAt],
                [UpdatedAt]
            ) VALUES (
                @Id,
                @WorkPackageId,
                @RequestId,
                @SortOrder,
                @AddedAt,
                @RemovedAt,
                @CreatedAt,
                @UpdatedAt
            );
            """;

        const string updateMembershipSql = """
            UPDATE [workpackage].[WorkPackageRequests]
            SET
                [SortOrder] = @SortOrder,
                [RemovedAt] = @RemovedAt,
                [UpdatedAt] = @UpdatedAt
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            entity.Id,
            entity.Name,
            entity.Objective,
            Status = entity.Status.ToName(),
            entity.OwnerPersonId,
            entity.CustomerId,
            entity.ProductId,
            entity.ClosedReason,
            entity.ClosedAt,
            entity.UpdatedAt
        }, transaction: transaction, cancellationToken: cancellationToken));

        var existingIds = (await connection.QueryAsync<Guid>(new CommandDefinition(
            getExistingMembershipIdsSql,
            new { WorkPackageId = entity.Id },
            transaction: transaction,
            cancellationToken: cancellationToken))).ToHashSet();

        foreach (var membership in entity.Requests)
        {
            if (existingIds.Contains(membership.Id))
            {
                await connection.ExecuteAsync(new CommandDefinition(updateMembershipSql, new
                {
                    membership.Id,
                    membership.SortOrder,
                    membership.RemovedAt,
                    membership.UpdatedAt
                }, transaction: transaction, cancellationToken: cancellationToken));
            }
            else
            {
                await connection.ExecuteAsync(new CommandDefinition(insertMembershipSql, new
                {
                    membership.Id,
                    membership.WorkPackageId,
                    membership.RequestId,
                    membership.SortOrder,
                    membership.AddedAt,
                    membership.RemovedAt,
                    membership.CreatedAt,
                    membership.UpdatedAt
                }, transaction: transaction, cancellationToken: cancellationToken));
            }
        }

        transaction.Commit();
    }

    public Task SaveAsync(Domain.WorkPackage entity, CancellationToken cancellationToken = default)
        => UpdateAsync(entity, cancellationToken);

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            DELETE FROM [workpackage].[WorkPackages]
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task AddRequestMembershipAsync(
        WorkPackageRequest membership,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(membership);

        if (membership.Id == Guid.Empty)
        {
            membership.Id = Guid.NewGuid();
        }

        if (membership.CreatedAt == default)
        {
            membership.CreatedAt = membership.AddedAt;
        }

        const string sql = """
            INSERT INTO [workpackage].[WorkPackageRequests] (
                [Id],
                [WorkPackageId],
                [RequestId],
                [SortOrder],
                [AddedAt],
                [RemovedAt],
                [CreatedAt],
                [UpdatedAt]
            ) VALUES (
                @Id,
                @WorkPackageId,
                @RequestId,
                @SortOrder,
                @AddedAt,
                @RemovedAt,
                @CreatedAt,
                @UpdatedAt
            );
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            membership.Id,
            membership.WorkPackageId,
            membership.RequestId,
            membership.SortOrder,
            membership.AddedAt,
            membership.RemovedAt,
            membership.CreatedAt,
            membership.UpdatedAt
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateRequestMembershipAsync(
        WorkPackageRequest membership,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(membership);

        membership.UpdatedAt ??= membership.RemovedAt ?? DateTime.UtcNow;

        const string sql = """
            UPDATE [workpackage].[WorkPackageRequests]
            SET
                [SortOrder] = @SortOrder,
                [RemovedAt] = @RemovedAt,
                [UpdatedAt] = @UpdatedAt
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            membership.Id,
            membership.SortOrder,
            membership.RemovedAt,
            membership.UpdatedAt
        }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<WorkPackageRequest>> GetMembershipsByWorkPackageIdAsync(
        Guid workPackageId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                [Id],
                [WorkPackageId],
                [RequestId],
                [SortOrder],
                [AddedAt],
                [RemovedAt],
                [CreatedAt],
                [UpdatedAt]
            FROM [workpackage].[WorkPackageRequests]
            WHERE [WorkPackageId] = @WorkPackageId
            ORDER BY [SortOrder] ASC, [AddedAt] ASC, [CreatedAt] ASC, [Id] ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<WorkPackageRequestRow>(
            new CommandDefinition(sql, new { WorkPackageId = workPackageId }, cancellationToken: cancellationToken));

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public bool IsRequestInActiveWorkPackage(Guid requestId, Guid? excludingWorkPackageId = null)
        => IsRequestInActiveWorkPackageAsync(requestId, excludingWorkPackageId, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

    public async Task<bool> IsRequestInActiveWorkPackageAsync(
        Guid requestId,
        Guid? excludingWorkPackageId = null,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            return false;
        }

        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM [workpackage].[WorkPackageRequests] wpr
                INNER JOIN [workpackage].[WorkPackages] wp ON wp.[Id] = wpr.[WorkPackageId]
                WHERE wpr.[RequestId] = @RequestId
                  AND wpr.[RemovedAt] IS NULL
                  AND wp.[Status] IN ('DRAFT', 'ACTIVE')
                  AND (@ExcludingWorkPackageId IS NULL OR wpr.[WorkPackageId] <> @ExcludingWorkPackageId)
            ) THEN 1 ELSE 0 END;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                sql,
                new { RequestId = requestId, ExcludingWorkPackageId = excludingWorkPackageId },
                cancellationToken: cancellationToken));

        return result == 1;
    }

    private sealed class WorkPackageRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Objective { get; init; } = string.Empty;
        public string Status { get; init; } = WorkPackageStatusNames.Draft;
        public Guid OwnerPersonId { get; init; }
        public Guid? CustomerId { get; init; }
        public Guid? ProductId { get; init; }
        public string? ClosedReason { get; init; }
        public DateTime? ClosedAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }

        public Domain.WorkPackage ToDomain(IEnumerable<WorkPackageRequest>? requests = null)
        {
            return Domain.WorkPackage.Rehydrate(
                Id,
                Name,
                Objective,
                WorkPackageStatusNames.FromName(Status),
                OwnerPersonId,
                CustomerId,
                ProductId,
                ClosedReason,
                ClosedAt,
                CreatedAt,
                UpdatedAt,
                requests);
        }
    }

    private sealed class WorkPackageRequestRow
    {
        public Guid Id { get; init; }
        public Guid WorkPackageId { get; init; }
        public Guid RequestId { get; init; }
        public int SortOrder { get; init; }
        public DateTime AddedAt { get; init; }
        public DateTime? RemovedAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }

        public WorkPackageRequest ToDomain() =>
            WorkPackageRequest.Rehydrate(
                Id,
                WorkPackageId,
                RequestId,
                AddedAt,
                RemovedAt,
                CreatedAt,
                UpdatedAt,
                SortOrder);
    }
}
