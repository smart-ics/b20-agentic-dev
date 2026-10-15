---
Title: Request Blocker Capability and History Tracking Architecture
Code: CR-032
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-15
---

# 1. Overview

This architecture defines the technical realization for Change Request `CR-032`: Add Blocker Capability and History Tracking to Requests.

It consumes and realizes the approved feasibility decisions from [CR-032-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-032-FEASIBILITY-ASSESSMENT.md), establishing:

1. **Domain Model & Aggregate Root Boundary**:
   - `RequestBlocker` child entity within `Cakra.Modules.Request.Domain`.
   - Authoritative aggregate invariant enforcement in `Request.cs`: recording a blocker transitions an active request to `PAUSED`; completing (`Complete`) or resuming work (`StartWork`) is strictly rejected while any active blocker exists; clearing all blockers leaves the request in `PAUSED` until the owner manually resumes.
   - Preserving open blockers as historical records when a request is cancelled (`Cancel`).
2. **Persistence & Relational Database Design**:
   - Dedicated table `[request].[RequestBlockers]` with referential integrity (`ON DELETE CASCADE` to `[request].[Requests]`).
   - Summary column `[ActiveBlockersCount]` on `[request].[Requests]` with non-negative check constraint.
   - Migration script `0021_request_blockers.sql` in `Cakra.Api/Migrations/Scripts`.
   - Dapper repository hydration and transactional persistence in `RequestRepository.cs`.
3. **Application Service & REST API Contracts**:
   - Commands `AddRequestBlockerCommand` and `ClearRequestBlockerCommand` with FluentValidation.
   - REST endpoints `POST /api/v1/requests/{id}/blockers` and `POST /api/v1/requests/{id}/blockers/{blockerId}/clear` in `RequestsController.cs`.
   - Role and ownership authorization: any authenticated collaborator may add a blocker; clearing is restricted to the assigned Request Owner, Administrator, or Manager.
4. **Domain Events & Projections**:
   - `RequestBlockerAdded` and `RequestBlockerCleared` emitted by the aggregate root.
   - DTO projection `RequestBlockerDto` and enrichment of `RequestDto` with `ActiveBlockersCount`, `HasActiveBlockers`, and `Blockers` collection.
5. **Frontend User Experience & Screens**:
   - `src/frontend/Cakra.Web/src/api/requests.ts`: Typed TypeScript contracts for `RequestBlocker`, `AddBlockerPayload`, `ClearBlockerPayload`, and API functions.
   - `SCR-REQ-003: Request Detail` (`RequestDetailView.vue`): Dedicated **Blockers** card rendering active blockers, "Add Blocker" modal, "Clear Blocker" modal with clearance note, and historical cleared blockers log. Disables Resume Work and Complete actions while active blockers exist.
   - `SCR-REQ-004: My Requests` (`MyRequestsView.vue`) & `SCR-WP-001: Work Package Scope` (`WorkPackageView.vue`): Renders visual `[Blocked]` badges and active blocker descriptions on cards and list rows.

---

# 2. Architectural Basis

## Business Context

- ISSUE: [CR-032-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-032-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md) (v1.3, Business Rules 22–28)
- FEATURE: [FEAT-REQ-009-manage-request-blockers.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-009-manage-request-blockers.md)
- FEATURE: [FEAT-REQ-008-review-request-completion.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-008-review-request-completion.md)
- REFERENCE ARCHITECTURE: [CR-016-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-016-ARCHITECTURE.md) (Lifecycle Simplification)
- REFERENCE ARCHITECTURE: [CR-025-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-025-ARCHITECTURE.md) (Work Package Telemetry & Health Invariants)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-032-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-032-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

All feasibility decisions are realized herein:
- `GAP-001`, `OQ-003`: `RequestBlocker` child entity with immutable audit trail.
- `GAP-002`, `GAP-003`, `OQ-001`, `OQ-002`: Invariants in `Complete`, `StartWork`, `PauseWork`.
- `GAP-004`, `GAP-005`: Schema migration `0021_request_blockers.sql`, Dapper hydration, REST endpoints.
- `GAP-006`, `GAP-007`, `GAP-008`, `OQ-006`, `OQ-007`: DTOs and UI cards across `SCR-REQ-003`, `SCR-REQ-004`, `SCR-WP-001`.
- `OQ-004`, `OQ-005`, `OQ-008`, `OQ-009`: Security policies, state eligibility, immutability, and cancellation tolerance.

