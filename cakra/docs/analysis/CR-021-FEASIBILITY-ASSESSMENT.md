---
Title: Feasibility Assessment for Work in Progress (WIP) Tracking and Single In-Progress Task Policy (CR-021)
Code: CR-021
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-07
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Change being assessed: Implementation of an operations-wide Work in Progress (WIP) tracking dashboard and single in-progress task policy enforcement, as formally captured in [CR-021-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-021-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-021-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-021-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- DOMAIN: [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md)
- FEATURES: [FEAT-COL-004-review-assigned-requests.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-COL-004-review-assigned-requests.md)
- FEATURES: [FEAT-MGT-004-review-programmer-workload.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-MGT-004-review-programmer-workload.md)

## Objective

Assess the feasibility, domain boundaries, query aggregation mechanics, business rule enforcement, navigation, UI layout, and planning readiness to:

1. Enforce the Single In-Progress Task Policy: Prevent an owner from starting work on a request (`StartWork`) if they already have another active request in `IN_PROGRESS` status.
2. Calculate the cumulative elapsed `IN_PROGRESS` time for each task across all historical start-to-pause and start-to-complete intervals recorded in `[request].[RequestAssignments]`.
3. Include live ongoing duration up to current server UTC time if the task is currently in `IN_PROGRESS` status.
4. Expose an operations-wide query projection and REST API endpoint (`GET /api/v1/requests/wip`) accessible to all authenticated users, listing all persons who have active work (`IN_PROGRESS` or `PAUSED`).
5. Group active tasks strictly by currently assigned owner (`OwnerPersonId`), reflecting reassignment semantics where prior owners are no longer reported for reassigned tasks.
6. Provide a dedicated frontend screen `WorkInProgressView.vue` under the Operations section (`/operations/wip`, titled "Work in Progress") featuring:
   - Persons with an `IN_PROGRESS` task displayed first, followed by persons with only `PAUSED` tasks.
   - For each person, their single `IN_PROGRESS` task prominently featured at top (or idle state indicator), followed by `PAUSED` tasks sorted by most recently updated/paused.
   - Elapsed `IN_PROGRESS` time displayed in hours and minutes (`Xh Ym`), with total decimal hours available via tooltip and API payload.
   - Direct navigation links to [`/requests/{id}`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue).
   - Sidebar navigation integration under Operations in [`App.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue) and route registration in [`router/index.ts`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/router/index.ts).

---

# 2. Current State

## Existing Behavior

1. **Domain Model & Commands (`Cakra.Modules.Request`)**:
   - The `Request` aggregate root (`Request.cs`) manages lifecycle states: `Captured`, `Assigned`, `InProgress`, `Paused`, `Completed`, `Cancelled`.
   - `StartWork(Guid actorPersonId, ...)` transitions `ASSIGNED` or `PAUSED` into `IN_PROGRESS`, verifying only that `actorPersonId == OwnerPersonId`. It has no knowledge of other requests owned by the person.
   - `RequestService.StartWorkAsync` retrieves the target request, invokes `request.StartWork`, and persists the changes. It does not perform any pre-flight verification on whether the owner already has another request in `IN_PROGRESS`.
   - Consequently, an owner can call `StartWork` on arbitrarily many requests, resulting in multiple concurrent `IN_PROGRESS` tasks.

2. **Audit History & Time Tracking (`[request].[RequestAssignments]`)**:
   - `RequestAssignment` records each lifecycle state transition, including `PreviousStatus`, `NewStatus`, and `AssignedAtUtc`.
   - Every `StartWork` creates an assignment row with `NewStatus = 'IN_PROGRESS'`.
   - Subsequent state changes (`PauseWork`, `Complete`, `Cancel`, `AssignOwner`) create subsequent assignment rows with their respective new statuses.
   - Currently, there is no service or query computing cumulative in-progress duration across multiple intervals. `RequestDto` only provides raw timestamps (`CreatedAt`, `UpdatedAt`) without elapsed work duration.

3. **Query & REST API Endpoints (`Cakra.Api`, `Cakra.Modules.Request.Services`)**:
   - `RequestsController.cs` provides `GET /api/v1/requests/my` (personal queue for logged-in user), `GET /api/v1/requests/assigned-subtasks`, and `GET /api/v1/requests/{id}/history`.
   - There is no operations-wide endpoint returning all team members' active and paused work.
   - `AnalyticsController.cs` exposes `GET /api/v1/analytics/programmer-workload` (`SCR-MGT-003`), but this endpoint is restricted by `[Authorize(Roles = "Management")]`, only provides high-level counts (`inProgressCount`, `pausedCount`), and does not compute elapsed in-progress time per task.

4. **Frontend Shell & Views (`Cakra.Web`)**:
   - `App.vue` defines navigation sections: `Operations` (Feed, My Requests, Work Packages), `Catalog` (Product Catalog), `Management` (Customer Portfolio, Programmer Performance, Programmer Workload), and `Administration` (User Management, Person Management).
   - There is no "Work in Progress" navigation link or route (`/operations/wip`).
   - The only screen showing programmer workloads is `ProgrammerWorkloadView.vue` (`/analytics/programmer-workload`), which is designed for management oversight and inaccessible to non-management personnel.

## Existing Constraints

1. **Architecture Module Boundaries**:
   - `Cakra.Modules.Request` owns the Request aggregate, lifecycle transitions, and request query projections.
   - `Cakra.Modules.Organization` owns Person master records. Cross-module queries must resolve person names via `IOrganizationQueryService` without database foreign keys (Architecture §6, §15, §20).
2. **Role-Based Access Control**:
   - Unlike Management Analytics which requires `[Authorize(Roles = "Management")]`, the Work in Progress screen must be accessible to all authenticated operators (`[Authorize]`).
3. **Audit Immutability**:
   - The chronological audit records in `[request].[RequestAssignments]` are append-only. Cumulative elapsed time calculations must read existing assignment rows without modifying historical records.
4. **Time & Interval Calculation Rules**:
   - Total in-progress time must be calculated from historical intervals (`IN_PROGRESS` start to interval end).
   - If a request is currently `IN_PROGRESS`, the open interval (`UtcNow - LastStartedAt`) must be dynamically included.
   - Time calculations must use UTC timestamps (`AssignedAtUtc`, `DateTime.UtcNow`) to eliminate timezone bias.

---

# 3. Gap Analysis

| ID | Severity | Gap |
|---|---|---|
| GAP-001 | CRITICAL | `RequestService.StartWorkAsync` lacks enforcement of the single in-progress task policy, permitting an owner to have multiple `IN_PROGRESS` tasks simultaneously. |
| GAP-002 | CRITICAL | No query service exists to calculate cumulative `IN_PROGRESS` elapsed time across historical intervals in `[request].[RequestAssignments]` and group active tasks by assigned owner. |
| GAP-003 | MAJOR | `RequestsController.cs` lacks an operations-wide `GET /api/v1/requests/wip` endpoint accessible to all authenticated users. |
| GAP-004 | MAJOR | Frontend application lacks `WorkInProgressView.vue` (`/operations/wip`) rendering active persons, their single in-progress task, and paused tasks with formatted elapsed time. |
| GAP-005 | MAJOR | Sidebar navigation in `App.vue` and route definitions in `router/index.ts` lack the "Work in Progress" link under the Operations section. |
| GAP-006 | MINOR | Frontend API layer (`api/requests.ts`) lacks DTO interfaces and client methods for querying the WIP endpoint. |

---

# 4. Open Questions

| ID | Question | Impact | Status |
|---|---|---|---|
| OQ-001 | Should the single in-progress task constraint be enforced as a domain validation on `StartWork` or purely as a reporting expectation? | Domain validation in `RequestService`, exception handling, API error responses. | CLOSED |
| OQ-002 | When ownership of a task switches from User-A to User-B, how should the task be reported? | Grouping and attribution logic in query projection. | CLOSED |
| OQ-003 | If a person has no `IN_PROGRESS` task but has one or more `PAUSED` tasks, should they appear in the WIP list? | Inclusion filter in the query projection and UI empty state handling. | CLOSED |
| OQ-004 | If a task was worked on by previous owners before reassignment, how should total `IN_PROGRESS` elapsed time be calculated? | Cumulative interval aggregation algorithm across `RequestAssignments`. | CLOSED |
| OQ-005 | For tasks currently in `IN_PROGRESS`, should elapsed time include the live ongoing duration up to the current moment? | Real-time interval calculation using server `UtcNow`. | CLOSED |
| OQ-006 | How should elapsed `IN_PROGRESS` time be formatted and presented on screen? | UI string formatting, tooltips, and API response payload precision. | CLOSED |
| OQ-007 | Where in the application navigation should the screen live, what should its route be, and who has access? | Router configuration, `App.vue` sidebar menu, and controller authorization attributes. | CLOSED |
| OQ-008 | How should persons and their tasks be ordered on the Work in Progress screen? | Query sorting and UI layout hierarchy. | CLOSED |
| OQ-009 | What search/filter controls or inline action controls are required on the WIP screen? | Screen UI component complexity and interaction design. | CLOSED |

---

# 5. Assumptions

| ID | Assumption |
|---|---|
| ASM-001 | Cumulative in-progress intervals are authoritatively reconstructed from `[request].[RequestAssignments]` where `NewStatus = 'IN_PROGRESS'`. |
| ASM-002 | The single in-progress task policy is enforced at the application service boundary in `RequestService.StartWorkAsync` prior to calling `request.StartWork`. |
| ASM-003 | If a task has no recorded assignment intervals (e.g. legacy/mock test records), its total elapsed in-progress time defaults to 0 seconds. |
| ASM-004 | Reassignment (`AssignOwner`) sets the request status to `ASSIGNED`, so assigning or reassigning a task does not create an `IN_PROGRESS` conflict until the owner calls `StartWork`. |
| ASM-005 | An HTTP 400 Bad Request (ProblemDetails) is returned when `StartWork` is rejected due to an existing in-progress task, communicating the conflict clearly to the caller. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| RISK-001 | Performance of calculating cumulative intervals from `RequestAssignments` across multiple active tasks. | Slow response times for `GET /api/v1/requests/wip` as assignment history grows. | Filter `RequestAssignments` strictly for active request IDs (`IN_PROGRESS` and `PAUSED`) using indexed queries; aggregate intervals in a single optimized SQL query or efficient in-memory projection. |
| RISK-002 | Concurrent race condition when an owner initiates `StartWork` on two tasks simultaneously. | Owner could theoretically end up with two `IN_PROGRESS` tasks if requests run concurrently. | Query database for existing active `IN_PROGRESS` tasks owned by the person within `RequestService.StartWorkAsync` before state transition. |
| RISK-003 | Clock skew between client browser and server for live ongoing duration. | Inconsistent elapsed time displayed to users across different timezones or drifting client clocks. | Server computes elapsed duration using authoritative server UTC clock (`DateTime.UtcNow`); frontend displays server-computed formatted duration. |
| RISK-004 | Reassigned task ownership confusion. | Users might expect historical owners to still show the task. | Clarified during intake: only current assigned owner reports the task; cumulative task duration reflects overall task lifecycle. |

---

# 7. Recommendations

## Option A: Full-Stack Operations Integration (Recommended)

1. **Domain & Command Layer**:
   - In `RequestService.StartWorkAsync`, execute a check against `IRequestRepository` (or direct SQL query) verifying whether `request.OwnerPersonId` currently owns any other request where `Status = 'IN_PROGRESS'`.
   - If an existing in-progress request is found, throw `RequestDomainValidationException` stating: `"Cannot start work on request '{Title}' because the assigned owner already has an active task in progress: '{ActiveTitle}'. Please pause or complete it first."`
2. **Query & Service Layer**:
   - In `IRequestQueryService` and `RequestQueryService`, introduce `GetWorkInProgressOverviewAsync(CancellationToken)`.
   - Query all requests where `Status IN ('IN_PROGRESS', 'PAUSED')` and `OwnerPersonId IS NOT NULL`.
   - Query corresponding `RequestAssignments` rows for those request IDs.
   - For each request, calculate cumulative in-progress seconds by iterating chronological assignments:
     - An interval starts when `NewStatus = 'IN_PROGRESS'`.
     - The interval ends at the timestamp of the next assignment, or at `UtcNow` if no subsequent assignment exists and `Status == 'IN_PROGRESS'`.
     - Sum all interval seconds -> `TotalInProgressSeconds`, `TotalInProgressHours = TotalInProgressSeconds / 3600.0`, and formatted string `TotalInProgressFormatted` (e.g. `"3h 45m"`).
   - Enrich person details via `IOrganizationQueryService` and customer details via `ICustomerQueryService`.
   - Group by `PersonId`: exactly one `InProgressTask` (nullable) and a list of `PausedTasks` (sorted by most recently updated).
   - Sort persons: those with an active `InProgressTask` first (alphabetical by name), followed by persons with only `PausedTasks`.
3. **REST API**:
   - Add `[HttpGet("wip")]` in `RequestsController.cs` protected by `[Authorize]`.
   - Returns `IReadOnlyList<PersonWorkInProgressDto>`.
4. **Frontend UI**:
   - Create `WorkInProgressView.vue` in `cakra/src/frontend/Cakra.Web/src/views/`.
   - Register route `/operations/wip` (`SCR-REQ-006: Work in Progress`) in `router/index.ts`.
   - Add sidebar menu link "Work in Progress" under Operations in `App.vue` with an appropriate icon (e.g., `bi-hourglass-split`).
   - Render clean person cards displaying person name, single highlighted in-progress task (or idle badge), and list of paused tasks, each showing elapsed duration and link to `/requests/{id}`.

### Advantages
- Completely satisfies all user requirements and collaborative intake agreements.
- Enforces single in-progress policy at the domain/application boundary without breaking existing database constraints.
- Clean separation between operations-wide visibility (`/operations/wip`) and management analytics (`/analytics/programmer-workload`).
- Efficient, centralized interval calculation on server.

### Disadvantages
- Requires implementation across backend command, query service, controller, and frontend Vue SPA.

## Option B: Query-Only Implementation (No Validation Enforcement)

- Implement the query and UI only, without enforcing the single in-progress constraint in `StartWork`.

### Advantages
- Slightly fewer backend changes.

### Disadvantages
- Fails the core requirement agreed during the `/grill-me` session ("Both: build the listing query/view and also enforce domain validation preventing a user from starting a second IN_PROGRESS task"). Rejected.

---

# 8. Gap Closure

## GAP-001 (Single In-Progress Policy Enforcement)
### Decision
Enforce the single in-progress policy in `RequestService.StartWorkAsync`. Before invoking `request.StartWork`, check if the owner has another request in `IN_PROGRESS` status. If so, throw `RequestDomainValidationException` with details of the conflicting request.
### Rationale
Ensures operational discipline and prevents concurrent multitasking on conflicting demands, strictly as specified in intake.
### Impact
`Cakra.Modules.Request/Services/RequestService.cs`, `Cakra.Modules.Request/Domain/Exceptions/RequestDomainValidationException.cs`.
### Architecture Impact
Application service command validation rule and invariant guard.
### Resolved By
User & Analyst
### Resolved Date
2026-10-07

---

## GAP-002 & GAP-003 (WIP Query Projection, Interval Calculation, and REST API)
### Decision
Add `GetWorkInProgressOverviewAsync` to `IRequestQueryService` and `RequestQueryService.cs`. Compute cumulative in-progress intervals from `[request].[RequestAssignments]`. Add `[HttpGet("wip")]` endpoint in `RequestsController.cs` returning `IReadOnlyList<PersonWorkInProgressDto>` protected by standard `[Authorize]`.
### Rationale
Provides high-performance, accurate interval duration calculations and serves operations-wide data to all authenticated personnel.
### Impact
`Cakra.Modules.Request/IRequestQueryService.cs`, `Cakra.Modules.Request/Services/RequestQueryService.cs`, `Cakra.Modules.Request/Models/WorkInProgressDto.cs`, `Cakra.Api/Controllers/RequestsController.cs`.
### Architecture Impact
New query contract, read model DTOs, and REST API endpoint.
### Resolved By
User & Analyst
### Resolved Date
2026-10-07

---

## GAP-004, GAP-005 & GAP-006 (Frontend View, Navigation, and API Client)
### Decision
Create `WorkInProgressView.vue`, register route `/operations/wip` in `router/index.ts`, add "Work in Progress" link under the Operations section in `App.vue`, and expose `getWorkInProgressOverview` in `src/frontend/Cakra.Web/src/api/requests.ts`.
### Rationale
Delivers the user-facing operational screen accessible to everyone, presenting active and paused work cleanly with direct links to task details.
### Impact
`Cakra.Web/src/views/WorkInProgressView.vue`, `Cakra.Web/src/router/index.ts`, `Cakra.Web/src/App.vue`, `Cakra.Web/src/api/requests.ts`.
### Architecture Impact
New Vue screen component and navigation tree addition.
### Resolved By
User & Analyst
### Resolved Date
2026-10-07

---

## OQ-001 through OQ-009 (Decisions Aligned from Grill-Me Session)
### Decision
All open questions are formally closed based on the intake interview:
- OQ-001: Enforce single `IN_PROGRESS` constraint on `StartWork`, rejecting duplicates with a validation error.
- OQ-002: Tasks are attributed strictly to their currently assigned owner. Reassigned tasks do not appear under prior owners.
- OQ-003: Persons with no `IN_PROGRESS` task but having `PAUSED` tasks are included in the WIP list with an idle in-progress status.
- OQ-004: Cumulative `IN_PROGRESS` duration is computed across the task's full lifetime (all historical intervals).
- OQ-005: If a task is currently in `IN_PROGRESS`, the open ongoing interval (`UtcNow - LastStartedAt`) is included in the total.
- OQ-006: Display formatted as hours and minutes (`Xh Ym`), with total decimal hours available via tooltip and API payload.
- OQ-007: Placed under Operations at `/operations/wip` with label "Work in Progress", open to all authenticated users.
- OQ-008: Persons with an active in-progress task appear first (sorted by name), followed by persons with only paused tasks; paused tasks are sorted by most recently updated.
- OQ-009: No search/filter controls or inline action controls; each task links directly to `/requests/{id}`.
### Rationale
Agreed collaboratively with stakeholder during interactive grill-me session.
### Impact
Provides unambiguous requirements across domain logic, query models, API contracts, and UI screens.
### Architecture Impact
Forms the definitive specification consumed by Architecture and Planning.
### Resolved By
User & Analyst
### Resolved Date
2026-10-07

---

# 9. Architecture Applicability

## Decision
ARCHITECTURE-REQUIRED

## Rationale
Architecture definition is required because this change introduces:
1. New operations-wide query projection and REST endpoint (`GET /api/v1/requests/wip`) accessible to all authenticated users.
2. In-progress time interval calculation algorithm aggregating multiple historical intervals from `[request].[RequestAssignments]` combined with real-time dynamic ongoing duration.
3. Domain invariant enforcement in `RequestService.StartWorkAsync` preventing multiple concurrent in-progress tasks per owner.
4. New top-level Vue view `WorkInProgressView.vue`, route `/operations/wip`, and Operations sidebar navigation updates in `App.vue`.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved
- [x] All required decisions recorded
- [x] All blocking open questions resolved
- [x] Architecture can be finalized or updated (Gate granted by Architect)

## Status

READY-FOR-PLANNING

## Notes

All feasibility analysis, gap identification, and gap closure decisions are completed and approved. The Architect has evaluated the artifact and granted the READY-FOR-PLANNING gate. Target architecture definition may proceed.

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-021-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-021-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- DOMAIN: [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md)
- FEATURES: [FEAT-COL-004-review-assigned-requests.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-COL-004-review-assigned-requests.md)
- FEATURES: [FEAT-MGT-004-review-programmer-workload.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-MGT-004-review-programmer-workload.md)

Referenced codebase locations:

- [Request.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs)
- [RequestAssignment.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/RequestAssignment.cs)
- [RequestService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs)
- [IRequestQueryService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/IRequestQueryService.cs)
- [RequestQueryService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestQueryService.cs)
- [RequestsController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/RequestsController.cs)
- [0006_request_tables.sql](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Migrations/Scripts/0006_request_tables.sql)
- [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue)
- [router/index.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/router/index.ts)
- [requests.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/requests.ts)
