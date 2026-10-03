---
Title: Feasibility Assessment for Request Sub-Tasks Breakdown and Completion Progress Tracking (CR-006)
Code: CR-006
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-03
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Change being assessed: Introduction of **Sub-Tasks** breakdown and **Sub-Task-Driven Completion Percentage** for operational requests in the Request module (`Cakra.Modules.Request`), as requested in [CR-006-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-006-ISSUE.md) and specified in the aligned brainstorming specification [request_subtasks_brainstorming_specification.md](file:///C:/Users/drury/.gemini/antigravity/brain/5878bc7a-289d-4fc8-89b7-77673ed6d3a2/request_subtasks_brainstorming_specification.md).

Referenced artifacts:

- ISSUE: [CR-006-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-006-ISSUE.md)
- BRAINSTORMING SPEC: [request_subtasks_brainstorming_specification.md](file:///C:/Users/drury/.gemini/antigravity/brain/5878bc7a-289d-4fc8-89b7-77673ed6d3a2/request_subtasks_brainstorming_specification.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md), [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md)
- FEATURES: [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md), [FEAT-REQ-008-review-request-completion.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-008-review-request-completion.md), [FEAT-COL-004-review-assigned-requests.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-COL-004-review-assigned-requests.md)

## Objective

Assess the feasibility, domain model impact, persistence structure, lifecycle rules, authorization policies, command workflows, query models, and planning readiness to:

1. Support lightweight child sub-tasks (`RequestSubTask`) encapsulated within the authoritative `Request` aggregate root.
2. Enforce a strict domain blocking invariant preventing a Request from transitioning to `Completed` while incomplete sub-tasks exist.
3. Provide a simple binary lifecycle (`Pending` / `Completed`) with reopening and deletion capabilities during active lifecycle states (`Captured`, `Evaluating`, `Accepted`, `InProgress`, `Escalated`).
4. Enforce strict immutability of sub-tasks once a Request reaches a closed state (`Completed` or `Rejected`).
5. Dynamically calculate completion percentage (`(Completed / Total) * 100` or `100%` when completed with 0 sub-tasks) and persist `TotalSubTasksCount`, `CompletedSubTasksCount`, and `CompletionPercentage` directly on `[request].[Requests]`.
6. Provide role- and assignment-based authorization (Owner/Admin for sub-task management; Assignee/Owner/Admin for completion and reopening).
7. Dispatch domain events (`RequestSubTaskAdded`, `RequestSubTaskCompleted`, `RequestSubTaskReopened`, `RequestSubTaskRemoved`) to audit sub-task actions and support operational feed integration.
8. Support optional initial sub-tasks during request intake (`RecordRequestCommand`) and provide dedicated application commands for post-creation mutations.
9. Expose sub-tasks and progress metrics in `RequestDto` and surface assigned sub-tasks in personal queues (`MyRequestsView.vue`).
10. Deliver an interactive checklist card and progress bar visualization (`60% (3/5)`) in the frontend UI (`Cakra.Web`).

---

# 2. Current State

## Existing Behavior

1. **Domain Model (`Cakra.Modules.Request.Domain.Request`)**:
   - The [`Request`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs) aggregate root manages operational properties (`Title`, `Description`, `RequestType`, `Status`, `Priority`, `Complexity`, `OwnerPersonId`, `Resolution`), but has no collection, entity, or methods for decomposing the request into smaller tasks.
   - Lifecycle completion via [`Request.Complete`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs#L350-L380) checks only whether the request is already closed or missing resolution details; it contains no verification of whether underlying work items or sub-tasks are unfinished.
   - Work assignment is coarse-grained at the aggregate level (`OwnerPersonId`); there is no mechanism to delegate discrete tasks to multiple team members.
   - No progress metrics, sub-task counts, or completion percentages are tracked or calculated.

2. **Application Commands & Handlers (`RequestCommands.cs`, `RequestService.cs`)**:
   - [`RecordRequestCommand`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs#L9-L18) captures title, description, customer ID, product ID, request type, priority, actor ID, work package ID, and complexity, but cannot accept initial sub-tasks.
   - No commands exist for adding, completing, reopening, or removing sub-tasks.
   - [`CompleteRequestCommand`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs#L145-L155) delegates directly to `Request.Complete` without checking sub-task statuses.

3. **Persistence & Data Schema (`RequestRepository.cs`)**:
   - Table `[request].[Requests]` contains columns for top-level operational attributes, but lacks `[TotalSubTasksCount]`, `[CompletedSubTasksCount]`, and `[CompletionPercentage]`.
   - No relational table exists for child sub-tasks (e.g., `[request].[RequestSubTasks]`).
   - [`RequestRepository.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Persistence/RequestRepository.cs) only queries and persists `[request].[Requests]`, `[request].[RequestResolutions]`, and `[request].[RequestAssignments]`.

4. **Authorization & Security**:
   - Role verification is available via `IOrganizationQueryService` and `IAuthorizationService`, but currently only guards complexity adjustments (`ValidateComplexityAuthorizationAsync`).
   - There are no permissions or policies evaluating whether an actor owns a request, manages it as an administrator, or is the assigned collaborator on a sub-task.

5. **Read Projections & Personal Workspaces**:
   - [`RequestDto.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Models/RequestDto.cs) lacks sub-task collections and progress counters.
   - Query endpoints do not return sub-task items.
   - [`MyRequestsView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/MyRequestsView.vue) only filters requests by `OwnerPersonId`, meaning team members assigned individual tasks cannot see their assigned responsibilities in their personal queue.

6. **Frontend User Interface (`Cakra.Web`)**:
   - [`RequestDetailView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue) contains no checklist component or task controls.
   - [`RequestListView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestListView.vue) contains no progress column or visual completion indicators.
   - [`CreateRequestView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/CreateRequestView.vue) has no input controls for supplying initial sub-tasks.

## Existing Constraints

1. **Aggregate Boundary Integrity**: All child sub-task mutations must route through the `Request` aggregate root; direct external repository manipulation of child records is forbidden.
2. **State Machine Invariants**: Requests in terminal states (`Completed` or `Rejected`) are immutable historical records; sub-tasks cannot be created, modified, completed, reopened, or deleted on closed requests.
3. **Completion Invariant**: A Request cannot transition to `Completed` if any sub-task remains incomplete (`IsCompleted == false`).
4. **Relational Integrity**: Deleting a Request must cascade-delete all associated sub-tasks (`ON DELETE CASCADE`).
5. **No Cross-Schema Direct Writes**: All persistence changes remain within the `[request]` schema.
6. **Backward Compatibility**: Existing database records must be cleanly backfilled (`TotalSubTasksCount = 0`, `CompletedSubTasksCount = 0`, and `CompletionPercentage = 100` if `Completed`, else `0`).

---

# 3. Gap Analysis

| ID | Severity | Gap |
|---|---|---|
| GAP-001 | CRITICAL | `Request` aggregate root lacks child entity collection `_subTasks`, domain invariants, methods (`AddSubTask`, `CompleteSubTask`, `ReopenSubTask`, `RemoveSubTask`), and blocking check in `Complete(...)`. |
| GAP-002 | CRITICAL | Database schema `[request]` lacks table `[request].[RequestSubTasks]` and columns on `[request].[Requests]` (`TotalSubTasksCount`, `CompletedSubTasksCount`, `CompletionPercentage`), plus foreign key cascade and indexes. |
| GAP-003 | MAJOR | Application layer lacks commands `AddRequestSubTaskCommand`, `CompleteRequestSubTaskCommand`, `ReopenRequestSubTaskCommand`, `RemoveRequestSubTaskCommand`, and optional initial sub-task list in `RecordRequestCommand`. |
| GAP-004 | MAJOR | Domain events `RequestSubTaskAdded`, `RequestSubTaskCompleted`, `RequestSubTaskReopened`, `RequestSubTaskRemoved` do not exist. |
| GAP-005 | MAJOR | `RequestService` lacks authorization checks for sub-task management (Request Owner, Admin, Team Lead, Manager) and sub-task completion (Sub-Task Assignee, Request Owner, Management). |
| GAP-006 | MAJOR | Completion percentage calculation, dynamic recalculation, and database persistence synchronization logic are missing. |
| GAP-007 | MAJOR | `RequestDto` and query projections do not include `SubTasks`, `TotalSubTasksCount`, `CompletedSubTasksCount`, `CompletionPercentage`, and lack personal queue query filtering by assigned sub-tasks. |
| GAP-008 | MAJOR | Frontend UI lacks checklist card on `RequestDetailView.vue`, progress bar on `RequestListView.vue`, sub-task input on `CreateRequestView.vue`, and assigned sub-tasks section on `MyRequestsView.vue`. |
| GAP-009 | MAJOR | Domain documentation (`request-domain.md`) and Feature specifications (`FEAT-REQ-001`, `FEAT-REQ-008`, `FEAT-COL-004`) lack sub-task and completion percentage specifications. |

---

# 4. Open Questions

| ID | Question | Impact |
|---|---|---|
| OQ-001 | Can a Request be marked Completed if there are open/incomplete sub-tasks? | Domain aggregate completion invariant, validation error handling, and UI button guard. |
| OQ-002 | During which lifecycle states can sub-tasks be modified or completed? | Aggregate state validation and historical immutability rules. |
| OQ-003 | Can a completed sub-task be reopened? | Workflow flexibility, error recovery, and domain event dispatching. |
| OQ-004 | How should completion percentage behave when there are 0 sub-tasks vs when sub-tasks exist? | Mathematical formula, edge case handling, and progress visualization. |
| OQ-005 | Where should completion percentage and counters be stored? | Performance of SQL sorting/filtering, database schema, and read projections. |
| OQ-006 | Who is authorized to create, delete, and complete sub-tasks? | Security model, command authorization validation, and UI button visibility. |
| OQ-007 | How should sub-tasks be provided during initial creation? | Command contract design and intake workflow usability. |
| OQ-008 | How should sub-tasks assigned to an individual integrate with their personal workspace? | Personal queue query performance and user task visibility. |

---

# 5. Assumptions

| ID | Assumption |
|---|---|
| ASM-001 | Sub-task titles are required strings between 1 and 255 characters, with leading and trailing whitespace trimmed. |
| ASM-002 | Removing an active sub-task executes a physical deletion in `[request].[RequestSubTasks]` within the aggregate transaction, and immediately triggers progress recalculation. |
| ASM-003 | Existing historical records in `[request].[Requests]` are backfilled during schema migration: Requests in `Completed` status receive `CompletionPercentage = 100`, while all others receive `0`. |
| ASM-004 | Sub-task ordering is maintained by a zero-based integer `SortOrder` column incremented sequentially upon addition. |
| ASM-005 | Personal queue views will surface assigned sub-tasks with links back to the parent Request details. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| RISK-001 | Concurrency conflicts when multiple collaborators toggle different sub-tasks on the same Request simultaneously. | One update overwrites another or throws optimistic concurrency exception. | Encapsulate sub-task updates within atomic aggregate repository transactions and leverage row versioning or optimistic lock checks. |
| RISK-002 | Performance degradation when loading request lists with multiple sub-tasks. | N+1 query overhead or excessive payload size on list endpoints. | Persist `TotalSubTasksCount`, `CompletedSubTasksCount`, and `CompletionPercentage` directly on `[request].[Requests]` so list queries require no joins. |
| RISK-003 | Premature completion attempt by users unaware of pending sub-tasks. | Frustration from unexpected command validation rejections. | Disable the "Complete Request" action button in the UI and display an explicit badge/alert indicating remaining open sub-tasks. |
| RISK-004 | Inadvertent loss of task history when sub-tasks are deleted. | Audit gap for removed work items. | Emit `RequestSubTaskRemoved` domain event capturing the deleted task title and actor ID before physical removal. |

---

# 7. Recommendations

## Option A: Encapsulated Child Entity with Persisted Summary Metrics (Recommended)

Model `RequestSubTask` as a child entity inside the `Request` aggregate root. Maintain `TotalSubTasksCount`, `CompletedSubTasksCount`, and `CompletionPercentage` directly as columns on `[request].[Requests]`. Persist child rows in a normalized table `[request].[RequestSubTasks]` with foreign key cascade delete.

### Advantages

- Full DDD encapsulation: `Request` aggregate strictly enforces all invariants, including the blocking completion guard.
- Ultra-fast list queries: Sorting, filtering, and pagination on `CompletionPercentage` require zero table joins or dynamic aggregation.
- Clean audit trail: Fine-grained domain events dispatched directly from aggregate lifecycle methods.
- Simple transactional boundary: Modifying a sub-task and updating the parent progress occurs within a single SQL transaction.

### Disadvantages

- Requires synchronizing aggregate summary columns whenever sub-tasks are modified (handled cleanly by aggregate domain methods).

## Option B: Standalone Task Aggregate with Eventual Consistency

Model tasks as independent aggregate roots in a separate module with loose reference IDs to Requests.

### Advantages

- Decouples task lifecycle from the request module.

### Disadvantages

- Severe complexity: Enforcing the blocking completion invariant requires distributed locking or eventual consistency sagas.
- Slower list queries: Aggregating progress requires cross-aggregate queries or projection tables.
- Over-engineering for what is fundamentally a request work breakdown checklist.

---

# 8. Gap Closure

## GAP-001

### Status

CLOSED

### Decision

Add child entity `RequestSubTask` inside `Request` aggregate root. Add methods `AddSubTask`, `CompleteSubTask`, `ReopenSubTask`, and `RemoveSubTask`. Modify `Complete` method to verify that no sub-tasks remain with `IsCompleted == false`, throwing `RequestHasUnfinishedSubTasksException` if violated.

### Rationale

Encapsulates business rules within the domain boundary and enforces the hard completion prerequisite at the aggregate level.

### Impact

Directly prevents premature request completion in domain logic.

### Architecture Impact

Requires updates to `Request.cs`, new entity `RequestSubTask.cs`, and custom exception `RequestHasUnfinishedSubTasksException.cs`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## GAP-002

### Status

CLOSED

### Decision

Create normalized table `[request].[RequestSubTasks]` with foreign key referencing `[request].[Requests]` (`ON DELETE CASCADE`). Add `TotalSubTasksCount`, `CompletedSubTasksCount`, and `CompletionPercentage` columns to `[request].[Requests]`. Map child records in `RequestRepository.cs` via Dapper multi-mapping or sequential queries within transaction.

### Rationale

Follows Cakra persistence standards and architectural conventions, providing high-performance query capabilities.

### Impact

Database schema extended; historical records backfilled cleanly.

### Architecture Impact

Requires SQL migration script and updates to `RequestRepository.cs`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## GAP-003

### Status

CLOSED

### Decision

Implement `AddRequestSubTaskCommand`, `CompleteRequestSubTaskCommand`, `ReopenRequestSubTaskCommand`, and `RemoveRequestSubTaskCommand`. Extend `RecordRequestCommand` to accept an optional `IReadOnlyList<InitialSubTaskDto>? InitialSubTasks`. Add FluentValidation validators for all commands.

### Rationale

Provides clear, task-oriented CQRS command interfaces for both initial intake and incremental task execution.

### Impact

Application layer expanded to support full sub-task lifecycle.

### Architecture Impact

Requires new command records and validator classes in `RequestCommands.cs` and handlers in `RequestService.cs`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## GAP-004

### Status

CLOSED

### Decision

Create domain events `RequestSubTaskAdded`, `RequestSubTaskCompleted`, `RequestSubTaskReopened`, and `RequestSubTaskRemoved`. Dispatch them from the aggregate root upon state changes.

### Rationale

Maintains complete traceability, feeds operational activity streams, and enables cross-module awareness.

### Impact

Auditing and event subscribers receive real-time notifications of task progress.

### Architecture Impact

Requires new event classes in `Cakra.Modules.Request.Domain.Events`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## GAP-005

### Status

CLOSED

### Decision

Enforce authorization in `RequestService`:
- Managing sub-tasks (add, remove, reassign): Allowed for Request Owner, Administrator, Team Lead, and Manager.
- Completing or reopening sub-tasks: Allowed for the specific sub-task Assignee, Request Owner, Administrator, Team Lead, and Manager.

### Rationale

Provides the necessary operational flexibility for assigned contributors to complete their own work while maintaining owner and manager oversight.

### Impact

Prevents unauthorized users from tampering with tasks while enabling seamless team collaboration.

### Architecture Impact

Requires authorization checks in `RequestService.cs` using `IOrganizationQueryService` and `IAuthorizationService`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## GAP-006

### Status

CLOSED

### Decision

Encapsulate progress calculation within `Request` aggregate:
- If `TotalSubTasksCount > 0`: `CompletionPercentage = (int)Math.Round((double)CompletedSubTasksCount / TotalSubTasksCount * 100.0)`.
- If `TotalSubTasksCount == 0`: `100` if `Status == Completed`, else `0`.
Recalculate automatically whenever sub-tasks are added, removed, completed, or reopened.

### Rationale

Ensures consistent calculation rules across all operations without business logic leaking into presentation layers.

### Impact

Guarantees 100% data integrity between sub-task states and parent progress columns.

### Architecture Impact

Implemented as a private helper method `RecalculateProgress()` in `Request.cs`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## GAP-007

### Status

CLOSED

### Decision

Update `RequestDto` to include `TotalSubTasksCount`, `CompletedSubTasksCount`, `CompletionPercentage`, and `IReadOnlyList<RequestSubTaskDto> SubTasks`. Add endpoint or query filter in `RequestQueryService` to retrieve requests with sub-tasks assigned to a specific `PersonId`.

### Rationale

Enables both high-level grid rendering and detailed checklist views, while supporting personal workload views.

### Impact

API contracts enriched; clients receive structured task information.

### Architecture Impact

Requires updates to `RequestDto.cs`, new `RequestSubTaskDto.cs`, and query extensions in `RequestQueryService.cs`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## GAP-008

### Status

CLOSED

### Decision

Update frontend views:
- `RequestDetailView.vue`: Interactive checklist card with one-click check/uncheck toggle, inline task input with assignee picker, delete button, and progress bar `60% (3/5)`.
- `RequestListView.vue`: Progress column with compact progress bar and fraction text.
- `CreateRequestView.vue`: Dynamic checklist input allowing initial sub-tasks.
- `MyRequestsView.vue`: "Assigned Sub-Tasks" section displaying tasks assigned to the current user with direct toggle capability.

### Rationale

Delivers an intuitive, responsive user experience adhering to Cakra UI conventions.

### Impact

Users gain full visibility and control over sub-tasks and operational progress.

### Architecture Impact

Vue 3 component updates in `cakra/src/frontend/Cakra.Web/src/views/`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## GAP-009

### Status

CLOSED

### Decision

Update `request-domain.md`, `FEAT-REQ-001-record-customer-request.md`, `FEAT-REQ-008-review-request-completion.md`, and `FEAT-COL-004-review-assigned-requests.md` to define sub-task concepts, lifecycle rules, completion blocking invariant, and progress metrics.

### Rationale

Synchronizes permanent and working business knowledge per SDLC standards.

### Impact

Documentation reflects accurate domain model and feature specifications.

### Architecture Impact

Analyst artifact updates in `cakra/docs/domains/` and `cakra/docs/features/`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## OQ-001

### Status

CLOSED

### Decision

Strictly reject completion: A Request cannot be marked `Completed` while any sub-tasks remain incomplete. The domain method `Complete` will throw `RequestHasUnfinishedSubTasksException`, and the UI will disable the completion action.

### Rationale

Guarantees operational integrity and ensures work items are not forgotten or prematurely closed.

### Impact

Enforces completion discipline.

### Architecture Impact

Domain rule in `Request.cs`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## OQ-002

### Status

CLOSED

### Decision

Sub-tasks can be added, updated, removed, completed, or reopened only during active states (`Captured`, `Evaluating`, `Accepted`, `InProgress`, `Escalated`). Once in `Completed` or `Rejected`, all sub-tasks are immutable.

### Rationale

Preserves historical state integrity for closed requests.

### Impact

Closed requests are strictly read-only.

### Architecture Impact

State validation guards in `Request` aggregate methods.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## OQ-003

### Status

CLOSED

### Decision

Allow reopening a completed sub-task back to `Pending` at any time while the parent Request is active.

### Rationale

Supports real-world rework, test failure cycles, and mistake recovery.

### Impact

Provides operational flexibility.

### Architecture Impact

Aggregate method `ReopenSubTask` and event `RequestSubTaskReopened`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## OQ-004

### Status

CLOSED

### Decision

Calculate as `(CompletedSubTasksCount / TotalSubTasksCount) * 100` when sub-tasks exist. If no sub-tasks exist, progress is 100% if `Status == Completed`, and 0% otherwise.

### Rationale

Provides clear, predictable progress metrics for both sub-task-decomposed requests and simple single-step requests.

### Impact

Consistent percentage across all operational views.

### Architecture Impact

Progress calculation method in `Request.cs`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## OQ-005

### Status

CLOSED

### Decision

Store `TotalSubTasksCount`, `CompletedSubTasksCount`, and `CompletionPercentage` directly as columns on `[request].[Requests]`, updated by the aggregate root.

### Rationale

Maximizes query performance for grid sorting and filtering without expensive dynamic subqueries or joins.

### Impact

Fast database performance and clean query models.

### Architecture Impact

DDL migration on `[request].[Requests]` and repository mapping.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## OQ-006

### Status

CLOSED

### Decision

Request Owners and Admins/Managers can manage all sub-tasks (add, delete, reassign). Sub-task Assignees can mark their own assigned tasks complete or reopen them.

### Rationale

Balances operational governance with day-to-day execution autonomy for individual team members.

### Impact

Empowers task assignees while preserving owner authority.

### Architecture Impact

Authorization logic in `RequestService.cs`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## OQ-007

### Status

CLOSED

### Decision

Support both: allow an optional initial sub-task list in `RecordRequestCommand`, and provide dedicated commands for adding, removing, completing, and reopening sub-tasks post-creation.

### Rationale

Accommodates upfront task breakdown during intake as well as emergent task discovery during execution.

### Impact

Flexible intake and execution workflows.

### Architecture Impact

Command definitions in `RequestCommands.cs`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## OQ-008

### Status

CLOSED

### Decision

Surface assigned sub-tasks in personal workspace views (`MyRequestsView.vue`) under an "Assigned Tasks" section, linking directly to the parent Request details.

### Rationale

Ensures team members have clear visibility into all their immediate action items without needing to own the entire Request.

### Impact

Improves individual task management and collaboration.

### Architecture Impact

Query enhancement in `RequestQueryService.cs` and UI component in `MyRequestsView.vue`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

# 9. Architecture Applicability

Record the Architecture Applicability decision.

## Decision

ARCHITECTURE-REQUIRED

## Rationale

Introducing Request Sub-Tasks and Sub-Task-Driven Completion Percentage introduces significant technical and architectural changes:
1. New child entity `RequestSubTask` and aggregate root modifications to `Request.cs`.
2. New normalized database table `[request].[RequestSubTasks]` with cascading foreign keys and indexes.
3. Schema alterations to `[request].[Requests]` (`TotalSubTasksCount`, `CompletedSubTasksCount`, `CompletionPercentage`).
4. Multiple new CQRS commands (`AddRequestSubTaskCommand`, `CompleteRequestSubTaskCommand`, etc.) and validation rules.
5. New domain events (`RequestSubTaskAdded`, `RequestSubTaskCompleted`, etc.) and operational feed integration.
6. Role- and assignee-based authorization checks in `RequestService.cs`.
7. Substantial frontend component additions in `Cakra.Web` (`RequestDetailView.vue`, `RequestListView.vue`, `MyRequestsView.vue`).

A formal target architecture artifact (`CR-006-ARCHITECTURE.md`) and implementation plan (`CR-006-IMPLEMENTATION-PLAN.md`) are required to define the technical realization, migration strategy, and execution slices.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved
- [x] All required decisions recorded
- [x] All blocking open questions resolved
- [x] Architecture can be finalized or updated

## Status

READY-FOR-PLANNING

## Notes

All feasibility analysis, gap definitions, open questions, and closure decisions have been thoroughly analyzed and verified. In accordance with SDLC governance rules, the `READY-FOR-PLANNING` gate is evaluated and granted by `ica-architect`. Technical realization will be defined in target architecture artifact `CR-006-ARCHITECTURE.md`.

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-006-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-006-ISSUE.md)
- BRAINSTORMING SPEC: [request_subtasks_brainstorming_specification.md](file:///C:/Users/drury/.gemini/antigravity/brain/5878bc7a-289d-4fc8-89b7-77673ed6d3a2/request_subtasks_brainstorming_specification.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- DOMAIN: [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md)
- FEATURE: [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md)
- FEATURE: [FEAT-REQ-008-review-request-completion.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-008-review-request-completion.md)
- FEATURE: [FEAT-COL-004-review-assigned-requests.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-COL-004-review-assigned-requests.md)

Referenced codebase locations:

- [Request.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs)
- [RequestStatus.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/RequestStatus.cs)
- [RequestCommands.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs)
- [RequestService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs)
- [RequestDto.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Models/RequestDto.cs)
- [RequestRepository.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Persistence/RequestRepository.cs)
- [RequestQueryService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestQueryService.cs)
- [IAuthorizationService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Services/IAuthorizationService.cs)
- [RequestDetailView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue)
- [RequestListView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestListView.vue)
- [MyRequestsView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/MyRequestsView.vue)
