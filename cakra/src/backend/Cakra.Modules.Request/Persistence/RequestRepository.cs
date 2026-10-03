using System.Data;
using Cakra.Core.Infrastructure.Persistence;
using Cakra.Modules.Request.Domain;
using Dapper;

namespace Cakra.Modules.Request.Persistence;

/// <summary>
/// Dapper implementation of <see cref="IRequestRepository"/> using explicit parameterized SQL
/// exclusively against <c>request.Requests</c>, <c>request.RequestResolutions</c>, and
/// <c>request.RequestAssignments</c> (Architecture §6, §17, §18, §19.3, §20, §21).
/// </summary>
internal sealed class RequestRepository : IRequestRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    static RequestRepository()
    {
        SqlMapper.AddTypeMap(typeof(DateTime), DbType.DateTime2);
        SqlMapper.AddTypeMap(typeof(DateTime?), DbType.DateTime2);
    }

    public RequestRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Domain.Request?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string requestSql = """
            SELECT
                [Id],
                [Title],
                [Description],
                [RequestType],
                [Status],
                [Priority],
                [Complexity],
                [TotalSubTasksCount],
                [CompletedSubTasksCount],
                [CompletionPercentage],
                [OwnerPersonId],
                [CustomerId],
                [ProductId],
                [WorkPackageId],
                [EvaluationNotes],
                [EscalationReason],
                [ManagementDecisionNotes],
                [CreatedAt],
                [UpdatedAt]
            FROM [request].[Requests]
            WHERE [Id] = @Id;
            """;

        const string resolutionSql = """
            SELECT
                [Id],
                [RequestId],
                [Outcome],
                [Description],
                [ResolvedBy],
                [ResolvedAt],
                [CreatedAt],
                [UpdatedAt]
            FROM [request].[RequestResolutions]
            WHERE [RequestId] = @RequestId;
            """;

        const string assignmentsSql = """
            SELECT
                [Id],
                [RequestId],
                [PreviousOwnerPersonId],
                [AssignedOwnerPersonId],
                [ActorPersonId],
                [PreviousStatus],
                [NewStatus],
                [AssignedAtUtc],
                [Notes],
                [CreatedAt],
                [UpdatedAt]
            FROM [request].[RequestAssignments]
            WHERE [RequestId] = @RequestId
            ORDER BY [AssignedAtUtc] ASC, [CreatedAt] ASC;
            """;

        const string subTasksSql = """
            SELECT
                [Id],
                [RequestId],
                [Title],
                [IsCompleted],
                [AssigneePersonId],
                [CompletedAt],
                [CompletedByPersonId],
                [SortOrder],
                [CreatedAt],
                [UpdatedAt]
            FROM [request].[RequestSubTasks]
            WHERE [RequestId] = @RequestId
            ORDER BY [SortOrder] ASC, [CreatedAt] ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<RequestRow>(
            new CommandDefinition(requestSql, new { Id = id }, cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        var resolutionRow = await connection.QuerySingleOrDefaultAsync<RequestResolutionRow>(
            new CommandDefinition(resolutionSql, new { RequestId = id }, cancellationToken: cancellationToken));

        var assignmentRows = await connection.QueryAsync<RequestAssignmentRow>(
            new CommandDefinition(assignmentsSql, new { RequestId = id }, cancellationToken: cancellationToken));

        var subTaskRows = await connection.QueryAsync<RequestSubTaskRow>(
            new CommandDefinition(subTasksSql, new { RequestId = id }, cancellationToken: cancellationToken));

        var resolution = resolutionRow?.ToDomain();
        var assignments = assignmentRows.Select(a => a.ToDomain()).ToList();
        var subTasks = subTaskRows.Select(st => st.ToDomain()).ToList();

        return row.ToDomain(resolution, assignments, subTasks);
    }

    public async Task<IReadOnlyList<Domain.Request>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                [Id],
                [Title],
                [Description],
                [RequestType],
                [Status],
                [Priority],
                [Complexity],
                [TotalSubTasksCount],
                [CompletedSubTasksCount],
                [CompletionPercentage],
                [OwnerPersonId],
                [CustomerId],
                [ProductId],
                [WorkPackageId],
                [EvaluationNotes],
                [EscalationReason],
                [ManagementDecisionNotes],
                [CreatedAt],
                [UpdatedAt]
            FROM [request].[Requests]
            ORDER BY [CreatedAt] DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<RequestRow>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task AddAsync(Domain.Request entity, CancellationToken cancellationToken = default)
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
            INSERT INTO [request].[Requests] (
                [Id],
                [Title],
                [Description],
                [RequestType],
                [Status],
                [Priority],
                [Complexity],
                [TotalSubTasksCount],
                [CompletedSubTasksCount],
                [CompletionPercentage],
                [OwnerPersonId],
                [CustomerId],
                [ProductId],
                [WorkPackageId],
                [EvaluationNotes],
                [EscalationReason],
                [ManagementDecisionNotes],
                [CreatedAt],
                [UpdatedAt]
            ) VALUES (
                @Id,
                @Title,
                @Description,
                @RequestType,
                @Status,
                @Priority,
                @Complexity,
                @TotalSubTasksCount,
                @CompletedSubTasksCount,
                @CompletionPercentage,
                @OwnerPersonId,
                @CustomerId,
                @ProductId,
                @WorkPackageId,
                @EvaluationNotes,
                @EscalationReason,
                @ManagementDecisionNotes,
                @CreatedAt,
                @UpdatedAt
            );
            """;

        const string insertSubTaskSql = """
            INSERT INTO [request].[RequestSubTasks] (
                [Id],
                [RequestId],
                [Title],
                [IsCompleted],
                [AssigneePersonId],
                [CompletedAt],
                [CompletedByPersonId],
                [SortOrder],
                [CreatedAt],
                [UpdatedAt]
            ) VALUES (
                @Id,
                @RequestId,
                @Title,
                @IsCompleted,
                @AssigneePersonId,
                @CompletedAt,
                @CompletedByPersonId,
                @SortOrder,
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
            entity.Title,
            entity.Description,
            entity.RequestType,
            Status = entity.Status.ToName(),
            entity.Priority,
            entity.Complexity,
            entity.TotalSubTasksCount,
            entity.CompletedSubTasksCount,
            entity.CompletionPercentage,
            entity.OwnerPersonId,
            entity.CustomerId,
            entity.ProductId,
            entity.WorkPackageId,
            entity.EvaluationNotes,
            entity.EscalationReason,
            entity.ManagementDecisionNotes,
            entity.CreatedAt,
            entity.UpdatedAt
        }, transaction: transaction, cancellationToken: cancellationToken));

        foreach (var subTask in entity.SubTasks)
        {
            await connection.ExecuteAsync(new CommandDefinition(insertSubTaskSql, new
            {
                subTask.Id,
                subTask.RequestId,
                subTask.Title,
                subTask.IsCompleted,
                subTask.AssigneePersonId,
                subTask.CompletedAt,
                subTask.CompletedByPersonId,
                subTask.SortOrder,
                subTask.CreatedAt,
                subTask.UpdatedAt
            }, transaction: transaction, cancellationToken: cancellationToken));
        }

        transaction.Commit();
    }

    public async Task UpdateAsync(Domain.Request entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.UpdatedAt ??= DateTime.UtcNow;

        const string sql = """
            UPDATE [request].[Requests]
            SET
                [Title] = @Title,
                [Description] = @Description,
                [RequestType] = @RequestType,
                [Status] = @Status,
                [Priority] = @Priority,
                [Complexity] = @Complexity,
                [TotalSubTasksCount] = @TotalSubTasksCount,
                [CompletedSubTasksCount] = @CompletedSubTasksCount,
                [CompletionPercentage] = @CompletionPercentage,
                [OwnerPersonId] = @OwnerPersonId,
                [CustomerId] = @CustomerId,
                [ProductId] = @ProductId,
                [WorkPackageId] = @WorkPackageId,
                [EvaluationNotes] = @EvaluationNotes,
                [EscalationReason] = @EscalationReason,
                [ManagementDecisionNotes] = @ManagementDecisionNotes,
                [UpdatedAt] = @UpdatedAt
            WHERE [Id] = @Id;
            """;

        const string getExistingSubTaskIdsSql = """
            SELECT [Id]
            FROM [request].[RequestSubTasks]
            WHERE [RequestId] = @RequestId;
            """;

        const string insertSubTaskSql = """
            INSERT INTO [request].[RequestSubTasks] (
                [Id],
                [RequestId],
                [Title],
                [IsCompleted],
                [AssigneePersonId],
                [CompletedAt],
                [CompletedByPersonId],
                [SortOrder],
                [CreatedAt],
                [UpdatedAt]
            ) VALUES (
                @Id,
                @RequestId,
                @Title,
                @IsCompleted,
                @AssigneePersonId,
                @CompletedAt,
                @CompletedByPersonId,
                @SortOrder,
                @CreatedAt,
                @UpdatedAt
            );
            """;

        const string updateSubTaskSql = """
            UPDATE [request].[RequestSubTasks]
            SET
                [Title] = @Title,
                [IsCompleted] = @IsCompleted,
                [AssigneePersonId] = @AssigneePersonId,
                [CompletedAt] = @CompletedAt,
                [CompletedByPersonId] = @CompletedByPersonId,
                [SortOrder] = @SortOrder,
                [UpdatedAt] = @UpdatedAt
            WHERE [Id] = @Id;
            """;

        const string deleteSubTaskSql = """
            DELETE FROM [request].[RequestSubTasks]
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
            entity.Title,
            entity.Description,
            entity.RequestType,
            Status = entity.Status.ToName(),
            entity.Priority,
            entity.Complexity,
            entity.TotalSubTasksCount,
            entity.CompletedSubTasksCount,
            entity.CompletionPercentage,
            entity.OwnerPersonId,
            entity.CustomerId,
            entity.ProductId,
            entity.WorkPackageId,
            entity.EvaluationNotes,
            entity.EscalationReason,
            entity.ManagementDecisionNotes,
            entity.UpdatedAt
        }, transaction: transaction, cancellationToken: cancellationToken));

        var existingIds = (await connection.QueryAsync<Guid>(
            new CommandDefinition(getExistingSubTaskIdsSql, new { RequestId = entity.Id }, transaction: transaction, cancellationToken: cancellationToken)))
            .ToHashSet();

        var currentIds = entity.SubTasks.Select(st => st.Id).ToHashSet();

        // 1. Delete removed sub-tasks
        foreach (var existingId in existingIds)
        {
            if (!currentIds.Contains(existingId))
            {
                await connection.ExecuteAsync(new CommandDefinition(deleteSubTaskSql, new { Id = existingId }, transaction: transaction, cancellationToken: cancellationToken));
            }
        }

        // 2. Insert new or update existing sub-tasks
        foreach (var subTask in entity.SubTasks)
        {
            if (existingIds.Contains(subTask.Id))
            {
                await connection.ExecuteAsync(new CommandDefinition(updateSubTaskSql, new
                {
                    subTask.Id,
                    subTask.Title,
                    subTask.IsCompleted,
                    subTask.AssigneePersonId,
                    subTask.CompletedAt,
                    subTask.CompletedByPersonId,
                    subTask.SortOrder,
                    subTask.UpdatedAt
                }, transaction: transaction, cancellationToken: cancellationToken));
            }
            else
            {
                await connection.ExecuteAsync(new CommandDefinition(insertSubTaskSql, new
                {
                    subTask.Id,
                    subTask.RequestId,
                    subTask.Title,
                    subTask.IsCompleted,
                    subTask.AssigneePersonId,
                    subTask.CompletedAt,
                    subTask.CompletedByPersonId,
                    subTask.SortOrder,
                    subTask.CreatedAt,
                    subTask.UpdatedAt
                }, transaction: transaction, cancellationToken: cancellationToken));
            }
        }

        transaction.Commit();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            DELETE FROM [request].[Requests]
            WHERE [Id] = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task AddAssignmentAsync(RequestAssignment assignment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        if (assignment.Id == Guid.Empty)
        {
            assignment.Id = Guid.NewGuid();
        }

        if (assignment.CreatedAt == default)
        {
            assignment.CreatedAt = assignment.AssignedAtUtc;
        }

        const string sql = """
            INSERT INTO [request].[RequestAssignments] (
                [Id],
                [RequestId],
                [PreviousOwnerPersonId],
                [AssignedOwnerPersonId],
                [ActorPersonId],
                [PreviousStatus],
                [NewStatus],
                [AssignedAtUtc],
                [Notes],
                [CreatedAt],
                [UpdatedAt]
            ) VALUES (
                @Id,
                @RequestId,
                @PreviousOwnerPersonId,
                @AssignedOwnerPersonId,
                @ActorPersonId,
                @PreviousStatus,
                @NewStatus,
                @AssignedAtUtc,
                @Notes,
                @CreatedAt,
                @UpdatedAt
            );
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            assignment.Id,
            assignment.RequestId,
            assignment.PreviousOwnerPersonId,
            assignment.AssignedOwnerPersonId,
            assignment.ActorPersonId,
            PreviousStatus = assignment.PreviousStatus?.ToName(),
            NewStatus = assignment.NewStatus.ToName(),
            assignment.AssignedAtUtc,
            assignment.Notes,
            assignment.CreatedAt,
            assignment.UpdatedAt
        }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<RequestAssignment>> GetAssignmentsByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                [Id],
                [RequestId],
                [PreviousOwnerPersonId],
                [AssignedOwnerPersonId],
                [ActorPersonId],
                [PreviousStatus],
                [NewStatus],
                [AssignedAtUtc],
                [Notes],
                [CreatedAt],
                [UpdatedAt]
            FROM [request].[RequestAssignments]
            WHERE [RequestId] = @RequestId
            ORDER BY [AssignedAtUtc] ASC, [CreatedAt] ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<RequestAssignmentRow>(
            new CommandDefinition(sql, new { RequestId = requestId }, cancellationToken: cancellationToken));

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task AddResolutionAsync(RequestResolution resolution, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        if (resolution.Id == Guid.Empty)
        {
            resolution.Id = Guid.NewGuid();
        }

        if (resolution.CreatedAt == default)
        {
            resolution.CreatedAt = resolution.ResolvedAt;
        }

        const string sql = """
            INSERT INTO [request].[RequestResolutions] (
                [Id],
                [RequestId],
                [Outcome],
                [Description],
                [ResolvedBy],
                [ResolvedAt],
                [CreatedAt],
                [UpdatedAt]
            ) VALUES (
                @Id,
                @RequestId,
                @Outcome,
                @Description,
                @ResolvedBy,
                @ResolvedAt,
                @CreatedAt,
                @UpdatedAt
            );
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            resolution.Id,
            resolution.RequestId,
            resolution.Outcome,
            resolution.Description,
            resolution.ResolvedBy,
            resolution.ResolvedAt,
            resolution.CreatedAt,
            resolution.UpdatedAt
        }, cancellationToken: cancellationToken));
    }

    public async Task<RequestResolution?> GetResolutionByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                [Id],
                [RequestId],
                [Outcome],
                [Description],
                [ResolvedBy],
                [ResolvedAt],
                [CreatedAt],
                [UpdatedAt]
            FROM [request].[RequestResolutions]
            WHERE [RequestId] = @RequestId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<RequestResolutionRow>(
            new CommandDefinition(sql, new { RequestId = requestId }, cancellationToken: cancellationToken));

        return row?.ToDomain();
    }

    private sealed class RequestRow
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string RequestType { get; init; } = string.Empty;
        public string Status { get; init; } = RequestStatusNames.Captured;
        public string Priority { get; init; } = "NORMAL";
        public int Complexity { get; init; } = 1;
        public int TotalSubTasksCount { get; init; } = 0;
        public int CompletedSubTasksCount { get; init; } = 0;
        public int CompletionPercentage { get; init; } = 0;
        public Guid? OwnerPersonId { get; init; }
        public Guid? CustomerId { get; init; }
        public Guid? ProductId { get; init; }
        public Guid? WorkPackageId { get; init; }
        public string? EvaluationNotes { get; init; }
        public string? EscalationReason { get; init; }
        public string? ManagementDecisionNotes { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }

        public Domain.Request ToDomain(
            RequestResolution? resolution = null,
            IEnumerable<RequestAssignment>? assignments = null,
            IEnumerable<RequestSubTask>? subTasks = null)
        {
            return Domain.Request.Rehydrate(
                Id,
                Title,
                Description,
                RequestType,
                RequestStatusNames.FromName(Status),
                Priority,
                OwnerPersonId,
                CustomerId,
                ProductId,
                WorkPackageId,
                EvaluationNotes,
                EscalationReason,
                ManagementDecisionNotes,
                CreatedAt,
                UpdatedAt,
                resolution,
                assignments,
                Complexity,
                subTasks,
                TotalSubTasksCount,
                CompletedSubTasksCount,
                CompletionPercentage);
        }
    }

    private sealed class RequestSubTaskRow
    {
        public Guid Id { get; init; }
        public Guid RequestId { get; init; }
        public string Title { get; init; } = string.Empty;
        public bool IsCompleted { get; init; }
        public Guid? AssigneePersonId { get; init; }
        public DateTime? CompletedAt { get; init; }
        public Guid? CompletedByPersonId { get; init; }
        public int SortOrder { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }

        public RequestSubTask ToDomain() =>
            RequestSubTask.Rehydrate(
                Id,
                RequestId,
                Title,
                IsCompleted,
                AssigneePersonId,
                CompletedAt,
                CompletedByPersonId,
                SortOrder,
                CreatedAt,
                UpdatedAt);
    }

    private sealed class RequestResolutionRow
    {
        public Guid Id { get; init; }
        public Guid RequestId { get; init; }
        public string Outcome { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public Guid ResolvedBy { get; init; }
        public DateTime ResolvedAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }

        public RequestResolution ToDomain() =>
            RequestResolution.Rehydrate(
                Id,
                RequestId,
                Outcome,
                Description,
                ResolvedBy,
                ResolvedAt,
                CreatedAt,
                UpdatedAt);
    }

    private sealed class RequestAssignmentRow
    {
        public Guid Id { get; init; }
        public Guid RequestId { get; init; }
        public Guid? PreviousOwnerPersonId { get; init; }
        public Guid? AssignedOwnerPersonId { get; init; }
        public Guid ActorPersonId { get; init; }
        public string? PreviousStatus { get; init; }
        public string NewStatus { get; init; } = RequestStatusNames.Captured;
        public DateTime AssignedAtUtc { get; init; }
        public string? Notes { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }

        public RequestAssignment ToDomain() =>
            RequestAssignment.Rehydrate(
                Id,
                RequestId,
                PreviousOwnerPersonId,
                AssignedOwnerPersonId,
                ActorPersonId,
                RequestStatusNames.FromNullableName(PreviousStatus),
                RequestStatusNames.FromName(NewStatus),
                AssignedAtUtc,
                Notes,
                CreatedAt,
                UpdatedAt);
    }
}
