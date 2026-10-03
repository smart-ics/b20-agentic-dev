---
Title: Implementation Plan for Request Sub-Tasks Breakdown and Completion Progress Tracking (CR-006)
Code: CR-006
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-03
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-006`: **Request Sub-Tasks Breakdown and Completion Progress Tracking**. Establish an encapsulated child entity collection (`RequestSubTask`) within the `Request` aggregate root, persist sub-tasks in a normalized table `[request].[RequestSubTasks]` with cascading referential integrity, store summary counters and completion percentage directly on `[request].[Requests]`, enforce a strict blocking domain invariant that prevents completing a Request while incomplete sub-tasks exist, implement role- and assignee-based authorization, publish domain events for change auditing, introduce CQRS application commands, extend query projections, and provide an interactive checklist UI with progress visualization across `Cakra.Web`.

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- FEATURE: [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md), [FEAT-REQ-008-review-request-completion.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-008-review-request-completion.md), [FEAT-COL-004-review-assigned-requests.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-COL-004-review-assigned-requests.md)
- ARCHITECTURE: [CR-006-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-006-ARCHITECTURE.md) (authoritative capability architecture), [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- FEASIBILITY-ASSESSMENT: [CR-006-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-006-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

Architecture Applicability: ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers the complete realization across persistence, domain modeling, application services, API contracts, frontend interfaces, and test suites:

1. **Persistence & Migration (`Cakra.Api`, `Cakra.Modules.Request.Persistence`)**:
   - Idempotent migration script `0012_request_subtasks.sql` creating table `[request].[RequestSubTasks]` and adding columns `[TotalSubTasksCount]`, `[CompletedSubTasksCount]`, and `[CompletionPercentage]` to `[request].[Requests]`.
   - Update SQL `SELECT`, `INSERT`, `UPDATE`, and `DELETE` mapping in `RequestRepository.cs` and `RequestQueryService.cs` to persist and load sub-tasks and summary metrics within atomic transactions.

2. **Domain Modeling & Invariants (`Cakra.Modules.Request.Domain`)**:
   - Create entity `RequestSubTask.cs`.
   - Encapsulate child sub-tasks `_subTasks` within aggregate root `Request.cs`.
   - Implement `AddSubTask`, `CompleteSubTask`, `ReopenSubTask`, `RemoveSubTask`, and `RecalculateProgress`.
   - Enforce completion prerequisite in `Request.Complete(...)` throwing `RequestHasUnfinishedSubTasksException`.
   - Enforce state machine immutability on closed requests (`Completed`, `Rejected`).
   - Create domain events `RequestSubTaskAdded`, `RequestSubTaskCompleted`, `RequestSubTaskReopened`, and `RequestSubTaskRemoved`.
   - Add domain unit tests in `Cakra.Tests.Unit/Request`.

3. **Application Services, Commands & Authorization (`Cakra.Modules.Request.Services`)**:
   - Create commands `AddRequestSubTaskCommand`, `CompleteRequestSubTaskCommand`, `ReopenRequestSubTaskCommand`, `RemoveRequestSubTaskCommand`, and respective FluentValidation validators.
   - Extend `RecordRequestCommand` with optional initial sub-task list.
   - Implement handlers in `RequestService.cs` with dual-tier authorization (owner/management for managing; assignee/owner/management for completing/reopening).
   - Create `RequestSubTaskDto` and extend `RequestDto`.
   - Extend `RequestQueryService.cs` with personal assigned sub-task query.
   - Add service unit tests in `Cakra.Tests.Unit/Request`.

4. **API Gateway & Controller (`Cakra.Api.Controllers`)**:
   - Implement sub-task management endpoints in `RequestsController.cs`:
     - `POST /api/v1/requests/{id}/subtasks`
     - `POST /api/v1/requests/{id}/subtasks/{subTaskId}/complete`
     - `POST /api/v1/requests/{id}/subtasks/{subTaskId}/reopen`
     - `DELETE /api/v1/requests/{id}/subtasks/{subTaskId}`
   - Update `POST /api/v1/requests` body binding for initial sub-tasks.
   - Add controller integration tests in `Cakra.Tests.Integration/Request`.

5. **Frontend Web UI & Verification (`Cakra.Web`)**:
   - Interactive checklist card and progress header (`60% (3/5)`) in `RequestDetailView.vue`.
   - Guard completion button when incomplete sub-tasks exist.
   - Progress column in `RequestListView.vue`.
   - Initial sub-tasks input in `CreateRequestView.vue`.
   - Assigned sub-tasks queue in `MyRequestsView.vue`.
   - Frontend typecheck and build validation (`npm run type-check`, `npm run build`).

---

# 3. Dependencies

- .NET 8 SDK / C# 12
- Dapper 2.x
- MediatR & FluentValidation
- Node.js & npm (Vue 3 / Vite / TypeScript / Bootstrap 5)
- Existing SQL Server database and `MigrationRunner`

For slice dependencies:
- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires referenced slice to have implementation status `IMPLEMENTED`.
- Dependency satisfaction does not require review status `GO`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Persistence & Database Schema | IMPLEMENTED | GO | 1/1 |
| P2 - Domain Modeling & Invariants | IMPLEMENTED | GO | 1/1 |
| P3 - Application Services, Commands & Authorization | IMPLEMENTED | GO | 1/1 |
| P4 - API Gateway & HTTP Endpoints | IMPLEMENTED | GO | 1/1 |
| P5 - Frontend Web UI & Verification | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Persistence & Database Schema

Implementation Status: IMPLEMENTED  
Review Status: GO  

### P1-S01

Title: Database Migration and Repository Mapping for Request Sub-Tasks

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective:  
Create the idempotent migration script `0012_request_subtasks.sql` to define table `[request].[RequestSubTasks]` (with primary key, foreign key cascade to `[request].[Requests]`, sort order, audit timestamps, and indexes) and alter `[request].[Requests]` to add `[TotalSubTasksCount]`, `[CompletedSubTasksCount]`, and `[CompletionPercentage]`. Update `RequestRepository.cs` and `RequestQueryService.cs` to map these columns and handle CRUD queries within transactions.

Depends On: None  

Repository: `cakra`  

Completion Criteria:  
- `0012_request_subtasks.sql` created in `cakra/src/backend/Cakra.Api/Migrations/Scripts/` with idempotent `IF NOT EXISTS` guards and historical record backfill.
- `[request].[RequestSubTasks]` table created with foreign key `ON DELETE CASCADE` and indexes on `RequestId` and `AssigneePersonId`.
- `RequestRepository.cs` maps `TotalSubTasksCount`, `CompletedSubTasksCount`, and `CompletionPercentage` on `Request` aggregate root and loads child sub-tasks in `GetByIdAsync`.
- `RequestRepository.cs` persists new, updated, and deleted sub-tasks alongside parent progress columns in atomic database transactions.
- Solution builds cleanly via `dotnet build cakra/Cakra.sln`.

Implementation Notes:  
- Created idempotent migration script `0012_request_subtasks.sql` with table `[request].[RequestSubTasks]`, cascading foreign key, and indexes. Added columns `[TotalSubTasksCount]`, `[CompletedSubTasksCount]`, `[CompletionPercentage]`, and check constraint `[CK_Requests_CompletionPercentage]` with backfill.
- Created `RequestSubTask.cs` child entity in `Cakra.Modules.Request.Domain` inheriting from `EntityBase` and updated `Request.cs` aggregate root with encapsulated `_subTasks`, progress summary properties, and rehydration mapping.
- Updated `RequestRepository.cs` to map `TotalSubTasksCount`, `CompletedSubTasksCount`, `CompletionPercentage`, load sub-tasks in `GetByIdAsync`, and persist sub-tasks atomically in `AddAsync` and `UpdateAsync` within database transactions.
- Updated `RequestDto.cs` and `RequestQueryService.cs` across all request queries (`GetRequestByIdAsync`, `ListMyAssignedRequestsAsync`, `GetFilteredRequestGridAsync`, `GetRequestsByIdsAsync`) and `RequestWithResolutionRow` to project sub-task progress metrics.
- Verified compilation with 0 warnings/0 errors via `dotnet build cakra/Cakra.sln` and full unit test execution (262 passed).

Notes:  
Follows Dapper parameterized SQL conventions (Architecture §8, §10).

---

## P2 - Domain Modeling & Invariants

Implementation Status: IMPLEMENTED  
Review Status: GO  

### P2-S02

Title: Sub-Task Child Entity, Aggregate Encapsulation, Completion Guard, and Events

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective:  
Implement `RequestSubTask.cs` child entity and update `Request.cs` aggregate root to encapsulate `_subTasks`. Implement business methods (`AddSubTask`, `CompleteSubTask`, `ReopenSubTask`, `RemoveSubTask`) and progress recalculation. Implement the hard completion invariant in `Request.Complete(...)` throwing `RequestHasUnfinishedSubTasksException` if incomplete sub-tasks exist. Enforce active state mutability guards. Implement domain events (`RequestSubTaskAdded`, `RequestSubTaskCompleted`, `RequestSubTaskReopened`, `RequestSubTaskRemoved`) and unit tests.

Depends On: P1-S01  

Repository: `cakra`  

Completion Criteria:  
- Entity `RequestSubTask.cs` created with encapsulation and internal state mutation methods.
- Aggregate root `Request.cs` exposes `IReadOnlyCollection<RequestSubTask> SubTasks` and encapsulates progress calculation.
- `Request.Complete(...)` throws `RequestHasUnfinishedSubTasksException` when incomplete sub-tasks exist.
- Sub-task modifications on closed requests (`Completed`, `Rejected`) throw `InvalidRequestStateTransitionException`.
- Domain events created in `Cakra.Modules.Request.Domain.Events`.
- Comprehensive domain unit tests added in `Cakra.Tests.Unit/Request` verifying all invariants, progress calculations, and exceptions.
- All unit tests pass via `dotnet test cakra/tests/Cakra.Tests.Unit`.

Implementation Notes:  
- Created domain exceptions `RequestDomainException` and `RequestHasUnfinishedSubTasksException` in `Cakra.Modules.Request.Domain.Exceptions`.
- Enhanced `RequestSubTask.cs` child entity with domain invariants and validation rules (title 1-255 characters, non-empty IDs, internal `MarkCompleted` and `Reopen` transitions).
- Implemented aggregate business methods in `Request.cs`: `AddSubTask`, `CompleteSubTask`, `ReopenSubTask`, `RemoveSubTask`, `EnsureActiveStateForSubTaskMutation`, and encapsulated `RecalculateProgress()`.
- Implemented hard completion invariant in `Request.Complete(...)` enforcing zero incomplete sub-tasks and raising `RequestHasUnfinishedSubTasksException` (TD-002).
- Enforced active lifecycle state mutability guard (TD-003) rejecting mutations on closed requests (`Completed`, `Rejected`) with `InvalidRequestStateTransitionException`.
- Created fine-grained domain events `RequestSubTaskAdded`, `RequestSubTaskCompleted`, `RequestSubTaskReopened`, and `RequestSubTaskRemoved` in `Cakra.Modules.Request.Domain.Events` (TD-007).
- Added comprehensive unit test suite `RequestSubTaskDomainTests.cs` (23 tests) verifying domain invariants, sort order, validation, completion guards, reopening, removal, and percentage rounding.
- Verified test runs: 285 unit tests passed (0 failures) and 125 integration tests passed (0 failures).

Notes:  
Implements Architecture TD-001, TD-002, TD-003, TD-004, TD-007.

---

## P3 - Application Services, Commands & Authorization

Implementation Status: IMPLEMENTED  
Review Status: GO  

### P3-S03

Title: Application Commands, Dual-Tier Authorization, Query Projections, and DTOs

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective:  
Define MediatR commands and FluentValidation validators for sub-task management (`AddRequestSubTaskCommand`, `CompleteRequestSubTaskCommand`, `ReopenRequestSubTaskCommand`, `RemoveRequestSubTaskCommand`). Extend `RecordRequestCommand` to accept optional initial sub-tasks. Implement handler methods in `RequestService.cs` enforcing dual-tier authorization (owner/management for managing; assignee/owner/management for completing/reopening). Update `RequestDto.cs`, create `RequestSubTaskDto.cs`, and add assigned sub-task query in `RequestQueryService.cs`. Add service unit tests.

Depends On: P2-S02  

Repository: `cakra`  

Completion Criteria:  
- Command records and validators created in `RequestCommands.cs`.
- `RecordRequestCommand` accepts optional `InitialSubTasks`.
- `RequestService.cs` implements authorization checks for management and assignee roles, rejecting unauthorized calls with `UnauthorizedAccessException`.
- `RequestDto.cs` enriched with `SubTasks`, `TotalSubTasksCount`, `CompletedSubTasksCount`, and `CompletionPercentage`.
- `RequestQueryService.cs` implements `GetRequestsWithAssignedSubTasksAsync` to support personal queues.
- Service unit tests pass in `Cakra.Tests.Unit`.

Implementation Notes:  
- Created `RequestSubTaskDto.cs` with factory mapping method `FromDomain`.
- Enriched `RequestDto.cs` with `IReadOnlyList<RequestSubTaskDto> SubTasks` mapped from aggregate child entities.
- Defined `InitialSubTaskDto` and extended `RecordRequestCommand` with optional initial sub-tasks and FluentValidation rules in `RequestCommands.cs`.
- Defined `AddRequestSubTaskCommand`, `CompleteRequestSubTaskCommand`, `ReopenRequestSubTaskCommand`, and `RemoveRequestSubTaskCommand` alongside FluentValidation validators in `RequestCommands.cs`.
- Implemented dual-tier role and assignee authorization checks (TD-006) in `RequestService.cs` rejecting unauthorized calls with `UnauthorizedAccessException`.
- Extended `IRequestService` and implemented sub-task command handlers in `RequestService.cs` driving aggregate state changes, atomic persistence, and event dispatch.
- Implemented `GetRequestsWithAssignedSubTasksAsync` (TD-009) and `GetRequestSubTasksAsync` in `RequestQueryService.cs` with cross-module display name enrichment (`AssigneeName`, `CompletedByName`).
- Added MediatR query `GetRequestsWithAssignedSubTasksQuery` and handler in `RequestQueryService.cs`.
- Added unit test suite `RequestSubTaskCommandServiceTests.cs` (13 test cases) covering commands, validators, dual-tier authorization, initial sub-tasks, lifecycle guards, events, and DTO mapping.
- Verified compilation and test passes: 314 unit tests passed (0 failures).

Notes:  
Implements Architecture TD-006, TD-008, TD-009.

---

## P4 - API Gateway & HTTP Endpoints

Implementation Status: IMPLEMENTED  
Review Status: GO  

### P4-S04

Title: REST API Endpoints for Sub-Tasks and Request Intake

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective:  
Implement RESTful endpoints in `RequestsController.cs` for adding, completing, reopening, and deleting sub-tasks. Update `RecordRequestBody` to accept initial sub-tasks. Map domain exceptions (`RequestHasUnfinishedSubTasksException` to HTTP 400 Bad Request, unauthorized to HTTP 403 Forbidden). Add controller integration tests.

Depends On: P3-S03  

Repository: `cakra`  

Completion Criteria:  
- Endpoints exposed in `RequestsController.cs`:
  - `POST /api/v1/requests/{id}/subtasks` -> 200 OK with updated `RequestDto`.
  - `POST /api/v1/requests/{id}/subtasks/{subTaskId}/complete` -> 200 OK.
  - `POST /api/v1/requests/{id}/subtasks/{subTaskId}/reopen` -> 200 OK.
  - `DELETE /api/v1/requests/{id}/subtasks/{subTaskId}` -> 200 OK.
  - `POST /api/v1/requests` binds and passes `initialSubTasks`.
- `CompleteRequestCommand` returning 400 when unfinished sub-tasks exist.
- Integration tests in `Cakra.Tests.Integration/Request` verify happy paths, completion blocking, and authorization guards.
- All integration tests pass via `dotnet test cakra/tests/Cakra.Tests.Integration`.

Implementation Notes:  
- Updated `RecordRequestBody` in `RequestsController.cs` with `InitialSubTasks` and `SubTasks` mapped via `InitialSubTaskInput` and forwarded to `RecordRequestCommand`.
- Added endpoints in `RequestsController.cs`:
  - `POST /api/v1/requests/{id}/subtasks` -> dispatches `AddRequestSubTaskCommand` (HTTP 200 OK with updated `RequestDto`).
  - `POST /api/v1/requests/{id}/subtasks/{subTaskId}/complete` -> dispatches `CompleteRequestSubTaskCommand` (HTTP 200 OK).
  - `POST /api/v1/requests/{id}/subtasks/{subTaskId}/reopen` -> dispatches `ReopenRequestSubTaskCommand` (HTTP 200 OK).
  - `DELETE /api/v1/requests/{id}/subtasks/{subTaskId}` -> dispatches `RemoveRequestSubTaskCommand` (HTTP 200 OK).
  - `GET /api/v1/requests/assigned-subtasks` -> dispatches `GetRequestsWithAssignedSubTasksQuery` resolving explicitly or via `ICurrentContextProvider` (HTTP 200 OK).
- Created `RequestNotFoundException` inheriting from `KeyNotFoundException` in `Cakra.Modules.Request.Domain.Exceptions`.
- Standardized exception mapping across controller endpoints and `GlobalExceptionHandlingMiddleware.cs`:
  - `RequestHasUnfinishedSubTasksException`, `RequestDomainException`, `InvalidRequestStateTransitionException`, and validation exceptions -> HTTP 400 Bad Request
  - `UnauthorizedAccessException` -> HTTP 403 Forbidden in controllers
  - `RequestNotFoundException` and `KeyNotFoundException` -> HTTP 404 Not Found
- Added integration tests in `Cakra.Tests.Integration/Request/RequestsControllerTests.cs`:
  - `Initial_subtasks_intake_via_POST_api_v1_requests_persists_subtasks_and_calculates_progress`
  - `Subtask_lifecycle_endpoints_add_complete_reopen_and_remove_update_progress_and_metrics`
  - `Complete_request_fails_with_400_Bad_Request_when_unfinished_subtasks_exist_and_succeeds_once_all_completed`
  - `Subtask_authorization_guards_reject_unauthorized_actors_with_403_Forbidden`
  - Added unauthorized 401 checks for all sub-task endpoints.
- Verified test passes: 314 unit tests passed (0 failures) and 129 integration tests passed (0 failures).

Notes:  
Implements Architecture §3, §6, and acceptance conditions.

---

## P5 - Frontend Web UI & Verification

Implementation Status: IMPLEMENTED  
Review Status: GO  

### P5-S05

Title: Vue 3 Checklist Card, Progress Visualization, and Personal Queues

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective:  
Update the Vue 3 frontend (`Cakra.Web`) to support sub-tasks across views:
1. `RequestDetailView.vue`: Display progress bar in header (`60% (3/5)`), render interactive checklist card with one-click toggle, inline task input, assignee selector, delete button, and disable the "Complete Request" button with tooltip when sub-tasks are incomplete.
2. `RequestListView.vue`: Render "Progress" column with progress bar and ratio text.
3. `CreateRequestView.vue`: Dynamic checklist input to specify initial sub-tasks.
4. `MyRequestsView.vue`: "Assigned Sub-Tasks" section displaying assigned tasks with direct completion toggle.
5. End-to-end verification via build and test scripts.

Depends On: P4-S04  

Repository: `cakra`  

Completion Criteria:  
- Interactive checklist card rendered and functional on `RequestDetailView.vue`.
- "Complete Request" button disabled when open sub-tasks exist.
- Progress bar (`60% (3/5)`) visible on `RequestListView.vue` and `RequestDetailView.vue`.
- Initial sub-tasks can be added when creating a request in `CreateRequestView.vue`.
- Assigned sub-tasks displayed with toggle on `MyRequestsView.vue`.
- `npm run type-check` and `npm run build` pass in `cakra/src/frontend/Cakra.Web`.
- Full solution build and test pass (`dotnet build`, `dotnet test`).

Implementation Notes:  
- Created `src/api/requests.ts` exporting `RequestSubTask`, `InitialSubTaskInput`, and `RequestDto` interfaces alongside API client helper functions (`addSubTask`, `completeSubTask`, `reopenSubTask`, `removeSubTask`, `getAssignedSubTasks`) and `requestService` object.
- Updated `RequestDetailView.vue`:
  - Added header progress bar indicator badge with percentage and fraction text (`60% (3/5)`).
  - Implemented interactive Sub-Tasks Checklist card displaying sub-task list, complete/reopen checkbox toggle, assignee badge, completed metadata, inline task addition form with assignee selector, and remove button.
  - Guarded "Complete Request" button and resolution description textarea when open sub-tasks remain (`hasUnfinishedSubTasks`), rendering an explanatory warning banner and button tooltip.
- Updated `RequestListView.vue`:
  - Added "Progress" column to the requests table rendering a compact progress bar and `percentage (completed/total)` text label.
- Updated `CreateRequestView.vue`:
  - Implemented dynamic checklist section allowing users to add/remove initial sub-tasks with optional assignee picker prior to submission.
  - Passed `initialSubTasks` payload to `POST /api/v1/requests`.
- Updated `MyRequestsView.vue`:
  - Implemented dual-tab personal workspace navigation between "Assigned Requests" and "Assigned Sub-Tasks".
  - Created "Assigned Sub-Tasks" queue table with one-click completion/reopen checkbox toggle, parent request link with status badge, created timestamp, and completion audit metadata.
- Verified build and tests:
  - `npm run type-check` passed with 0 errors in `cakra/src/frontend/Cakra.Web`.
  - `npm run build` passed in 940ms in `cakra/src/frontend/Cakra.Web`.
  - `dotnet build cakra/Cakra.sln` succeeded with 0 errors and 0 warnings.
  - `dotnet test cakra/tests/backend/Cakra.Tests.Unit` passed (314 passed, 0 failed).
  - `dotnet test cakra/tests/backend/Cakra.Tests.Integration --filter "FullyQualifiedName~Request"` passed (45 passed, 0 failed).

Notes:  
Implements Architecture TD-010.

---

# 6. Change Log

- 2026-10-03: Initial implementation plan created and approved by `ica-architect`. Structured into 5 sequential, independently reviewable execution slices with continuous numbering (P1-S01 through P5-S05).
