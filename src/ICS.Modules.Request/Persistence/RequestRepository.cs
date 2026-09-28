namespace ICS.Modules.Request.Persistence;

using System.Data;
using System.Text;
using Dapper;
using ICS.Core.Data;
using ICS.Modules.Request.Application.DTOs;
using ICS.Modules.Request.Domain;

/// <summary>
/// Dapper-based repository implementation for Request aggregate and associated entities.
/// Uses explicit parameterized SQL against request.* schema tables.
/// Architecture §16, §17, §19.3, §20.
/// </summary>
internal class RequestRepository : IRequestRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RequestRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Request?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                RequestId,
                Title,
                Description,
                Type,
                Priority,
                Status,
                RequesterPersonId,
                RequesterContactId,
                RequesterName,
                OwnerPersonId,
                CustomerId,
                ProductId,
                WorkPackageId,
                IsAwaitingManagementDecision,
                ManagementDecisionQuestion,
                ManagementDecisionOptions,
                ManagementDecisionImpact,
                ManagementDecisionRequestedAt,
                EscalationReason,
                EscalatedByPersonId,
                EscalatedAt,
                CreatedAt,
                UpdatedAt,
                ClosedAt
            FROM [request].[Requests]
            WHERE RequestId = @RequestId;

            SELECT 
                ResolutionId AS Id,
                RequestId,
                Outcome,
                Summary,
                ResolvedByPersonId,
                ResolvedAt,
                CreatedAt,
                UpdatedAt
            FROM [request].[RequestResolutions]
            WHERE RequestId = @RequestId;

            SELECT 
                AssignmentId AS Id,
                RequestId,
                OwnerPersonId,
                AssignedByPersonId,
                AssignedAt,
                Note,
                IsActive,
                CreatedAt,
                UpdatedAt
            FROM [request].[RequestAssignments]
            WHERE RequestId = @RequestId
            ORDER BY AssignedAt ASC;";

        using var multi = await connection.QueryMultipleAsync(sql, new { RequestId = requestId });

        var requestRow = await multi.ReadSingleOrDefaultAsync<RequestRow>();
        if (requestRow == null)
        {
            return null;
        }

        var resolutionRow = await multi.ReadSingleOrDefaultAsync<RequestResolutionRow>();
        var assignmentRows = (await multi.ReadAsync<RequestAssignmentRow>()).ToList();

        RequestResolution? resolution = null;
        if (resolutionRow != null)
        {
            resolution = new RequestResolution(
                resolutionRow.Id,
                resolutionRow.RequestId,
                resolutionRow.Outcome,
                resolutionRow.Summary,
                resolutionRow.ResolvedByPersonId,
                resolutionRow.ResolvedAt);
        }

        var assignments = assignmentRows.Select(a => new RequestAssignment(
            a.Id,
            a.RequestId,
            a.OwnerPersonId,
            a.AssignedByPersonId,
            a.AssignedAt,
            a.Note,
            a.IsActive)).ToList();

        return MapToAggregate(requestRow, resolution, assignments);
    }

    public async Task AddAsync(Request request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string insertRequestSql = @"
            INSERT INTO [request].[Requests] (
                RequestId,
                Title,
                Description,
                Type,
                Priority,
                Status,
                RequesterPersonId,
                RequesterContactId,
                RequesterName,
                OwnerPersonId,
                CustomerId,
                ProductId,
                WorkPackageId,
                IsAwaitingManagementDecision,
                ManagementDecisionQuestion,
                ManagementDecisionOptions,
                ManagementDecisionImpact,
                ManagementDecisionRequestedAt,
                EscalationReason,
                EscalatedByPersonId,
                EscalatedAt,
                CreatedAt,
                UpdatedAt,
                ClosedAt
            ) VALUES (
                @RequestId,
                @Title,
                @Description,
                @Type,
                @Priority,
                @Status,
                @RequesterPersonId,
                @RequesterContactId,
                @RequesterName,
                @OwnerPersonId,
                @CustomerId,
                @ProductId,
                @WorkPackageId,
                @IsAwaitingManagementDecision,
                @ManagementDecisionQuestion,
                @ManagementDecisionOptions,
                @ManagementDecisionImpact,
                @ManagementDecisionRequestedAt,
                @EscalationReason,
                @EscalatedByPersonId,
                @EscalatedAt,
                @CreatedAt,
                @UpdatedAt,
                @ClosedAt
            );";

        await connection.ExecuteAsync(insertRequestSql, new
        {
            RequestId = request.Id,
            request.Title,
            request.Description,
            request.Type,
            request.Priority,
            request.Status,
            request.RequesterPersonId,
            request.RequesterContactId,
            request.RequesterName,
            request.OwnerPersonId,
            request.CustomerId,
            request.ProductId,
            request.WorkPackageId,
            request.IsAwaitingManagementDecision,
            request.ManagementDecisionQuestion,
            request.ManagementDecisionOptions,
            request.ManagementDecisionImpact,
            request.ManagementDecisionRequestedAt,
            request.EscalationReason,
            request.EscalatedByPersonId,
            request.EscalatedAt,
            request.CreatedAt,
            request.UpdatedAt,
            request.ClosedAt
        }, transaction);

        if (request.Assignments.Count > 0)
        {
            const string insertAssignmentSql = @"
                INSERT INTO [request].[RequestAssignments] (
                    AssignmentId,
                    RequestId,
                    OwnerPersonId,
                    AssignedByPersonId,
                    AssignedAt,
                    Note,
                    IsActive,
                    CreatedAt,
                    UpdatedAt
                ) VALUES (
                    @AssignmentId,
                    @RequestId,
                    @OwnerPersonId,
                    @AssignedByPersonId,
                    @AssignedAt,
                    @Note,
                    @IsActive,
                    @CreatedAt,
                    @UpdatedAt
                );";

            foreach (var assignment in request.Assignments)
            {
                await connection.ExecuteAsync(insertAssignmentSql, new
                {
                    AssignmentId = assignment.Id,
                    assignment.RequestId,
                    assignment.OwnerPersonId,
                    assignment.AssignedByPersonId,
                    assignment.AssignedAt,
                    assignment.Note,
                    assignment.IsActive,
                    assignment.CreatedAt,
                    assignment.UpdatedAt
                }, transaction);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateAsync(Request request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string updateRequestSql = @"
            UPDATE [request].[Requests]
            SET 
                Title = @Title,
                Description = @Description,
                Type = @Type,
                Priority = @Priority,
                Status = @Status,
                RequesterPersonId = @RequesterPersonId,
                RequesterContactId = @RequesterContactId,
                RequesterName = @RequesterName,
                OwnerPersonId = @OwnerPersonId,
                CustomerId = @CustomerId,
                ProductId = @ProductId,
                WorkPackageId = @WorkPackageId,
                IsAwaitingManagementDecision = @IsAwaitingManagementDecision,
                ManagementDecisionQuestion = @ManagementDecisionQuestion,
                ManagementDecisionOptions = @ManagementDecisionOptions,
                ManagementDecisionImpact = @ManagementDecisionImpact,
                ManagementDecisionRequestedAt = @ManagementDecisionRequestedAt,
                EscalationReason = @EscalationReason,
                EscalatedByPersonId = @EscalatedByPersonId,
                EscalatedAt = @EscalatedAt,
                UpdatedAt = @UpdatedAt,
                ClosedAt = @ClosedAt
            WHERE RequestId = @RequestId;";

        await connection.ExecuteAsync(updateRequestSql, new
        {
            RequestId = request.Id,
            request.Title,
            request.Description,
            request.Type,
            request.Priority,
            request.Status,
            request.RequesterPersonId,
            request.RequesterContactId,
            request.RequesterName,
            request.OwnerPersonId,
            request.CustomerId,
            request.ProductId,
            request.WorkPackageId,
            request.IsAwaitingManagementDecision,
            request.ManagementDecisionQuestion,
            request.ManagementDecisionOptions,
            request.ManagementDecisionImpact,
            request.ManagementDecisionRequestedAt,
            request.EscalationReason,
            request.EscalatedByPersonId,
            request.EscalatedAt,
            request.UpdatedAt,
            request.ClosedAt
        }, transaction);

        if (request.Assignments.Count > 0)
        {
            const string upsertAssignmentSql = @"
                IF EXISTS (SELECT 1 FROM [request].[RequestAssignments] WHERE AssignmentId = @AssignmentId)
                BEGIN
                    UPDATE [request].[RequestAssignments]
                    SET IsActive = @IsActive,
                        UpdatedAt = @UpdatedAt
                    WHERE AssignmentId = @AssignmentId;
                END
                ELSE
                BEGIN
                    INSERT INTO [request].[RequestAssignments] (
                        AssignmentId,
                        RequestId,
                        OwnerPersonId,
                        AssignedByPersonId,
                        AssignedAt,
                        Note,
                        IsActive,
                        CreatedAt,
                        UpdatedAt
                    ) VALUES (
                        @AssignmentId,
                        @RequestId,
                        @OwnerPersonId,
                        @AssignedByPersonId,
                        @AssignedAt,
                        @Note,
                        @IsActive,
                        @CreatedAt,
                        @UpdatedAt
                    );
                END";

            foreach (var assignment in request.Assignments)
            {
                await connection.ExecuteAsync(upsertAssignmentSql, new
                {
                    AssignmentId = assignment.Id,
                    assignment.RequestId,
                    assignment.OwnerPersonId,
                    assignment.AssignedByPersonId,
                    assignment.AssignedAt,
                    assignment.Note,
                    assignment.IsActive,
                    assignment.CreatedAt,
                    assignment.UpdatedAt
                }, transaction);
            }
        }

        if (request.Resolution != null)
        {
            const string upsertResolutionSql = @"
                IF EXISTS (SELECT 1 FROM [request].[RequestResolutions] WHERE ResolutionId = @ResolutionId)
                BEGIN
                    UPDATE [request].[RequestResolutions]
                    SET Outcome = @Outcome,
                        Summary = @Summary,
                        ResolvedByPersonId = @ResolvedByPersonId,
                        ResolvedAt = @ResolvedAt,
                        UpdatedAt = @UpdatedAt
                    WHERE ResolutionId = @ResolutionId;
                END
                ELSE
                BEGIN
                    INSERT INTO [request].[RequestResolutions] (
                        ResolutionId,
                        RequestId,
                        Outcome,
                        Summary,
                        ResolvedByPersonId,
                        ResolvedAt,
                        CreatedAt,
                        UpdatedAt
                    ) VALUES (
                        @ResolutionId,
                        @RequestId,
                        @Outcome,
                        @Summary,
                        @ResolvedByPersonId,
                        @ResolvedAt,
                        @CreatedAt,
                        @UpdatedAt
                    );
                END";

            await connection.ExecuteAsync(upsertResolutionSql, new
            {
                ResolutionId = request.Resolution.Id,
                request.Resolution.RequestId,
                request.Resolution.Outcome,
                request.Resolution.Summary,
                request.Resolution.ResolvedByPersonId,
                request.Resolution.ResolvedAt,
                request.Resolution.CreatedAt,
                request.Resolution.UpdatedAt
            }, transaction);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AddStateHistoryAsync(
        Guid requestId,
        string? fromStatus,
        string toStatus,
        Guid actorPersonId,
        string? reason,
        DateTime changedAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO [request].[RequestStateHistories] (
                StateHistoryId,
                RequestId,
                FromStatus,
                ToStatus,
                ActorPersonId,
                Reason,
                ChangedAt
            ) VALUES (
                @StateHistoryId,
                @RequestId,
                @FromStatus,
                @ToStatus,
                @ActorPersonId,
                @Reason,
                @ChangedAt
            );";

        await connection.ExecuteAsync(sql, new
        {
            StateHistoryId = Guid.NewGuid(),
            RequestId = requestId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ActorPersonId = actorPersonId,
            Reason = reason,
            ChangedAt = changedAt
        });
    }

    public async Task<IReadOnlyList<RequestStateHistoryDto>> GetStateHistoryAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                StateHistoryId,
                RequestId,
                FromStatus,
                ToStatus,
                ActorPersonId,
                Reason,
                ChangedAt
            FROM [request].[RequestStateHistories]
            WHERE RequestId = @RequestId
            ORDER BY ChangedAt ASC;";

        var rows = await connection.QueryAsync<RequestStateHistoryRow>(sql, new { RequestId = requestId });
        return rows.Select(r => new RequestStateHistoryDto(
            r.StateHistoryId,
            r.RequestId,
            r.FromStatus,
            r.ToStatus,
            r.ActorPersonId,
            null,
            r.Reason,
            r.ChangedAt)).ToList();
    }

    public async Task<IReadOnlyList<Request>> ListMyAssignedAsync(Guid personId, bool includeClosed = false, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var sql = new StringBuilder(@"
            SELECT 
                RequestId,
                Title,
                Description,
                Type,
                Priority,
                Status,
                RequesterPersonId,
                RequesterContactId,
                RequesterName,
                OwnerPersonId,
                CustomerId,
                ProductId,
                WorkPackageId,
                IsAwaitingManagementDecision,
                ManagementDecisionQuestion,
                ManagementDecisionOptions,
                ManagementDecisionImpact,
                ManagementDecisionRequestedAt,
                EscalationReason,
                EscalatedByPersonId,
                EscalatedAt,
                CreatedAt,
                UpdatedAt,
                ClosedAt
            FROM [request].[Requests]
            WHERE OwnerPersonId = @PersonId");

        if (!includeClosed)
        {
            sql.Append(" AND Status NOT IN ('COMPLETED', 'REJECTED')");
        }

        sql.Append(" ORDER BY CreatedAt DESC;");

        var rows = await connection.QueryAsync<RequestRow>(sql.ToString(), new { PersonId = personId });
        return rows.Select(r => MapToAggregate(r, null, null)).ToList();
    }

    public async Task<(int TotalCount, IReadOnlyList<Request> Items)> GetFilteredGridAsync(RequestGridFilterDto filter, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var whereClause = new StringBuilder("WHERE 1=1");
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            whereClause.Append(" AND (Title LIKE @SearchPattern OR Description LIKE @SearchPattern)");
            parameters.Add("SearchPattern", $"%{filter.SearchTerm.Trim()}%");
        }

        if (filter.CustomerId.HasValue && filter.CustomerId.Value != Guid.Empty)
        {
            whereClause.Append(" AND CustomerId = @CustomerId");
            parameters.Add("CustomerId", filter.CustomerId.Value);
        }

        if (filter.ProductId.HasValue && filter.ProductId.Value != Guid.Empty)
        {
            whereClause.Append(" AND ProductId = @ProductId");
            parameters.Add("ProductId", filter.ProductId.Value);
        }

        if (filter.OwnerPersonId.HasValue && filter.OwnerPersonId.Value != Guid.Empty)
        {
            whereClause.Append(" AND OwnerPersonId = @OwnerPersonId");
            parameters.Add("OwnerPersonId", filter.OwnerPersonId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Priority))
        {
            whereClause.Append(" AND Priority = @Priority");
            parameters.Add("Priority", filter.Priority.Trim().ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(filter.Statuses))
        {
            var statusList = filter.Statuses.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToUpperInvariant())
                .ToList();

            if (statusList.Count > 0)
            {
                whereClause.Append(" AND Status IN @Statuses");
                parameters.Add("Statuses", statusList);
            }
        }

        if (filter.FromDate.HasValue)
        {
            whereClause.Append(" AND CreatedAt >= @FromDate");
            parameters.Add("FromDate", filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            whereClause.Append(" AND CreatedAt <= @ToDate");
            parameters.Add("ToDate", filter.ToDate.Value);
        }

        var countSql = $"SELECT COUNT(*) FROM [request].[Requests] {whereClause};";
        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

        var pageSql = $@"
            SELECT 
                RequestId,
                Title,
                Description,
                Type,
                Priority,
                Status,
                RequesterPersonId,
                RequesterContactId,
                RequesterName,
                OwnerPersonId,
                CustomerId,
                ProductId,
                WorkPackageId,
                IsAwaitingManagementDecision,
                ManagementDecisionQuestion,
                ManagementDecisionOptions,
                ManagementDecisionImpact,
                ManagementDecisionRequestedAt,
                EscalationReason,
                EscalatedByPersonId,
                EscalatedAt,
                CreatedAt,
                UpdatedAt,
                ClosedAt
            FROM [request].[Requests]
            {whereClause}
            ORDER BY CreatedAt DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;";

        parameters.Add("Skip", Math.Max(0, filter.Skip));
        parameters.Add("Take", filter.Take <= 0 ? 50 : filter.Take);

        var rows = await connection.QueryAsync<RequestRow>(pageSql, parameters);
        var items = rows.Select(r => MapToAggregate(r, null, null)).ToList();

        return (totalCount, items);
    }

    private static Request MapToAggregate(RequestRow row, RequestResolution? resolution, IEnumerable<RequestAssignment>? assignments)
    {
        return new Request(
            row.RequestId,
            row.Title,
            row.Description,
            row.Type,
            row.Priority,
            row.Status,
            row.RequesterPersonId,
            row.RequesterContactId,
            row.RequesterName,
            row.OwnerPersonId,
            row.CustomerId,
            row.ProductId,
            row.WorkPackageId,
            row.IsAwaitingManagementDecision,
            row.ManagementDecisionQuestion,
            row.ManagementDecisionOptions,
            row.ManagementDecisionImpact,
            row.ManagementDecisionRequestedAt,
            row.EscalationReason,
            row.EscalatedByPersonId,
            row.EscalatedAt,
            row.CreatedAt,
            row.UpdatedAt,
            row.ClosedAt,
            resolution,
            assignments);
    }

    private sealed class RequestRow
    {
        public Guid RequestId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public Guid? RequesterPersonId { get; set; }
        public Guid? RequesterContactId { get; set; }
        public string? RequesterName { get; set; }
        public Guid? OwnerPersonId { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? ProductId { get; set; }
        public Guid? WorkPackageId { get; set; }
        public bool IsAwaitingManagementDecision { get; set; }
        public string? ManagementDecisionQuestion { get; set; }
        public string? ManagementDecisionOptions { get; set; }
        public string? ManagementDecisionImpact { get; set; }
        public DateTime? ManagementDecisionRequestedAt { get; set; }
        public string? EscalationReason { get; set; }
        public Guid? EscalatedByPersonId { get; set; }
        public DateTime? EscalatedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
    }

    private sealed class RequestResolutionRow
    {
        public Guid Id { get; set; }
        public Guid RequestId { get; set; }
        public string Outcome { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public Guid ResolvedByPersonId { get; set; }
        public DateTime ResolvedAt { get; set; }
    }

    private sealed class RequestAssignmentRow
    {
        public Guid Id { get; set; }
        public Guid RequestId { get; set; }
        public Guid OwnerPersonId { get; set; }
        public Guid AssignedByPersonId { get; set; }
        public DateTime AssignedAt { get; set; }
        public string? Note { get; set; }
        public bool IsActive { get; set; }
    }

    private sealed class RequestStateHistoryRow
    {
        public Guid StateHistoryId { get; set; }
        public Guid RequestId { get; set; }
        public string? FromStatus { get; set; }
        public string ToStatus { get; set; } = string.Empty;
        public Guid ActorPersonId { get; set; }
        public string? Reason { get; set; }
        public DateTime ChangedAt { get; set; }
    }
}
