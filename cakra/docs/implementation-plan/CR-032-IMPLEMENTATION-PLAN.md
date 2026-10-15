---
Title: Request Blocker Capability and History Tracking Implementation Plan
Code: CR-032
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-15
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-032`: Add Blocker Capability and History Tracking to Requests across backend domain, persistence, API, and frontend workbench screens.

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- ISSUE: [CR-032-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-032-ISSUE.md)
- FEASIBILITY-ASSESSMENT: [CR-032-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-032-FEASIBILITY-ASSESSMENT.md) (Planning Readiness: `READY-FOR-PLANNING`)
- ARCHITECTURE: [CR-032-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-032-ARCHITECTURE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md) (v1.3, Business Rules 22–28)
- FEATURE: [FEAT-REQ-009-manage-request-blockers.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-009-manage-request-blockers.md)

Architecture Applicability: ARCHITECTURE-REQUIRED

This implementation plan realizes the technical architecture defined in `CR-032-ARCHITECTURE.md`, establishing first-class `RequestBlocker` domain entities, strict aggregate invariant guards for work resumption and completion, relational persistence via `[request].[RequestBlockers]`, service commands with role-based clearance authorization, typed frontend API client contracts, a dedicated Blockers management card on `SCR-REQ-003`, and visual blocked indicators on `SCR-REQ-004` and `SCR-WP-001`.

---

# 2. Planning Scope

This plan covers full-stack implementation across Cakra backend and frontend:

1. **Database Migration & Schema**:
   - Migration script `0021_request_blockers.sql` in `Cakra.Api/Migrations/Scripts`.
   - Table `[request].[RequestBlockers]` with foreign key cascading delete to `[request].[Requests]`.
   - Summary column `[ActiveBlockersCount]` on `[request].[Requests]` with non-negative check constraint.
2. **Domain Model & Invariants (`Cakra.Modules.Request.Domain`)**:
   - `RequestBlocker` entity with creation and clearance audit fields.
   - `RequestHasActiveBlockersException` domain exception.
   - Domain events `RequestBlockerAdded` and `RequestBlockerCleared`.
   - Aggregate root updates in `Request.cs`: `_blockers` collection, `AddBlocker`, `ClearBlocker`, `ActiveBlockersCount`, invariant guards in `StartWork` and `Complete`, cancellation preservation, and `Rehydrate`.
3. **Repository Persistence & Hydration (`Cakra.Modules.Request.Persistence`)**:
   - Dapper SQL query hydration in `RequestRepository.GetByIdAsync` loading blockers.
   - Transactional save of new and cleared blockers in `RequestRepository.SaveAsync`.
4. **Application Services & REST Endpoints (`Cakra.Modules.Request.Services` & `Cakra.Api`)**:
   - Commands `AddRequestBlockerCommand` and `ClearRequestBlockerCommand` with FluentValidation.
   - DTOs `RequestBlockerDto` and updated `RequestDto`.
   - Service methods `AddBlockerAsync` and `ClearBlockerAsync` with authorization in `RequestService.cs`.
   - REST endpoints `POST /api/v1/requests/{id}/blockers` and `POST /api/v1/requests/{id}/blockers/{blockerId}/clear` in `RequestsController.cs`.
5. **Frontend API Client & Workbench UI (`Cakra.Web`)**:
   - Typed contracts and API client methods in `api/requests.ts`.
   - Dedicated Blockers card, Add/Clear modals, cleared history log, and action guard disables in `RequestDetailView.vue` (`SCR-REQ-003`).
   - Visual `[Blocked]` badge and active blocker callout on cards in `MyRequestsView.vue` (`SCR-REQ-004`).
   - Blocker badge and note on scope list items in `WorkPackageView.vue` (`SCR-WP-001`).
6. **Automated Testing & Verification**:
   - Backend unit tests in `Cakra.Tests.Unit`.
   - Frontend component tests in `Cakra.Web`.
   - Full regression build and test verification.

---

# 3. Dependencies

**External Dependencies:** None

**Slice Dependencies:**
- `P1-S01` has no dependencies (`Depends On: None`).
- `P1-S02` has no dependencies (`Depends On: None`).
- `P2-S03` depends on `P1-S01, P1-S02`.
- `P2-S04` depends on `P2-S03`.
- `P3-S05` depends on `P2-S04`.
- `P3-S06` depends on `P3-S05`.
- `P3-S07` depends on `P3-S05`.
- `P4-S08` depends on `P3-S06, P3-S07`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Database Schema & Domain Invariants Foundation | NOT-STARTED | NOT-REVIEWED | 0/2 |
| P2 - Persistence, Service Orchestration & REST API | NOT-STARTED | NOT-REVIEWED | 0/2 |
| P3 - Frontend API Client & Workbench UI Integration | NOT-STARTED | NOT-REVIEWED | 0/3 |
| P4 - Verification & Automated Test Suite Execution | NOT-STARTED | NOT-REVIEWED | 0/1 |

---

# 5. Phases

## P1 - Database Schema & Domain Invariants Foundation

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED

### P1-S01

Title: Database Migration Script for Request Blockers & Active Counter

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective:
1. Create SQL migration script `0021_request_blockers.sql` in `cakra/src/backend/Cakra.Api/Migrations/Scripts`.
2. Define table `[request].[RequestBlockers]` with columns: `Id` (PK), `RequestId` (FK), `Reason`, `CreatedByPersonId`, `CreatedAtUtc`, `IsCleared`, `ClearedAtUtc`, `ClearedByPersonId`, `ClearanceReason`, `CreatedAt`, `UpdatedAt`.
3. Add foreign key `FK_RequestBlockers_Requests` referencing `[request].[Requests]([Id])` with `ON DELETE CASCADE`.
4. Add non-clustered index on `[RequestId]` and filtered index on `([RequestId], [IsCleared]) WHERE [IsCleared] = 0`.
5. Add `[ActiveBlockersCount]` column to `[request].[Requests]` with constraint `CHECK ([ActiveBlockersCount] >= 0)` and backfill script ensuring idempotent execution.

Depends On: None

Repository: `cakra`

Completion Criteria:
- File `0021_request_blockers.sql` exists and is formatted idempotently with `OBJECT_ID` and `sys.columns` guards.
- Database migration runner loads and executes the script successfully.

Implementation Notes:
- Created SQL migration script `0021_request_blockers.sql` in `cakra/src/backend/Cakra.Api/Migrations/Scripts`.
- Defined table `[request].[RequestBlockers]` with all specified columns and defaults (`Id` PK, `RequestId` FK, `Reason` NVARCHAR(1000), `CreatedByPersonId`, `CreatedAtUtc`, `IsCleared` BIT default 0, `ClearedAtUtc`, `ClearedByPersonId`, `ClearanceReason` NVARCHAR(1000), `CreatedAt`, `UpdatedAt`).
- Added cascading delete foreign key constraint `FK_RequestBlockers_Requests` referencing `[request].[Requests]([Id])`.
- Added non-clustered index `IX_RequestBlockers_RequestId` and filtered non-clustered index `IX_RequestBlockers_Active` on `([RequestId], [IsCleared]) INCLUDE ([Reason], [CreatedAtUtc]) WHERE [IsCleared] = 0`.
- Added `[ActiveBlockersCount]` column to `[request].[Requests]` with check constraint `CK_Requests_ActiveBlockersCount` enforcing `>= 0` and backfilled active blocker counts idempotently.
- Verified build of `Cakra.Api` and confirmed `0021_request_blockers.sql` is embedded as an assembly resource discovered by `DatabaseMigrationRunner`.

Changed Files:
- `cakra/src/backend/Cakra.Api/Migrations/Scripts/0021_request_blockers.sql`

Notes:
- Follow conventions established in `0012_request_subtasks.sql`.

---

### P1-S02

Title: Domain Model, RequestBlocker Entity, Domain Events & Aggregate Invariants

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective:
1. Create `RequestBlocker.cs` in `cakra/src/backend/Cakra.Modules.Request/Domain` implementing entity lifecycle, creation validation, and `Clear` method.
2. Create `RequestHasActiveBlockersException.cs` in `cakra/src/backend/Cakra.Modules.Request/Domain/Exceptions`.
3. Create domain events `RequestBlockerAdded.cs` and `RequestBlockerCleared.cs` in `cakra/src/backend/Cakra.Modules.Request/Domain/Events`.
4. Update `Request.cs`:
   - Add private `List<RequestBlocker> _blockers` and public `IReadOnlyCollection<RequestBlocker> Blockers`.
   - Add public `int ActiveBlockersCount { get; private set; }`.
   - Implement `AddBlocker(Guid blockerId, string reason, Guid createdByPersonId, DateTime? utcNow = null)`.
     - Validates open state (`ASSIGNED`, `IN_PROGRESS`, `PAUSED`). Rejects `COMPLETED` or `CANCELLED`.
     - Transitions `IN_PROGRESS -> PAUSED` (or `ASSIGNED -> PAUSED`), logs `RequestAssignment`, increments `ActiveBlockersCount`, and emits `RequestBlockerAdded`.
   - Implement `ClearBlocker(Guid blockerId, Guid clearedByPersonId, string? clearanceReason, DateTime? utcNow = null)`.
     - Locates blocker, calls `blocker.Clear`, decrements `ActiveBlockersCount`, leaves status in `PAUSED`, and emits `RequestBlockerCleared`.
   - Update `StartWork`:
     - Checks `if (_blockers.Any(b => !b.IsCleared))` and throws `RequestHasActiveBlockersException`.
   - Update `Complete`:
     - Checks `if (_blockers.Any(b => !b.IsCleared))` and throws `RequestHasActiveBlockersException`.
   - Update `Cancel`:
     - Preserves all open blockers in history.
   - Update `Rehydrate`:
     - Accepts `IEnumerable<RequestBlocker>? blockers = null, int activeBlockersCount = 0` and populates `_blockers`.

Depends On: None

Repository: `cakra`

Completion Criteria:
- `RequestBlocker.cs`, `RequestHasActiveBlockersException.cs`, `RequestBlockerAdded.cs`, `RequestBlockerCleared.cs` compiled without errors.
- `Request.cs` encapsulates blocker invariants and passes all unit tests for adding, clearing, and invariant guard violations.

Implementation Notes:
- Created `RequestBlocker` entity in `Cakra.Modules.Request.Domain` implementing `Create`, `Clear`, and `Rehydrate` with validation and audit fields.
- Created `RequestHasActiveBlockersException` in `Cakra.Modules.Request.Domain.Exceptions`.
- Created domain events `RequestBlockerAdded` and `RequestBlockerCleared` in `Cakra.Modules.Request.Domain.Events`.
- Updated `Request` aggregate root with `_blockers`, `ActiveBlockersCount`, `Blockers`, `AddBlocker`, `ClearBlocker`, `StartWork` invariant guard, `Complete` invariant guard, cancellation tolerance, and `Rehydrate` population.
- Added 16 unit tests in `RequestBlockerDomainTests.cs` verifying blocker operations, state transitions to `PAUSED`, resume/completion guards, cancellation tolerance, and rehydration. All 560 unit tests pass cleanly.

Changed Files:
- `cakra/src/backend/Cakra.Modules.Request/Domain/RequestBlocker.cs`
- `cakra/src/backend/Cakra.Modules.Request/Domain/Exceptions/RequestHasActiveBlockersException.cs`
- `cakra/src/backend/Cakra.Modules.Request/Domain/Events/RequestBlockerAdded.cs`
- `cakra/src/backend/Cakra.Modules.Request/Domain/Events/RequestBlockerCleared.cs`
- `cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs`
- `cakra/tests/backend/Cakra.Tests.Unit/Request/RequestBlockerDomainTests.cs`

Notes:
- Independent of persistence; purely domain logic and unit tests.

---

## P2 - Persistence, Service Orchestration & REST API

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED

### P2-S03

Title: Dapper Repository Mapping & Transactional Persistence for Blockers

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective:
1. Update `IRequestRepository.cs` and `RequestRepository.cs` in `cakra/src/backend/Cakra.Modules.Request/Persistence`:
   - In `GetByIdAsync`, execute parameterized query selecting from `[request].[RequestBlockers]` for `@RequestId` ordered by `CreatedAtUtc ASC`.
   - Pass hydrated blockers to `Request.Rehydrate`.
   - In `SaveAsync`, persist changes to `[request].[Requests]` (`ActiveBlockersCount`), insert new blocker rows, and update cleared blocker rows (`IsCleared`, `ClearedAtUtc`, `ClearedByPersonId`, `ClearanceReason`, `UpdatedAt`) within the existing transactional connection scope.
2. Ensure repository handles concurrency and nullable person IDs gracefully.

Depends On: P1-S01, P1-S02

Repository: `cakra`

Completion Criteria:
- Repository round-trip unit and integration tests successfully persist a request with blockers, reload it via `GetByIdAsync`, and verify all blocker fields and `ActiveBlockersCount`.

Implementation Notes:
- Updated `IRequestRepository.cs` and `RequestRepository.cs` to support transactional blocker persistence and hydration.
- In `GetByIdAsync`, selected from `[request].[RequestBlockers]` for `@RequestId` ordered by `CreatedAtUtc ASC`, hydrated `RequestBlockerRow` into `RequestBlocker`, and passed blockers to `Request.Rehydrate`.
- In `AddAsync`, persisted `ActiveBlockersCount` and inserted initial blocker rows into `[request].[RequestBlockers]`.
- In `UpdateAsync`, updated `ActiveBlockersCount` on `[request].[Requests]`, queried existing blocker IDs, inserted new blocker rows, and updated cleared blocker rows (`IsCleared`, `ClearedAtUtc`, `ClearedByPersonId`, `ClearanceReason`, `UpdatedAt`) within the existing transaction scope.
- Added `SaveAsync` method mapping to `UpdateAsync` on both `IRequestRepository` and `RequestRepository`.
- Updated `GetAllAsync` and `GetActiveInProgressByOwnerAsync` queries to include `ActiveBlockersCount`.
- Added integration test suite `RequestBlockerPersistenceIntegrationTests.cs` in `Cakra.Tests.Integration/Request` verifying round-trip persistence, incremental blocker additions, blocker clearance, nullable person IDs and clearance reasons, ordering by `CreatedAtUtc ASC`, and foreign key cascade deletion.
- Confirmed all 560 unit tests and 195 integration tests pass cleanly with zero regressions.

Changed Files:
- `cakra/src/backend/Cakra.Modules.Request/Persistence/IRequestRepository.cs`
- `cakra/src/backend/Cakra.Modules.Request/Persistence/RequestRepository.cs`
- `cakra/tests/backend/Cakra.Tests.Integration/Request/RequestBlockerPersistenceIntegrationTests.cs`

Notes:
- Model after `_subTasks` persistence in `RequestRepository.cs`.

---

### P2-S04

Title: Commands, DTO Models, Application Service & Controller Endpoints

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective:
1. Create `RequestBlockerDto.cs` and update `RequestDto.cs` in `Cakra.Modules.Request.Models` with `ActiveBlockersCount`, `HasActiveBlockers`, and `IReadOnlyList<RequestBlockerDto> Blockers`.
2. Create `AddRequestBlockerCommand` and `ClearRequestBlockerCommand` records in `Cakra.Modules.Request.Services.RequestCommands.cs` with FluentValidation validators.
3. Update `IRequestService.cs` and `RequestService.cs`:
   - Implement `AddBlockerAsync(Guid requestId, string reason, Guid? actorPersonId, CancellationToken cancellationToken)`.
   - Implement `ClearBlockerAsync(Guid requestId, Guid blockerId, string? clearanceReason, Guid? actorPersonId, CancellationToken cancellationToken)`.
   - Enforce authorization in `ClearBlockerAsync`: if actor is not the assigned owner, query `_organizationQueryService.GetPersonRolesAsync` and verify actor has role `"Administrator"`, `"Manager"`, `"System Administrator"`, or `"Team Lead"`. Throw `UnauthorizedAccessException` if not.
4. Update `RequestsController.cs` in `Cakra.Api`:
   - `POST /api/v1/requests/{id}/blockers`: binds body `{ reason, actorPersonId }` and invokes `AddBlockerAsync`, returning `201 Created` with updated `RequestDto`.
   - `POST /api/v1/requests/{id}/blockers/{blockerId}/clear`: binds body `{ clearanceReason, actorPersonId }` and invokes `ClearBlockerAsync`, returning `200 OK` with updated `RequestDto`.

Depends On: P2-S03

Repository: `cakra`

Completion Criteria:
- `RequestsController` endpoints respond with expected status codes and DTOs.
- Non-owner without manager role is rejected with 403/Unauthorized when attempting to clear a blocker.
- Adding a blocker transitions request status to `PAUSED` and returns updated blocker collection.

Implementation Notes:
- Created `RequestBlockerDto.cs` and updated `RequestDto.cs` with `ActiveBlockersCount`, `HasActiveBlockers`, and `IReadOnlyList<RequestBlockerDto> Blockers`, mapping them in `FromDomain`.
- Updated `WorkPackageDto.cs` blocker note property to check the latest active blocker reason per TD-007.
- Added `AddRequestBlockerCommand` and `ClearRequestBlockerCommand` in `RequestCommands.cs` with FluentValidation rules (`AddRequestBlockerCommandValidator` ensuring non-empty RequestId, non-empty Reason max 1000 chars; `ClearRequestBlockerCommandValidator` ensuring non-empty RequestId, non-empty BlockerId, and max 1000 chars ClearanceReason).
- Updated `IRequestService.cs` and `RequestService.cs`:
  - Added MediatR request handlers `IRequestHandler<AddRequestBlockerCommand, RequestDto>` and `IRequestHandler<ClearRequestBlockerCommand, RequestDto>`.
  - Implemented `AddBlockerAsync`, `AddBlocker`, `ClearBlockerAsync`, `ClearBlocker`.
  - Implemented `ValidateClearBlockerAuthorizationAsync` enforcing that non-owner clearance requires one of the authorized roles: `Administrator`, `Manager`, `System Administrator`, or `Team Lead` queried via `_organizationQueryService.GetPersonRolesAsync`, throwing `UnauthorizedAccessException` if unauthorized.
- Updated `IRequestQueryService.cs` and `RequestQueryService.cs`:
  - Added `GetRequestBlockersAsync` and `GetRequestBlockers` with actor name enrichment from Organization.
  - Included `ActiveBlockersCount` in grid/detail SQL projections and populated blockers list in `GetRequestByIdAsync`.
- Updated `RequestsController.cs` in `Cakra.Api`:
  - Added endpoints `POST /api/v1/requests/{id}/blockers` (returns 201 Created with updated `RequestDto`) and `POST /api/v1/requests/{id}/blockers/{blockerId}/clear` (returns 200 OK with updated `RequestDto`).
  - Added 403 Forbidden handling for `UnauthorizedAccessException` during blocker clearance.
  - Added 409 Conflict Problem mapping for `RequestHasActiveBlockersException` (`REQUEST_HAS_ACTIVE_BLOCKERS`) in `StartWork` and `ReviewRequestCompletion`.
  - Verified active blocker guard ordering in `Request.Complete` in domain to return `RequestHasActiveBlockersException` before state transition checks.
- Added comprehensive unit tests in `Cakra.Tests.Unit/Request/RequestBlockerCommandServiceTests.cs` (validators, service workflows, pause transition, owner vs manager clearance authorization, non-manager 403 authorization denial).
- Added comprehensive integration tests in `Cakra.Tests.Integration/Request/RequestsControllerTests.cs` (unauthenticated endpoint rejection, blocker addition with 201 Created, 409 Conflict guard on start/complete with active blockers, 403 Forbidden for unauthorized non-owner clearance, 200 OK for owner clearance, and subsequent work resumption).
- Verified full test suite passes (581 unit tests passing, all RequestsController integration tests passing).

Changed Files:
- `cakra/src/backend/Cakra.Modules.Request/Models/RequestBlockerDto.cs`
- `cakra/src/backend/Cakra.Modules.Request/Models/RequestDto.cs`
- `cakra/src/backend/Cakra.Modules.WorkPackage/Models/WorkPackageDto.cs`
- `cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs`
- `cakra/src/backend/Cakra.Modules.Request/Services/IRequestService.cs`
- `cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs`
- `cakra/src/backend/Cakra.Modules.Request/IRequestQueryService.cs`
- `cakra/src/backend/Cakra.Modules.Request/Services/RequestQueryService.cs`
- `cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs`
- `cakra/src/backend/Cakra.Api/Controllers/RequestsController.cs`
- `cakra/tests/backend/Cakra.Tests.Unit/Request/RequestBlockerCommandServiceTests.cs`
- `cakra/tests/backend/Cakra.Tests.Integration/Request/RequestsControllerTests.cs`
- `cakra/docs/implementation-plan/CR-032-IMPLEMENTATION-PLAN.md`

Notes:
- Map `RequestHasActiveBlockersException` to appropriate HTTP response status.

---

## P3 - Frontend API Client & Workbench UI Integration

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED

### P3-S05

Title: Frontend Typed API Client Contracts & Blocker Functions

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective:
1. Update `cakra/src/frontend/Cakra.Web/src/api/requests.ts`:
   - Export interface `RequestBlocker` with `id`, `requestId`, `reason`, `createdByPersonId`, `createdByName`, `createdAtUtc`, `isCleared`, `clearedAtUtc`, `clearedByPersonId`, `clearedByName`, `clearanceReason`.
   - Export interfaces `AddBlockerPayload` (`reason`, `actorPersonId?`) and `ClearBlockerPayload` (`clearanceReason?`, `actorPersonId?`).
   - Update `RequestDetail` and `RequestItem` interfaces to include `activeBlockersCount?: number`, `hasActiveBlockers?: boolean`, `blockers?: RequestBlocker[]`.
   - Implement and export `addBlocker(requestId: string, payload: AddBlockerPayload): Promise<RequestDetail>`.
   - Implement and export `clearBlocker(requestId: string, blockerId: string, payload: ClearBlockerPayload): Promise<RequestDetail>`.

Depends On: P2-S04

Repository: `cakra`

Completion Criteria:
- `requests.ts` exports all types and API functions without TypeScript compilation errors.
- Unit tests verify API client calls with mock Axios.

Implementation Notes:
- Exported `RequestBlocker` interface in `requests.ts` with all audit fields: `id`, `requestId`, `reason`, `createdByPersonId`, `createdByName`, `createdAtUtc`, `isCleared`, `clearedAtUtc`, `clearedByPersonId`, `clearedByName`, and `clearanceReason`.
- Exported `AddBlockerPayload` (`reason`, `actorPersonId?`) and `ClearBlockerPayload` (`clearanceReason?`, `actorPersonId?`).
- Updated `RequestDto`, `RequestDetail`, and exported `RequestItem` interfaces with `activeBlockersCount?: number`, `hasActiveBlockers?: boolean`, and `blockers?: RequestBlocker[]`.
- Implemented and exported `addBlocker(requestId, payload)` posting to `/requests/${requestId}/blockers`.
- Implemented and exported `clearBlocker(requestId, blockerId, payload)` posting to `/requests/${requestId}/blockers/${blockerId}/clear`.
- Added `addBlocker` and `clearBlocker` to `requestService` export object.
- Added comprehensive unit tests in `tests/unit/requests.spec.ts` covering endpoint paths, payload forwarding, response shape, service object exports, and interface property contracts.
- Confirmed type checking passes cleanly (`npx vue-tsc --noEmit` exited with code 0) and all 106 frontend unit tests pass cleanly.

Changed Files:
- `cakra/src/frontend/Cakra.Web/src/api/requests.ts`
- `cakra/src/frontend/Cakra.Web/tests/unit/requests.spec.ts`
- `cakra/docs/implementation-plan/CR-032-IMPLEMENTATION-PLAN.md`

Notes:
- Use existing `httpClient` helper.

---

### P3-S06

Title: SCR-REQ-003 Request Detail Blockers Card, Modals & Action Invariant Guards

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective:
1. Update `cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue`:
   - Under the Sub-Tasks section, add a dedicated Bootstrap card `data-testid="request-blockers-card"`:
     - Card header: "Operational Blockers", count badge `request.activeBlockersCount`, and an "Add Blocker" button (`data-testid="add-blocker-btn"`).
     - Active Blockers list: Table/cards showing blocker reason, reporter name, creation timestamp, and a "Clear" button (`data-testid="clear-blocker-btn"`).
     - Cleared Blockers history section: Accordion/collapsible list showing resolved blockers with reporter, resolver, clearance notes, and timestamps.
   - Add Blocker modal dialog (`data-testid="add-blocker-modal"`):
     - Textarea for `reason` (required) and submit button. Invokes `apiAddBlocker` and refreshes request state.
   - Clear Blocker modal dialog (`data-testid="clear-blocker-modal"`):
     - Textarea for `clearanceReason` (optional) and confirm button. Invokes `apiClearBlocker` and refreshes request state.
   - Lifecycle Action Guard Disabling:
     - In the Lifecycle Actions card, disable the "Start / Resume Work" button (`apiStartWork`) and "Complete Request" button (`apiCompleteRequest`) whenever `request.activeBlockersCount > 0`.
     - Render tooltip or warning alert: `"Cannot resume work or complete request while active blockers remain."`.

Depends On: P3-S05

Repository: `cakra`

Completion Criteria:
- `RequestDetailView.vue` renders the Blockers card and all sub-sections cleanly.
- Adding a blocker updates state and pauses the request.
- Clearing a blocker moves it to history.
- Resume Work and Complete buttons are strictly disabled when `activeBlockersCount > 0`.

Implementation Notes:
- Added dedicated Bootstrap card `data-testid="request-blockers-card"` directly under the Sub-Tasks section.
- Card header features "Operational Blockers", count badge `request.activeBlockersCount ?? activeBlockersCount`, and "Add Blocker" button (`data-testid="add-blocker-btn"`).
- Active blockers list renders cards with blocker reason (`data-testid="blocker-reason"`), reporter name (`data-testid="blocker-reporter"`), creation timestamp (`data-testid="blocker-timestamp"`), and a "Clear" button (`data-testid="clear-blocker-btn"`).
- Cleared blockers history section renders collapsible/accordion list (`data-testid="cleared-blockers-history"`) with reporter (`data-testid="cleared-blocker-reporter"`), resolver (`data-testid="cleared-blocker-resolver"`), clearance notes (`data-testid="cleared-blocker-notes"`), and resolution timestamps (`data-testid="cleared-blocker-resolved-at"`).
- Implemented Add Blocker modal dialog (`data-testid="add-blocker-modal"`) with required reason textarea and submit button (`data-testid="submit-add-blocker-btn"`), invoking `apiAddBlocker` and refreshing request details and history.
- Implemented Clear Blocker modal dialog (`data-testid="clear-blocker-modal"`) with optional clearanceReason textarea and confirm button (`data-testid="confirm-clear-blocker-btn"`), invoking `apiClearBlocker` and refreshing request details and history.
- Implemented lifecycle invariant guards: disabled "Start / Resume Work" button (`start-work-action-button` and `start-work-button`) and "Complete Request" button (`complete-request-action-button` and `complete-request-button`) whenever `activeBlockersCount > 0`, rendering warning alert (`data-testid="active-blockers-warning"`) and button tooltip: `"Cannot resume work or complete request while active blockers remain."`.
- Added comprehensive Vitest unit test suite `request-detail-blockers.spec.ts` (25 tests) verifying blocker filtering, count computations, invariant guard disabling truth tables, and tooltip/warning messages.
- Verified `npx vue-tsc --noEmit` compiles cleanly with zero errors and all 152 Vitest unit tests pass cleanly.

Changed Files:
- `cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue`
- `cakra/src/frontend/Cakra.Web/tests/unit/request-detail-blockers.spec.ts`
- `cakra/docs/implementation-plan/CR-032-IMPLEMENTATION-PLAN.md`

Notes:
- Style consistent with Cakra dark theme design tokens.

---

### P3-S07

Title: SCR-REQ-004 & SCR-WP-001 Visual Blocked Badges and Summaries

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective:
1. Update `cakra/src/frontend/Cakra.Web/src/views/MyRequestsView.vue`:
   - In Assigned Requests card header, if `req.activeBlockersCount > 0`, render `<span class="badge bg-danger-subtle text-danger border border-danger-subtle" data-testid="request-blocked-badge"><i class="bi bi-shield-slash"></i> Blocked ({{ req.activeBlockersCount }})</span>`.
   - On card body, display active blocker callout if blocked.
2. Update `cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue`:
   - On scope item rows, render blocker badge and active blocker reason when `item.request?.activeBlockersCount > 0` or `item.activeBlockersCount > 0`.

Depends On: P3-S05

Repository: `cakra`

Completion Criteria:
- `MyRequestsView.vue` and `WorkPackageView.vue` render blocked badges when requests have active blockers.
- No TypeScript or Vitest regressions.

Implementation Notes:
- Updated `MyRequestsView.vue` (`SCR-REQ-004`):
  - Added `activeBlockersCount`, `hasActiveBlockers`, and `blockers` properties to `AssignedRequestItem`.
  - Added helper functions `isRequestBlocked` and `resolveActiveBlockerCallout`.
  - Rendered `<span class="badge bg-danger-subtle text-danger border border-danger-subtle" data-testid="request-blocked-badge"><i class="bi bi-shield-slash"></i> Blocked ({{ req.activeBlockersCount }})</span>` in Assigned Requests card header when `req.activeBlockersCount > 0`.
  - Rendered alert callout `data-testid="request-active-blockers-callout"` on card body when `req.activeBlockersCount > 0` detailing blocker reason or count summary.
- Updated `WorkPackageView.vue` (`SCR-WP-001`):
  - Added `activeBlockersCount` and `hasActiveBlockers` properties to `WorkPackageScopeItem`.
  - Exported helper functions `getScopeItemActiveBlockersCount`, `isScopeItemBlocked`, and `resolveScopeItemBlockerReason`.
  - Updated `isRequestPausedOrBlocked` to evaluate active blockers count on both scope item and nested request alongside `PAUSED` status.
  - Updated `getRequestBlockerNote` to prioritize `item.blockerNote`, nested active blocker reason from `item.request.blockers`, assignments notes, and active blocker summary.
  - Rendered `data-testid="request-blocker-badge"` with `bi-shield-slash` icon and count, and `data-testid="request-blocker-note"` with active blocker reason.
- Added comprehensive unit tests in `my-requests.spec.ts` (8 new tests) and `workpackage-assignee.spec.ts` (13 new tests).
- Confirmed full test suite passes with 0 regressions (127 frontend Vitest tests, 581 backend unit tests, clean `vue-tsc` typecheck).

Changed Files:
- `cakra/src/frontend/Cakra.Web/src/views/MyRequestsView.vue`
- `cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue`
- `cakra/src/frontend/Cakra.Web/tests/unit/my-requests.spec.ts`
- `cakra/src/frontend/Cakra.Web/tests/unit/workpackage-assignee.spec.ts`
- `cakra/docs/implementation-plan/CR-032-IMPLEMENTATION-PLAN.md`

Notes:
- Independent of `P3-S06`; can be implemented in parallel.

---

## P4 - Verification & Automated Test Suite Execution

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED

### P4-S08

Title: Full Verification, Backend Tests, Frontend Vitest & Clean Build

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective:
1. Add backend unit tests in `cakra/tests/Cakra.Tests.Unit/Request`:
   - Test adding blockers in `ASSIGNED`, `IN_PROGRESS`, `PAUSED` states.
   - Test rejecting blocker addition in `COMPLETED` and `CANCELLED` states.
   - Test `StartWork` throwing `RequestHasActiveBlockersException` when active blockers exist.
   - Test `Complete` throwing `RequestHasActiveBlockersException` when active blockers exist.
   - Test `ClearBlocker` updating status, leaving `PAUSED`, and permitting `StartWork` once all blockers are cleared.
   - Test authorization check in `RequestService.ClearBlockerAsync`.
2. Add frontend unit tests in `cakra/src/frontend/Cakra.Web/tests/unit`:
   - Test blockers card rendering, add modal, clear modal, and disabled button guards.
3. Run complete verification:
   - `dotnet build cakra/Cakra.sln`
   - `dotnet test cakra/tests/Cakra.Tests.Unit`
   - `npm run test:unit` / `npx vitest run` in `cakra/src/frontend/Cakra.Web`
   - `npx vue-tsc --noEmit` in `cakra/src/frontend/Cakra.Web`

Depends On: P3-S06, P3-S07

Repository: `cakra`

Completion Criteria:
- 100% tests passing across backend and frontend.
- Zero build warnings or TypeScript errors.
- All acceptance conditions in `CR-032-ARCHITECTURE.md` verified.

Implementation Notes:
- Verified and expanded backend unit tests across `RequestBlockerDomainTests.cs` (16 tests) and `RequestBlockerCommandServiceTests.cs` (12 tests) confirming complete invariant coverage:
  - Adding blockers in `ASSIGNED`, `IN_PROGRESS`, and `PAUSED` states.
  - Rejecting blocker additions on terminal `COMPLETED` and `CANCELLED` states with `InvalidRequestStateTransitionException`.
  - Invariant guards on `StartWork` and `Complete` throwing `RequestHasActiveBlockersException` while active blockers exist.
  - Clearing blockers decrements count, retains `PAUSED` status, and permits `StartWork` once all active blockers are cleared.
  - Full authorization matrix verification for `RequestService.ClearBlockerAsync` enforcing owner vs authorized management roles.
- Enhanced frontend unit tests in `request-detail-blockers.spec.ts` (42 tests) verifying template structure, blockers card rendering hooks, add blocker modal validation and attributes, clear blocker modal controls, and disabled button guards with active blockers warning banner.
- Added `AssemblyInfo.cs` in `Cakra.Tests.Integration` with `[assembly: CollectionBehavior(DisableTestParallelization = true)]` to ensure reliable test isolation against the shared integration database.
- Executed full end-to-end verification runs:
  - `dotnet build cakra/Cakra.sln`: Succeeded with 0 warnings, 0 errors.
  - `dotnet test cakra/tests/backend/Cakra.Tests.Unit`: 581 tests passed (0 failed).
  - `dotnet test cakra/tests/backend/Cakra.Tests.Integration`: 196 tests passed (0 failed).
  - `npm run type-check` (`vue-tsc --noEmit`): Exited with code 0.
  - `npm run test` (`vitest run`): 6 test files, 169 tests passed (0 failed).
  - `npm run build`: Production build completed cleanly with 0 errors.

Changed Files:
- `cakra/src/frontend/Cakra.Web/tests/unit/request-detail-blockers.spec.ts`
- `cakra/tests/backend/Cakra.Tests.Integration/AssemblyInfo.cs`
- `cakra/docs/implementation-plan/CR-032-IMPLEMENTATION-PLAN.md`

Notes:
- Final slice verifying entire feature end-to-end.

---

# 6. Change Log

- 2026-10-15: Initial release of `CR-032-IMPLEMENTATION-PLAN.md` with 4 phases and 8 continuous slices. Execution Approval granted by ica-architect.