---

# 3. Scope

## Included

1. **Domain Model (`Cakra.Modules.Request.Domain`)**:
   - `RequestBlocker.cs`: Entity with `Id`, `RequestId`, `Reason`, `CreatedByPersonId`, `CreatedAtUtc`, `IsCleared`, `ClearedAtUtc`, `ClearedByPersonId`, `ClearanceReason`.
   - `RequestHasActiveBlockersException.cs`: Domain validation exception thrown when attempting `Complete` or `StartWork` with active blockers.
   - Domain events: `RequestBlockerAdded.cs` and `RequestBlockerCleared.cs`.
   - `Request.cs`: Internal `_blockers` collection, public `IReadOnlyCollection<RequestBlocker> Blockers`, `ActiveBlockersCount` property, `AddBlocker`, `ClearBlocker`, updated `StartWork`, `Complete`, `Cancel`, and `Rehydrate`.
2. **Database Schema & Migrations (`Cakra.Api/Migrations`)**:
   - Migration script `0021_request_blockers.sql` creating table `[request].[RequestBlockers]`, adding `[ActiveBlockersCount]` to `[request].[Requests]`, and backfilling count.
3. **Repository Persistence (`Cakra.Modules.Request.Persistence`)**:
   - `IRequestRepository.cs` & `RequestRepository.cs`: Select from `[request].[RequestBlockers]` in `GetByIdAsync`, transactional insert of new blockers, and update of cleared blockers.
4. **Application Services & Commands (`Cakra.Modules.Request.Services`)**:
   - `AddRequestBlockerCommand` and `ClearRequestBlockerCommand` with validation.
   - `IRequestService.cs` & `RequestService.cs`: `AddBlockerAsync` and `ClearBlockerAsync` methods with authorization logic.
5. **REST API Controllers (`Cakra.Api.Controllers`)**:
   - `RequestsController.cs`: `POST /api/v1/requests/{id}/blockers` and `POST /api/v1/requests/{id}/blockers/{blockerId}/clear`.
6. **DTO Projections (`Cakra.Modules.Request.Models`)**:
   - `RequestBlockerDto.cs`.
   - Update `RequestDto.cs` with `ActiveBlockersCount`, `HasActiveBlockers`, and `IReadOnlyList<RequestBlockerDto> Blockers`.
7. **Frontend Web (`Cakra.Web`)**:
   - `src/api/requests.ts`: Typed contracts and API methods `addBlocker`, `clearBlocker`.
   - `SCR-REQ-003` (`RequestDetailView.vue`): Dedicated Blockers card, Add/Clear modals, cleared history, button guard disables.
   - `SCR-REQ-004` (`MyRequestsView.vue`): Blocker badge and note on cards.
   - `SCR-WP-001` (`WorkPackageView.vue`): Blocker badge and note on scope list.
8. **Automated Testing**:
   - Backend unit tests for `Request.AddBlocker`, `Request.ClearBlocker`, `StartWork` invariant, `Complete` invariant, and service authorization.
   - Frontend unit tests for blockers card rendering, adding a blocker, clearing a blocker, and invariant button disabling.

## Excluded

- Deleting or updating existing blocker reasons (forbidden by immutability invariant).
- Automatic resumption of work upon clearing the final blocker (strictly forbidden by domain rule 27).
- Blocking cancellation when open blockers exist (cancellation is explicitly permitted by domain rule 28).

---

# 4. Technical Decisions

## TD-001: Domain Entity `RequestBlocker` and Aggregate Encapsulation

`RequestBlocker` is defined within `Cakra.Modules.Request.Domain` as a sealed entity:

