---
Title: Feasibility Assessment for Request Blocker Capability and History Tracking
Code: CR-032
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-15
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Feature being assessed: Enable requests to record blocking impediments that prevent completion, automatically suspend active work (`PAUSED`), safeguard against premature completion or resumption while blocked, provide clearance capabilities with resolution explanations for request owners, maintain full immutable history of all blockers, and surface visual blocker indicators on workbench screens (`SCR-REQ-003`, `SCR-REQ-004`, `SCR-WP-001`), as formally captured in [CR-032-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-032-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-032-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-032-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- FEATURE: [FEAT-REQ-009-manage-request-blockers.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-009-manage-request-blockers.md)
- FEATURE: [FEAT-REQ-008-review-request-completion.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-008-review-request-completion.md)
- REFERENCE ARCHITECTURE: [CR-016-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-016-ARCHITECTURE.md) (Lifecycle Simplification)
- REFERENCE ARCHITECTURE: [CR-025-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-025-ARCHITECTURE.md) (Work Package Telemetry & Health Invariants)

## Objective

Assess the technical feasibility, architectural implications, gap closure resolutions, and planning readiness to:

1. Model a first-class `RequestBlocker` child entity within the `Request` aggregate root with full creation and clearance audit metadata.
2. Enforce domain invariants that automatically pause active work upon recording a blocker, strictly block `Complete` while any blocker is open, and prevent `StartWork` from resuming while active blockers remain.
3. Provide application service methods and REST endpoints to add a blocker and clear an active blocker with authorization checks.
4. Persist blockers to a dedicated relational table `request.RequestBlockers` with Dapper repository hydration.
5. Deliver an intuitive user interface on `SCR-REQ-003: Request Detail` with a dedicated Blockers card, Add/Clear blocker dialogs, and a historical log of resolved blockers.
6. Surface active blocker badges and reasons on `SCR-REQ-004: My Requests` and `SCR-WP-001: Work Package Scope`.

---

# 2. Current State

## Existing Behavior

1. **Aggregate Root (`Request.cs`)**:
   - `Request` aggregate root manages `_assignments` and `_subTasks`.
   - `PauseWork(actorPersonId, note, utcNow)` transitions `IN_PROGRESS -> PAUSED` and appends a single `RequestAssignment` with an optional ephemeral string note.
   - `StartWork(actorPersonId, notes, utcNow)` transitions `ASSIGNED -> IN_PROGRESS` or `PAUSED -> IN_PROGRESS` verifying only that the actor is the assigned owner; it does not check for active impediments or blockers.
   - `Complete(resolutionDescription, actorPersonId, utcNow)` transitions `IN_PROGRESS -> COMPLETED`, verifying that all checklist sub-tasks are complete via `_subTasks.Any(t => !t.IsCompleted)`. It performs no validation of blocking impediments.
   - `Cancel(reason, actorPersonId, utcNow)` transitions any open state to `CANCELLED`.
   - There is no domain entity or collection representing blockers on `Request`.
2. **Persistence (`IRequestRepository.cs` / `RequestRepository.cs`)**:
   - Manages relational persistence across `request.Requests`, `request.RequestAssignments`, `request.RequestSubTasks`, and `request.RequestResolutions`.
   - No table `request.RequestBlockers` exists, and repository hydration queries do not query or populate blocker collections.
3. **Application Services & API Controllers**:
   - `IRequestService` and `RequestService` provide lifecycle orchestration for `StartWorkAsync`, `PauseWorkAsync`, `CompleteRequestAsync`, and subtask operations.
   - `RequestsController` exposes REST endpoints (`/pause`, `/start-work`, `/complete`, `/cancel`, `/sub-tasks`), but exposes no endpoints for adding or clearing blockers.
4. **DTO Projections & Telemetry**:
   - `RequestDto` does not project blocker collections, active blocker counts, or blocker flags.
   - `WorkPackageDto` currently derives an ad-hoc `BlockerNote` by inspecting the last `PAUSED` assignment note or `EvaluationNotes`.
5. **Frontend Client & Screens**:
   - `api/requests.ts` contains API methods and interfaces for sub-tasks and status transitions, but none for blockers.
   - `RequestDetailView.vue` (`SCR-REQ-003`) provides a Pause Work modal collecting a note, but lacks any card or UI for managing multiple blockers, clearing blockers with clearance notes, or reviewing resolved blocker history.
   - `MyRequestsView.vue` (`SCR-REQ-004`) renders a `PAUSED` badge, but provides no visual indicator that a request is blocked by a specific obstacle.

## Existing Constraints

1. **Aggregate Boundary Integrity**:
   - The Request aggregate root must remain the single transactional boundary and consistency gate for all Request state transitions and invariants. `RequestBlocker` must be managed strictly through methods on `Request`.
2. **Lifecycle Invariant Rules**:
   - A request cannot transition to `COMPLETED` or resume to `IN_PROGRESS` while active blockers exist.
   - Adding a blocker to an `IN_PROGRESS` request must transition it to `PAUSED`.
   - Adding a blocker to an `ASSIGNED` request transitions it to `PAUSED` to indicate work cannot begin.
   - Clearing blockers must not auto-resume work; manual owner action is required to resume.
3. **Audit Immutability**:
   - Blockers cannot be edited or deleted. Every blocker records who added it and when; resolving a blocker records who cleared it, when, and an optional clearance explanation.
4. **Authorization Rules**:
   - Any collaborator can record a blocker.
   - Only the assigned Request Owner, Administrator, or Manager can clear a blocker.

---

# 3. Gap Analysis

| ID | Severity | Gap |
|------|------|------|
| GAP-001 | CRITICAL | The `Request` aggregate root does not model `RequestBlocker` child entities or maintain an internal collection of blockers. |
| GAP-002 | CRITICAL | `Request.Complete(...)` does not enforce that all blockers must be cleared before completing a request. |
| GAP-003 | CRITICAL | `Request.StartWork(...)` allows resuming work from `PAUSED` even when active blockers remain unresolved. |
| GAP-004 | CRITICAL | Database schema lacks a `request.RequestBlockers` table, and repository lacks hydration and persistence mapping for blockers. |
| GAP-005 | MAJOR | `IRequestService`, `RequestService`, and `RequestsController` lack API endpoints and commands to add a blocker and clear a blocker. |
| GAP-006 | MAJOR | `RequestDto` does not expose blockers or active blocker counts/status, preventing API clients from rendering blocker information. |
| GAP-007 | MAJOR | `RequestDetailView.vue` (`SCR-REQ-003`) lacks a dedicated Blockers UI panel to add blockers, clear active blockers with clearance notes, and review historical blockers. |
| GAP-008 | MINOR | `MyRequestsView.vue` (`SCR-REQ-004`) and `WorkPackageView.vue` (`SCR-WP-001`) do not surface structured active blocker details or explicit blocked badges. |

---

# 4. Open Questions

| ID | Question | Impact | Status |
|------|------|------|------|
| OQ-001 | How should adding an active blocker affect the Request's lifecycle status? | State machine transitions and progress visibility. | CLOSED |
| OQ-002 | When all active blockers are cleared, how should resuming work be handled? | Resumption control and owner workflow. | CLOSED |
| OQ-003 | What details should be captured when adding and clearing a blocker? | Blocker entity schema and audit data. | CLOSED |
| OQ-004 | Who should be permitted to add and clear blockers on a request? | Authorization and permission logic. | CLOSED |
| OQ-005 | In which request states can a blocker be added? | State transition validation rules. | CLOSED |
| OQ-006 | How should blockers and their history be presented in the Request Detail screen? | UI card layout and interaction design. | CLOSED |
| OQ-007 | Should list and card views display a distinct active blocker indicator? | Operational scanning on summary screens. | CLOSED |
| OQ-008 | Can blockers be edited or deleted if entered in error, or are they strictly append-and-clear? | Audit trail integrity and data model. | CLOSED |
| OQ-009 | Should cancelling a request be allowed while active blockers are still open? | Terminal state transition rules. | CLOSED |

---

# 5. Assumptions

| ID | Assumption |
|------|------|
| ASM-001 | Storing blockers in a dedicated child table `request.RequestBlockers` with foreign key to `request.Requests(Id)` provides optimal relational integrity and query performance. |
| ASM-002 | Adding a blocker to an `ASSIGNED` or `PAUSED` request sets its status to `PAUSED` without re-transitioning, while transitioning `IN_PROGRESS -> PAUSED`. |
| ASM-003 | Preserving open blockers when a request is cancelled maintains accurate historical context without requiring forced clearance. |
| ASM-004 | Work Package telemetry (`hasActiveBlockers`) will seamlessly benefit from structured active blocker counts on requests. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|------|------|------|------|
| RISK-001 | Invariant enforcement in `Complete` and `StartWork` could break tests if not accounted for. | Medium | Add comprehensive unit tests covering all blocker lifecycle paths and ensure existing tests maintain clean state. |
| RISK-002 | Concurrent blocker additions or clearances could create race conditions. | Low | Aggregate root transactional persistence and explicit optimistic checks ensure consistent state. |
| RISK-003 | UI clutter on `SCR-REQ-003` from long blocker histories. | Low | Separate active blockers into actionable rows and collapse or paginate cleared blockers in a historical list. |

---

# 7. Recommendations

## Option A (Recommended): First-Class Entity within Aggregate Root & Relational Storage

Model `RequestBlocker` as an entity inside `Cakra.Modules.Request.Domain`, persisted to a dedicated table `request.RequestBlockers`. Enforce all invariants (`Complete`, `StartWork`, `PauseWork`) strictly within the `Request` aggregate root. Emit `RequestBlockerAdded` and `RequestBlockerCleared` domain events. Add `POST /api/v1/requests/{id}/blockers` and `POST /api/v1/requests/{id}/blockers/{blockerId}/clear` endpoints.

### Advantages

- Full DDD aggregate integrity: state transitions, invariants, and events are encapsulated.
- Complete relational queryability and auditability for telemetry, reporting, and history.
- Seamless alignment with existing `RequestSubTask` and `RequestAssignment` architecture patterns.

### Disadvantages

- Requires database migration and Dapper query hydration updates.

## Option B: Ad-Hoc JSON Column on Requests Table

Serialize blockers as a JSON string array within the existing `request.Requests` table without creating a child table.

### Advantages

- Fewer database schema changes.

### Disadvantages

- Violates relational queryability and schema standards established across Cakra modules.
- Poor auditability and cumbersome indexing for active blocker telemetry.

---

# 8. Gap Closure

## GAP-001 / OQ-003: Blocker Entity Structure

### Decision

Create `RequestBlocker` as a child entity in `Cakra.Modules.Request.Domain` containing `Id`, `RequestId`, `Reason`, `CreatedByPersonId`, `CreatedAtUtc`, `IsCleared`, `ClearedAtUtc`, `ClearedByPersonId`, and `ClearanceReason`. Maintain `_blockers` private collection on `Request` exposed via `IReadOnlyCollection<RequestBlocker> Blockers`.

### Rationale

Encapsulates blocker lifecycle within the aggregate boundary and preserves full auditability.

### Impact

Requires domain entity, aggregate root collection, and DTO projection.

### Architecture Impact

Requires architectural definition of `RequestBlocker` entity and aggregate methods.

### Resolved By

User intake interview via `/grill-me`.

### Resolved Date

2026-10-15

---

## GAP-002 / GAP-003 / OQ-001 / OQ-002: State Invariants & Work Resumption

### Decision

1. Adding a blocker to an `IN_PROGRESS` or `ASSIGNED` request transitions it to `PAUSED`.
2. Calling `Complete(...)` throws `RequestHasActiveBlockersException` if `_blockers.Any(b => !b.IsCleared)`.
3. Calling `StartWork(...)` throws `RequestHasActiveBlockersException` if `_blockers.Any(b => !b.IsCleared)`.
4. Clearing all blockers leaves the request in `PAUSED` state until the owner explicitly invokes `StartWork`.

### Rationale

Guarantees operational integrity by preventing premature completion or unauthorized resumption while impediments persist.

### Impact

Updates `Request.AddBlocker`, `Request.ClearBlocker`, `Request.StartWork`, and `Request.Complete`.

### Architecture Impact

Target architecture must specify invariant rules and exception types.

### Resolved By

User intake interview via `/grill-me`.

### Resolved Date

2026-10-15

---

## GAP-004 / GAP-005: Persistence & API Contracts

### Decision

1. Create migration for table `request.RequestBlockers`.
2. Update `RequestRepository` Dapper hydration to load blockers alongside subtasks and assignments.
3. Expose `POST /api/v1/requests/{id}/blockers` and `POST /api/v1/requests/{id}/blockers/{blockerId}/clear` in `RequestsController`.

### Rationale

Provides authoritative persistence and RESTful operational endpoints.

### Impact

Updates `Cakra.Modules.Request` repository, service, and controller.

### Architecture Impact

Architect must define SQL schema and API contract specifications.

### Resolved By

User intake interview via `/grill-me`.

### Resolved Date

2026-10-15

---

## GAP-007 / GAP-008 / OQ-006 / OQ-007: UI Components & Visual Indicators

### Decision

1. On `SCR-REQ-003` (`RequestDetailView.vue`), introduce a dedicated **Blockers** card rendering active blockers, an **Add Blocker** modal/button, a **Clear Blocker** action with clearance note input, and a historical list of cleared blockers.
2. On `SCR-REQ-004` (`MyRequestsView.vue`) and `SCR-WP-001` (`WorkPackageView.vue`), display a distinct `[Blocked]` badge and active blocker note when active blockers exist.

### Rationale

Ensures immediate visual awareness of obstacles across both detail and personal workspace summary screens.

### Impact

Updates `RequestDetailView.vue`, `MyRequestsView.vue`, and `WorkPackageView.vue`.

### Architecture Impact

Requires UI component layout specifications in target architecture.

### Resolved By

User intake interview via `/grill-me`.

### Resolved Date

2026-10-15

---

## OQ-004 / OQ-005 / OQ-008 / OQ-009: Authorization, Immutability & Cancellation

### Decision

1. Any authenticated collaborator can add a blocker on open requests (`ASSIGNED`, `IN_PROGRESS`, `PAUSED`).
2. Only the assigned Request Owner, Administrator, or Manager can clear an active blocker.
3. Blockers are strictly append-and-clear (no edits or deletions).
4. Requests with open blockers may still be cancelled (`CANCELLED`), retaining all open blockers in history.

### Rationale

Empowers any team member to flag blockers while maintaining strict ownership over clearance and preserving complete historical audit integrity.

### Impact

Enforced in domain methods and service authorization checks.

### Architecture Impact

Included in security and domain invariant specifications.

### Resolved By

User intake interview via `/grill-me`.

### Resolved Date

2026-10-15

---

# 9. Architecture Applicability

## Decision

ARCHITECTURE-REQUIRED

## Rationale

This change introduces:
1. A new domain entity (`RequestBlocker`) and aggregate root invariants.
2. A new database table (`request.RequestBlockers`) and migration.
3. New domain events (`RequestBlockerAdded`, `RequestBlockerCleared`).
4. New REST API endpoints and command contracts.
5. Cross-cutting UI enhancements across `SCR-REQ-003`, `SCR-REQ-004`, and `SCR-WP-001`.

A formal ARCHITECTURE artifact is required to define technical design, schema, and API contracts prior to planning.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved
- [x] All required decisions recorded
- [x] All blocking open questions resolved
- [x] Architecture can be finalized or updated (Granted by ica-architect)

## Status

READY-FOR-PLANNING

## Notes

All feasibility gaps, open questions, invariants, and design options have been fully analyzed and closed based on the intake interview. READY-FOR-PLANNING gate has been granted by ica-architect. Target architecture can now be authored.

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-032-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-032-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- FEATURE: [FEAT-REQ-009-manage-request-blockers.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-009-manage-request-blockers.md)
- FEATURE: [FEAT-REQ-008-review-request-completion.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-008-review-request-completion.md)
- REFERENCE ARCHITECTURE: [CR-016-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-016-ARCHITECTURE.md)

Referenced codebase locations:

- [Request.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs)
- [RequestStatus.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/RequestStatus.cs)
- [RequestService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs)
- [RequestsController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/RequestsController.cs)
- [RequestDetailView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue)
- [MyRequestsView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/MyRequestsView.vue)
- [WorkPackageView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue)
