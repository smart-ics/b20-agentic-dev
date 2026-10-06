---
Title: Work in Progress (WIP) Tracking and Single In-Progress Task Policy Architecture (CR-021)
Code: CR-021
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-07
---

# 1. Overview

This architecture defines the technical realization for Change Request `CR-021`: Operations-wide Work in Progress (WIP) tracking dashboard, cumulative in-progress duration calculation across start/pause cycles, and Single In-Progress Task Policy enforcement across the Cakra system.

It consumes and realizes the approved feasibility decisions from [CR-021-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-021-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`), establishing:

1. Application service invariant guard enforcing the **Single In-Progress Task Policy** in `RequestService.StartWorkAsync`, strictly preventing an owner from starting multiple concurrent active tasks.
2. High-performance interval calculation algorithm in `RequestQueryService` aggregating cumulative elapsed `IN_PROGRESS` duration across all historical start-to-pause and start-to-complete intervals in `[request].[RequestAssignments]`, including dynamic real-time duration up to server `UtcNow` for currently in-progress tasks.
3. Operations-wide REST API endpoint `GET /api/v1/requests/wip` in `RequestsController.cs` accessible to all authenticated operators (`[Authorize]`).
4. Read model DTOs (`PersonWorkInProgressDto`, `TaskWorkInProgressDto`) grouping active work strictly by current assigned owner (`OwnerPersonId`).
5. Operations frontend screen `WorkInProgressView.vue` (`SCR-REQ-006`) at `/operations/wip`, integrated into the Operations sidebar navigation in `App.vue` and registered in `router/index.ts`.

---

# 2. Architectural Basis

## Business Context

- ISSUE: [CR-021-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-021-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- DOMAIN: [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-021-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-021-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

This architecture consumes and realizes the approved decisions:
- `GAP-001`: Domain enforcement of single in-progress policy in `RequestService.StartWorkAsync`.
- `GAP-002` & `GAP-003`: WIP query projection, cumulative interval calculation, and `GET /api/v1/requests/wip` REST endpoint.
- `GAP-004`, `GAP-005`, `GAP-006`: Frontend screen `WorkInProgressView.vue`, navigation route `/operations/wip`, sidebar menu integration, and TypeScript client methods.
- Closed decisions `OQ-001` through `OQ-009`: Strict rejection on start attempt, current owner attribution, inclusion of persons with only paused tasks, full task lifetime cumulative duration, real-time live duration, `Xh Ym` formatting, and ordering hierarchy.

---

# 3. Scope

## Included

1. **Domain Policy Enforcement (`Cakra.Modules.Request.Services.RequestService`)**:
   - In `RequestService.StartWorkAsync`, execute pre-flight query checking whether the assigned owner already has another request in `IN_PROGRESS` status.
   - If an existing in-progress request is found, reject the command by throwing `RequestDomainValidationException`.
   - Add repository lookup `GetActiveInProgressByOwnerAsync(Guid ownerPersonId, CancellationToken cancellationToken)` on `IRequestRepository`.
2. **Cumulative In-Progress Duration Calculation (`Cakra.Modules.Request.Services.RequestQueryService`)**:
   - Query all active requests in `IN_PROGRESS` or `PAUSED` status where `OwnerPersonId IS NOT NULL`.
   - Query chronological assignment audit records from `[request].[RequestAssignments]` for those active requests.
   - Reconstruct each `IN_PROGRESS` interval:
     - Start: `AssignedAtUtc` of row with `NewStatus = 'IN_PROGRESS'`.
     - End: `AssignedAtUtc` of the immediate next assignment row for that request, or `DateTime.UtcNow` if the request is currently in `IN_PROGRESS` and has no subsequent row.
     - Sum duration in seconds -> compute `TotalInProgressSeconds`, `TotalInProgressHours`, and `TotalInProgressFormatted` (e.g. `"3h 45m"`).
3. **Query Contract & DTO Models (`Cakra.Modules.Request`)**:
   - Define `TaskWorkInProgressDto` and `PersonWorkInProgressDto`.
   - Add `GetWorkInProgressOverviewAsync(CancellationToken cancellationToken)` to `IRequestQueryService` and implement in `RequestQueryService`.
   - Enrich person identity details via `IOrganizationQueryService` and customer details via `ICustomerQueryService`.
4. **REST API Endpoint (`Cakra.Api.Controllers.RequestsController`)**:
   - Expose `GET /api/v1/requests/wip` protected by standard `[Authorize]` (accessible to all authenticated users).
   - Returns HTTP 200 OK with `IReadOnlyList<PersonWorkInProgressDto>`.
5. **Frontend API Client (`src/frontend/Cakra.Web/src/api/requests.ts`)**:
   - Export TypeScript interfaces `PersonWorkInProgressDto` and `TaskWorkInProgressDto`.
   - Export function `getWorkInProgressOverview(): Promise<PersonWorkInProgressDto[]>`.
6. **Frontend View & Navigation (`src/frontend/Cakra.Web`)**:
   - Create `WorkInProgressView.vue` (`SCR-REQ-006: Work in Progress`).
   - Register route `/operations/wip` in `router/index.ts`.
   - Add sidebar menu link "Work in Progress" under Operations in `App.vue` with icon `bi-hourglass-split`.
   - Display persons with active in-progress tasks first (sorted by name), followed by persons with only paused tasks.
   - For each person, highlight the single in-progress task (or display idle placeholder) and list paused tasks sorted by most recently updated.
   - Provide direct links from each task to the Request Detail screen (`/requests/{id}`).

## Excluded

- Modifying existing database schema or adding new tables (existing `Requests` and `RequestAssignments` tables are utilized).
- Search or filter inputs on the WIP view (designed as a clean, complete overview).
- Inline action controls (Pause/Start/Complete) directly on the WIP view (actions are executed via `/requests/{id}`).
- Cross-schema foreign keys or direct SQL cross-schema joins (preserves module isolation).

---

# 4. Technical Decisions

## TD-001: Single In-Progress Task Policy Enforcement in `RequestService`

The domain aggregate `Request` manages its own internal state machine, while multi-aggregate invariants across the system are enforced at the application service boundary:

1. **Repository Method**:
   In `IRequestRepository` and `RequestRepository.cs`:
   ```csharp
   Task<Request?> GetActiveInProgressByOwnerAsync(
       Guid ownerPersonId, 
       CancellationToken cancellationToken = default);
   ```
   Implemented via parameterized SQL:
   ```sql
   SELECT TOP (1) r.*
   FROM [request].[Requests] r
   WHERE r.[OwnerPersonId] = @OwnerPersonId
     AND r.[Status] = 'IN_PROGRESS';
   ```

2. **Application Service Enforcement**:
   In `RequestService.StartWorkAsync`:
   ```csharp
   if (request.OwnerPersonId.HasValue)
   {
       var existingInProgress = await _requestRepository.GetActiveInProgressByOwnerAsync(
           request.OwnerPersonId.Value, 
           cancellationToken);

       if (existingInProgress != null && existingInProgress.Id != requestId)
       {
           throw new RequestDomainValidationException(
               $"Cannot start work on request '{request.Title}' because the assigned owner already has an active task in progress: '{existingInProgress.Title}'. Please pause or complete it first.",
               nameof(requestId));
       }
   }
   ```
   If violated, the API returns HTTP 400 Bad Request with ProblemDetails, preventing concurrent multitasking.

## TD-002: Cumulative In-Progress Duration Calculation Algorithm

To accurately capture time across multiple start/pause cycles (`START -> PAUSE -> START -> PAUSE -> ...`), elapsed time is derived directly from chronological audit entries in `[request].[RequestAssignments]`:

1. **Query Active Requests & Audit Intervals**:
   - Query all requests where `Status IN ('IN_PROGRESS', 'PAUSED') AND OwnerPersonId IS NOT NULL`.
   - Query all `RequestAssignments` for those request IDs ordered by `AssignedAtUtc ASC`.

2. **Interval Computation Logic**:
   For each request:
   - Identify all assignment rows where `NewStatus = 'IN_PROGRESS'`.
   - For each `IN_PROGRESS` row at index $i$:
     - If a subsequent assignment exists at index $i+1$, interval end is $T_{end} = \text{row}_{i+1}.\text{AssignedAtUtc}$.
     - If no subsequent assignment exists:
       - If the request's current `Status` is `IN_PROGRESS`, the interval is currently active: $T_{end} = \text{referenceUtc}$ (where $\text{referenceUtc} = \text{DateTime.UtcNow}$).
       - If the request's current `Status` is `PAUSED`, fallback to $T_{end} = \text{request.UpdatedAt ?? request.CreatedAt}$.
     - Interval duration: $\Delta t = \max(0, (T_{end} - T_{start}).\text{TotalSeconds})$.
   - Cumulative in-progress seconds: $S = \sum \Delta t$.
   - Cumulative decimal hours: $H = \text{Math.Round}(S / 3600.0, 2)$.
   - Formatted string:
     ```csharp
     var timeSpan = TimeSpan.FromSeconds(S);
     var hours = (int)timeSpan.TotalHours;
     var minutes = timeSpan.Minutes;
     var formatted = $"{hours}h {minutes}m";
     ```

## TD-003: Read Model DTO Contracts

In `Cakra.Modules.Request.Models`:

```csharp
public sealed record TaskWorkInProgressDto
{
    public Guid RequestId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string RequestType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = "NORMAL";
    public Guid? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerCode { get; init; }
    public Guid? ProductId { get; init; }
    public Guid? WorkPackageId { get; init; }
    public double TotalInProgressSeconds { get; init; }
    public double TotalInProgressHours { get; init; }
    public string TotalInProgressFormatted { get; init; } = "0h 0m";
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? LastStartedAt { get; init; }
}

public sealed record PersonWorkInProgressDto
{
    public Guid PersonId { get; init; }
    public string PersonName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public TaskWorkInProgressDto? InProgressTask { get; init; }
    public IReadOnlyList<TaskWorkInProgressDto> PausedTasks { get; init; } = Array.Empty<TaskWorkInProgressDto>();
    public int TotalActiveTasksCount => (InProgressTask is not null ? 1 : 0) + PausedTasks.Count;
}
```

## TD-004: REST API Endpoint Specification

In `RequestsController.cs`:

```csharp
/// <summary>
/// Retrieves the real-time Work in Progress (WIP) overview across all persons with active
/// or paused requests, including cumulative elapsed IN_PROGRESS hours per task (CR-021).
/// </summary>
[HttpGet("wip")]
[ProducesResponseType(typeof(IReadOnlyList<PersonWorkInProgressDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public async Task<ActionResult<IReadOnlyList<PersonWorkInProgressDto>>> GetWorkInProgressOverview(
    CancellationToken cancellationToken = default)
{
    var result = await _requestQueryService.GetWorkInProgressOverviewAsync(cancellationToken);
    return Ok(result);
}
```

## TD-005: Ordering and Presentation Hierarchy

The query projection enforces the agreed presentation structure:
1. **Persons Ordering**:
   - Persons with an active `InProgressTask != null` are placed first, sorted alphabetically by `PersonName`.
   - Persons with only `PausedTasks` (where `InProgressTask == null`) follow, sorted alphabetically by `PersonName`.
2. **Tasks Ordering within Person**:
   - The single `InProgressTask` is displayed at the top in a distinct primary card with an animated or highlighted in-progress badge.
   - `PausedTasks` are rendered below, ordered by `COALESCE(UpdatedAt, CreatedAt) DESC` (most recently paused first).

## TD-006: Frontend View & Navigation (`SCR-REQ-006`)

1. **Route (`router/index.ts`)**:
   ```typescript
   {
     path: '/operations/wip',
     name: 'work-in-progress',
     component: () => import('@/views/WorkInProgressView.vue'),
     meta: {
       requiresAuth: true,
       screenId: 'SCR-REQ-006',
     },
   }
   ```
2. **Operations Sidebar Navigation (`App.vue`)**:
   Under the `Operations` section, add:
   ```html
   <router-link
     class="sidebar-nav-item"
     to="/operations/wip"
     active-class="active"
     data-testid="nav-wip-link"
     :title="isCollapsed ? 'Work in Progress' : undefined"
     @click="closeMobile"
   >
     <i class="bi bi-hourglass-split nav-icon" aria-hidden="true"></i>
     <span v-show="!isCollapsed" class="nav-label">Work in Progress</span>
   </router-link>
   ```

---

# 5. Component Responsibilities

| Component | Responsibility |
|---|---|
| `IRequestRepository` / `RequestRepository.cs` | Executes queries against `[request].[Requests]` to find active `IN_PROGRESS` tasks by owner person ID. |
| `RequestService.cs` | Enforces the Single In-Progress Policy invariant in `StartWorkAsync`, validating that the owner has no other active task in progress before calling `request.StartWork`. |
| `IRequestQueryService` / `RequestQueryService.cs` | Executes the WIP projection query, aggregates assignment intervals into cumulative elapsed hours, enriches person and customer data, groups tasks by owner, and sorts the result list. |
| `RequestsController.cs` | Exposes `GET /api/v1/requests/wip` protected by standard `[Authorize]` returning HTTP 200 OK. |
| `src/api/requests.ts` | Provides TypeScript contracts (`PersonWorkInProgressDto`, `TaskWorkInProgressDto`) and the HTTP fetch function `getWorkInProgressOverview`. |
| `WorkInProgressView.vue` (`SCR-REQ-006`) | Renders the Work in Progress screen with summary header, person cards, single in-progress task spotlight, paused tasks list, elapsed time badges, and direct links to `/requests/{id}`. |
| `router/index.ts` | Registers the route `/operations/wip` mapped to `WorkInProgressView.vue`. |
| `App.vue` | Houses the top-level navigation link "Work in Progress" in the sidebar under Operations. |

---

# 6. Integration Design

| Source | Target | Purpose |
|---|---|---|
| `RequestsController.cs` | `IRequestQueryService` | Invokes `GetWorkInProgressOverviewAsync` to retrieve real-time WIP dataset. |
| `RequestQueryService` | Database (`[request]`) | Queries active requests and assignment rows via Dapper parameterized SQL. |
| `RequestQueryService` | `IOrganizationQueryService` | Resolves person full names and emails for all active request owners. |
| `RequestQueryService` | `ICustomerQueryService` | Resolves customer names and codes for active requests. |
| `RequestService.StartWorkAsync` | `IRequestRepository` | Calls `GetActiveInProgressByOwnerAsync` to detect conflicting in-progress tasks. |
| `WorkInProgressView.vue` | `api/requests.ts` | Calls `getWorkInProgressOverview()` upon mounting or manual refresh. |
| `WorkInProgressView.vue` | `router` (`/requests/{id}`) | Navigates the operator to the Request Detail screen when clicking any task card. |

---

# 7. Data Ownership

| Data | Owner | Description |
|---|---|---|
| Request Lifecycle & Assignments | `Cakra.Modules.Request` | Authoritative owner of requests, statuses (`IN_PROGRESS`, `PAUSED`), and assignment history rows. |
| Person Master Data | `Cakra.Modules.Organization` | Authoritative owner of Person identities (`PersonId`, `FullName`, `Email`). |
| Customer Master Data | `Cakra.Modules.Customer` | Authoritative owner of Customer identities (`CustomerId`, `CustomerName`, `CustomerCode`). |
| Cumulative WIP Projection | `Cakra.Modules.Request` (Query) | Dynamic read-only calculation combining request status and assignment duration. |

---

# 8. Database Design

## New Tables
None.

## Modified Tables
None.

## Relationships
Existing referential integrity between `[request].[RequestAssignments]` and `[request].[Requests]` via `FK_RequestAssignments_Requests` is utilized.

## Migration Considerations
No new database migration script is required for CR-021. Existing tables and nonclustered indexes on `[request].[Requests]` (`[Status]`, `[OwnerPersonId]`) and `[request].[RequestAssignments]` (`[RequestId]`) provide full data and performance coverage.

---

# 9. Cross-Cutting Concerns

- **Security & Authorization**:
  - `GET /api/v1/requests/wip` is secured with `[Authorize]` requiring authentication, but permits all valid operational roles (not restricted to Management).
- **Audit Preservation**:
  - Interval calculation purely reads from `[request].[RequestAssignments]`. It never mutates or overwrites historical assignment rows.
- **Performance & Scalability**:
  - The query filters assignment rows strictly for active requests (`Status IN ('IN_PROGRESS', 'PAUSED')`), preventing full table scans over closed historical requests.
- **Clock Authority**:
  - Live duration calculations strictly use server UTC clock (`DateTime.UtcNow`), preventing client timezone distortion.

---

# 10. Implementation Constraints

- Must use Dapper for SQL queries; zero Entity Framework usage.
- Strict zero cross-schema SQL joins; person and customer enrichment must use published domain query contracts (`IOrganizationQueryService`, `ICustomerQueryService`).
- Validation errors on `StartWorkAsync` must throw `RequestDomainValidationException` which maps cleanly to HTTP 400 Bad Request ProblemDetails.
- Frontend must use Vue 3 `<script setup lang="ts">`, Pinia, and Bootstrap 5 styling consistent with existing screens.

---

# 11. Acceptance Conditions

1. When a person already has a request in `IN_PROGRESS`, calling `POST /api/v1/requests/{id}/start` on another request owned by the same person returns HTTP 400 Bad Request with a clear message indicating an existing in-progress task.
2. When a person has no requests in `IN_PROGRESS`, calling `POST /api/v1/requests/{id}/start` succeeds and transitions the request to `IN_PROGRESS`.
3. `GET /api/v1/requests/wip` returns all persons who have at least one `IN_PROGRESS` or `PAUSED` request.
4. Each task in the WIP response correctly calculates `TotalInProgressSeconds`, `TotalInProgressHours`, and `TotalInProgressFormatted` across all historical start/pause intervals, plus ongoing live time if currently in progress.
5. In the WIP response, persons with an active `IN_PROGRESS` task appear first, followed by persons with only `PAUSED` tasks.
6. The frontend route `/operations/wip` renders `SCR-REQ-006` with the navigation link "Work in Progress" visible under Operations in the sidebar.
7. Clicking any task card on `SCR-REQ-006` navigates to `/requests/{id}`.