```csharp
namespace Cakra.Modules.Request.Domain;

public sealed class RequestBlocker : EntityBase
{
    public Guid RequestId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid CreatedByPersonId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public bool IsCleared { get; private set; }
    public DateTime? ClearedAtUtc { get; private set; }
    public Guid? ClearedByPersonId { get; private set; }
    public string? ClearanceReason { get; private set; }

    private RequestBlocker() { }

    internal static RequestBlocker Create(
        Guid id,
        Guid requestId,
        string reason,
        Guid createdByPersonId,
        DateTime utcNow)
    {
        if (id == Guid.Empty) throw new RequestDomainValidationException("BlockerId cannot be empty.", nameof(id));
        if (requestId == Guid.Empty) throw new RequestDomainValidationException("RequestId cannot be empty.", nameof(requestId));
        if (string.IsNullOrWhiteSpace(reason)) throw new RequestDomainValidationException("Blocker reason cannot be empty.", nameof(reason));
        if (createdByPersonId == Guid.Empty) throw new RequestDomainValidationException("CreatedByPersonId cannot be empty.", nameof(createdByPersonId));

        return new RequestBlocker
        {
            Id = id,
            RequestId = requestId,
            Reason = reason.Trim(),
            CreatedByPersonId = createdByPersonId,
            CreatedAtUtc = utcNow,
            IsCleared = false,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    internal void Clear(Guid clearedByPersonId, string? clearanceReason, DateTime utcNow)
    {
        if (clearedByPersonId == Guid.Empty) throw new RequestDomainValidationException("ClearedByPersonId cannot be empty.", nameof(clearedByPersonId));
        if (IsCleared) throw new InvalidOperationException("Blocker is already cleared.");

        IsCleared = true;
        ClearedAtUtc = utcNow;
        ClearedByPersonId = clearedByPersonId;
        ClearanceReason = string.IsNullOrWhiteSpace(clearanceReason) ? null : clearanceReason.Trim();
        UpdatedAt = utcNow;
    }
}
```

## TD-002: Aggregate Invariants & State Transitions in `Request.cs`

1. **Adding a Blocker (`AddBlocker`)**:
   - Allowed states: `ASSIGNED`, `IN_PROGRESS`, `PAUSED`.
   - Closed states (`COMPLETED`, `CANCELLED`): Throws `InvalidRequestStateTransitionException`.
   - If current state is `IN_PROGRESS`: Automatically transitions `IN_PROGRESS -> PAUSED`, appends a `RequestAssignment` ("Work paused due to blocker"), and emits `RequestWorkPaused`.
   - If current state is `ASSIGNED`: Transitions `ASSIGNED -> PAUSED`, appends a `RequestAssignment` ("Assigned request paused due to blocker").
   - Increments `ActiveBlockersCount`.
   - Emits `RequestBlockerAdded`.

2. **Clearing a Blocker (`ClearBlocker`)**:
   - Locates blocker by `blockerId`; throws `RequestDomainValidationException` if not found or already cleared.
   - Clears the blocker with `actorPersonId`, `clearanceReason`, and timestamp.
   - Decrements `ActiveBlockersCount`.
   - Leaves the Request in its current status (`PAUSED`); does **not** auto-resume.
   - Emits `RequestBlockerCleared`.

3. **Resuming Work Guard (`StartWork`)**:
   - Evaluates:
     ```csharp
     if (_blockers.Any(b => !b.IsCleared))
     {
         var count = _blockers.Count(b => !b.IsCleared);
         throw new RequestHasActiveBlockersException(
             Id, count,
             $"Cannot start or resume work on request '{Id}' because it has {count} active blocker(s). All blockers must be cleared before work can resume.");
     }
     ```

4. **Completion Guard (`Complete`)**:
   - Evaluates:
     ```csharp
     if (_blockers.Any(b => !b.IsCleared))
     {
         var count = _blockers.Count(b => !b.IsCleared);
         throw new RequestHasActiveBlockersException(
             Id, count,
             $"Cannot complete request '{Id}' because it has {count} active blocker(s). All blockers must be cleared before completion.");
     }
     ```

5. **Cancellation Permitted (`Cancel`)**:
   - Permitted regardless of active blockers; any open blockers remain open in history for audit accuracy.

## TD-003: Domain Exception & Domain Events

1. **Exception**: `RequestHasActiveBlockersException : RequestDomainException`
   - Properties: `Guid RequestId`, `int ActiveBlockersCount`.
   - HTTP status code mapping: `409 Conflict` or `400 Bad Request` via existing global exception filters.

2. **Domain Events**:
   - `RequestBlockerAdded`: `(Guid RequestId, Guid BlockerId, string Reason, Guid CreatedByPersonId, DateTime OccurredAtUtc)`
   - `RequestBlockerCleared`: `(Guid RequestId, Guid BlockerId, string? ClearanceReason, Guid ClearedByPersonId, DateTime OccurredAtUtc)`

## TD-004: Relational Persistence & Migration `0021_request_blockers.sql`

```sql
IF OBJECT_ID(N'[request].[RequestBlockers]', N'U') IS NULL
BEGIN
    CREATE TABLE [request].[RequestBlockers] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_RequestBlockers] PRIMARY KEY CLUSTERED DEFAULT NEWID(),
        [RequestId] UNIQUEIDENTIFIER NOT NULL,
        [Reason] NVARCHAR(1000) NOT NULL,
        [CreatedByPersonId] UNIQUEIDENTIFIER NOT NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_RequestBlockers_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
        [IsCleared] BIT NOT NULL CONSTRAINT [DF_RequestBlockers_IsCleared] DEFAULT 0,
        [ClearedAtUtc] DATETIME2 NULL,
        [ClearedByPersonId] UNIQUEIDENTIFIER NULL,
        [ClearanceReason] NVARCHAR(1000) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_RequestBlockers_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_RequestBlockers_UpdatedAt] DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [FK_RequestBlockers_Requests] FOREIGN KEY ([RequestId])
            REFERENCES [request].[Requests]([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_RequestBlockers_RequestId]
        ON [request].[RequestBlockers] ([RequestId]);

    CREATE NONCLUSTERED INDEX [IX_RequestBlockers_Active]
        ON [request].[RequestBlockers] ([RequestId], [IsCleared])
        INCLUDE ([Reason], [CreatedAtUtc]);
END;

IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[request].[Requests]'))
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[request].[Requests]') AND name = N'ActiveBlockersCount')
    BEGIN
        EXEC (N'ALTER TABLE [request].[Requests] ADD [ActiveBlockersCount] INT NOT NULL CONSTRAINT [DF_Requests_ActiveBlockersCount] DEFAULT 0;');
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Requests_ActiveBlockersCount' AND parent_object_id = OBJECT_ID(N'[request].[Requests]'))
    BEGIN
        EXEC (N'ALTER TABLE [request].[Requests] ADD CONSTRAINT [CK_Requests_ActiveBlockersCount] CHECK ([ActiveBlockersCount] >= 0);');
    END;

    -- Backfill ActiveBlockersCount from existing blockers table
    EXEC (N'UPDATE r
           SET r.[ActiveBlockersCount] = ISNULL(b.[Cnt], 0)
           FROM [request].[Requests] r
           LEFT JOIN (
               SELECT [RequestId], COUNT(*) AS [Cnt]
               FROM [request].[RequestBlockers]
               WHERE [IsCleared] = 0
               GROUP BY [RequestId]
           ) b ON r.[Id] = b.[RequestId];');
END;
```

## TD-005: Commands & REST API Endpoints

1. **Commands** in `RequestCommands.cs`:
   - `AddRequestBlockerCommand(Guid RequestId, string Reason, Guid ActorPersonId)`
   - `ClearRequestBlockerCommand(Guid RequestId, Guid BlockerId, string? ClearanceReason, Guid ActorPersonId)`
2. **REST Endpoints** in `RequestsController.cs`:
   - `POST /api/v1/requests/{id}/blockers`:
     - Request body: `{ "reason": "string", "actorPersonId": "Guid?" }`
     - Returns `201 Created` with `RequestDto`.
   - `POST /api/v1/requests/{id}/blockers/{blockerId}/clear`:
     - Request body: `{ "clearanceReason": "string?", "actorPersonId": "Guid?" }`
     - Returns `200 OK` with `RequestDto`.

## TD-006: Authorization & Role Policies

- **Adding a Blocker**:
  - Any authenticated actor or collaborator can add a blocker.
- **Clearing a Blocker**:
  - In `RequestService.ClearBlockerAsync`:
    ```csharp
    var isOwner = request.OwnerPersonId.HasValue && request.OwnerPersonId.Value == resolvedActorId;
    if (!isOwner)
    {
        var roles = await _organizationQueryService.GetPersonRolesAsync(resolvedActorId, cancellationToken);
        var isAuthorized = roles.Any(r => r is "Administrator" or "Manager" or "System Administrator" or "Team Lead");
        if (!isAuthorized)
        {
            throw new UnauthorizedAccessException(
                $"Actor '{resolvedActorId}' is not authorized to clear blocker '{blockerId}' on request '{requestId}'. Only the assigned owner, Administrator, or Manager can clear blockers.");
        }
    }
    ```

## TD-007: DTO Models & Telemetry Alignment

1. `RequestBlockerDto`:
   ```csharp
   public sealed record RequestBlockerDto(
       Guid Id,
       Guid RequestId,
       string Reason,
       Guid CreatedByPersonId,
       string? CreatedByName,
       DateTime CreatedAtUtc,
       bool IsCleared,
       DateTime? ClearedAtUtc,
       Guid? ClearedByPersonId,
       string? ClearedByName,
       string? ClearanceReason);
   ```
2. `RequestDto`:
   - `int ActiveBlockersCount` (authoritative count from entity)
   - `bool HasActiveBlockers => ActiveBlockersCount > 0`
   - `IReadOnlyList<RequestBlockerDto> Blockers`
3. `WorkPackageDto`:
   - `BlockerNote`: Enhanced to return `Request?.Blockers?.LastOrDefault(b => !b.IsCleared)?.Reason` with fallback to `Request?.Assignments?.LastOrDefault(a => a.NewStatus == "PAUSED")?.Notes`.

## TD-008: Frontend UX Design (`SCR-REQ-003`, `SCR-REQ-004`, `SCR-WP-001`)

1. **`SCR-REQ-003: Request Detail` (`RequestDetailView.vue`)**:
   - Under the Sub-Tasks section, render a Bootstrap card `data-testid="request-blockers-card"`:
     - Header: "Operational Blockers", active count pill (`bg-danger`), and an "Add Blocker" button (`btn-outline-danger`).
     - **Active Blockers section**:
       - Card/table of open blockers showing reason, reporter name, timestamp, and a "Clear Blocker" button (`btn-sm btn-success`).
     - **Cleared Blockers history**:
       - Collapsible section showing resolved blockers with reporter, resolver, clearance notes, and resolution duration.
     - **Add Blocker Modal**:
       - Form with textarea (`reason`) and submit button.
     - **Clear Blocker Modal**:
       - Form with textarea (`clearanceReason`, optional) and confirm button.
     - **Lifecycle Button Invariant Protection**:
       - When `request.activeBlockersCount > 0`, the "Start / Resume Work" button and "Complete Request" button are disabled with explanatory tooltips: `"Cannot resume/complete while active blockers exist"`.
2. **`SCR-REQ-004: My Requests` (`MyRequestsView.vue`)**:
   - Card headers render `<span v-if="req.activeBlockersCount > 0" class="badge bg-danger-subtle text-danger border border-danger"><i class="bi bi-shield-slash"></i> Blocked</span>`.
   - Card body displays active blocker alert callout if blocked.
3. **`SCR-WP-001: Work Package` (`WorkPackageView.vue`)**:
   - Scope list items render the active blocker badge and latest active blocker reason.

---

# 5. Component Responsibilities

| Component | Responsibility |
|------------|---------------|
| `RequestBlocker` | Child domain entity maintaining single blocker state, reason, timestamps, clearance flag, and actors. |
| `Request` (Aggregate Root) | Authoritative aggregate consistency boundary; enforces state transition to `PAUSED` on blocker addition; validates 0 active blockers for `StartWork` and `Complete`; emits domain events. |
| `RequestRepository` | Executes parameterized Dapper SQL against `[request].[RequestBlockers]` and `[request].[Requests]`. |
| `RequestService` | Orchestrates commands, resolves actor identity, verifies role authorization for blocker clearance, persists changes, and dispatches events. |
| `RequestsController` | Exposes REST endpoints for blocker addition and clearance, validating incoming HTTP payloads and returning updated DTOs. |
| `requests.ts` (API Client) | Typed client interfaces and axios calls (`addBlocker`, `clearBlocker`) for frontend consumption. |
| `RequestDetailView.vue` | Primary workbench UI rendering active blockers, Add/Clear modals, clearance history, and disabling invalid actions. |
| `MyRequestsView.vue` | Surfaces visual `[Blocked]` indicators and callouts on assigned request cards. |
| `WorkPackageView.vue` | Renders active blocker indicators on constituent requests within Work Package scope items. |

---

# 6. Integration Design

| Source | Target | Purpose |
|----------|----------|----------|
| `RequestDetailView.vue` | `RequestsController` (`POST .../blockers`) | Submits new impediment blocker. |
| `RequestDetailView.vue` | `RequestsController` (`POST .../clear`) | Submits blocker clearance with resolution note. |
| `RequestsController` | `IRequestService` | Passes command to application service. |
| `RequestService` | `IOrganizationQueryService` | Verifies actor permissions for clearing blockers when actor is not request owner. |
| `RequestService` | `IRequestRepository` | Loads and persists `Request` aggregate root with blockers and updated `ActiveBlockersCount`. |
| `Request` | `IDomainEventPublisher` | Emits `RequestBlockerAdded` and `RequestBlockerCleared` events upon transactional commit. |

---

# 7. Data Ownership

| Data | Owner |
|--------|--------|
| `RequestBlocker` entities | `Cakra.Modules.Request` (`request.RequestBlockers`) |
| Request lifecycle status (`Status`, `ActiveBlockersCount`) | `Cakra.Modules.Request` (`request.Requests`) |
| Person / Actor Identity | `Cakra.Modules.Organization` (`organization.Persons`) |
| Work Package Scope Linkage | `Cakra.Modules.WorkPackage` (`workpackage.WorkPackageRequests`) |

---

# 8. Database Design

## New Tables

| Table | Purpose |
|---------|---------|
| `[request].[RequestBlockers]` | Stores granular operational blockers and clearance audit history linked to parent requests. |

## Modified Tables

| Table | Change |
|---------|---------|
| `[request].[Requests]` | Add column `[ActiveBlockersCount] INT NOT NULL DEFAULT 0` with constraint `CHECK ([ActiveBlockersCount] >= 0)`. |

## Relationships

- `FK_RequestBlockers_Requests`: `[request].[RequestBlockers]([RequestId])` references `[request].[Requests]([Id])` with `ON DELETE CASCADE`.

## Migration Considerations

- Migration script: `0021_request_blockers.sql` in `Cakra.Api/Migrations/Scripts`.
- Purely additive: adds new table and a nullable/defaulted counter column to `[request].[Requests]`.
- Existing systems have 0 blockers; backfill script sets `ActiveBlockersCount = 0` safely.
- Fully idempotent using `IF OBJECT_ID(...) IS NULL` and `sys.columns` guards.

---

# 9. Cross-Cutting Concerns

- **Security & Authorization**: Any authenticated user can record a blocker; clearance is restricted strictly to assigned Request Owner, Administrator, or Manager.
- **Audit & Immutability**: Blockers cannot be deleted or modified in place. Complete creation and clearance metadata is retained indefinitely.
- **Consistency**: Invariants enforced at aggregate root level ensures that no state transition bypasses blocker constraints.
- **Observability**: Domain events emitted into application pipeline enable downstream notification, feed timeline posts, and metrics updates.

---

# 10. Implementation Constraints

- **Language & Platform**: .NET 8 (C# 12) backend, Vue 3 + TypeScript frontend.
- **ORM / Persistence**: Dapper with raw parameterized SQL; Entity Framework is forbidden.
- **Database**: SQL Server 2022 compatibility.
- **UI Guidelines**: Bootstrap 5 dark theme consistent with Cakra UI design tokens and responsive breakpoints.
- **Component Tests**: Comprehensive Vitest unit tests in `Cakra.Web` and xUnit tests in `Cakra.Tests.Unit`.

---

# 11. Acceptance Conditions

1. `Request.AddBlocker` adds a blocker, increments `ActiveBlockersCount`, transitions `IN_PROGRESS` or `ASSIGNED` to `PAUSED`, and emits `RequestBlockerAdded`.
2. Calling `Request.Complete` or `Request.StartWork` with 1 or more active blockers throws `RequestHasActiveBlockersException`.
3. `Request.ClearBlocker` marks the blocker cleared, decrements `ActiveBlockersCount`, leaves status as `PAUSED`, and emits `RequestBlockerCleared`.
4. Non-owners (without admin/manager roles) are rejected with `UnauthorizedAccessException` when attempting to clear a blocker.
5. Migration `0021_request_blockers.sql` executes idempotently and creates table `request.RequestBlockers` with index and foreign key.
6. `RequestRepository` persists and hydrates blockers alongside assignments and subtasks.
7. `RequestsController` exposes `/blockers` and `/blockers/{blockerId}/clear` endpoints.
8. `SCR-REQ-003` renders the Blockers card, enables adding and clearing blockers, and disables Start Work / Complete buttons when active blockers exist.
9. `SCR-REQ-004` and `SCR-WP-001` render visual `[Blocked]` badges when `activeBlockersCount > 0`.
10. All unit and integration test suites pass with 0 regressions.
