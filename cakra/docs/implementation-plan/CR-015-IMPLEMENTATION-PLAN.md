---
Title: Task Drag-and-Drop Reordering in Work Package Implementation Plan
Code: CR-015
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-05
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement drag-and-drop reordering of linked operational requests (tasks) within the scope section of a Work Package in [`WorkPackageView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue) (`SCR-WP-001`). This introduces an explicit sequence column `[SortOrder] INT NOT NULL DEFAULT 0` on `[workpackage].[WorkPackageRequests]`, domain aggregate methods for reordering and sequential appending on `WorkPackage.cs`, batch reorder persistence in `WorkPackageRepository.cs`, a REST endpoint `PUT /api/v1/work-packages/{id}/requests/reorder` on `WorkPackagesController.cs`, scope query ordering (`ORDER BY SortOrder ASC, AddedAt ASC`), and a responsive drag-and-drop frontend UX with optimistic updates and automatic rollback on failure, in accordance with [CR-015-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-015-ARCHITECTURE.md), [CR-015-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-015-FEASIBILITY-ASSESSMENT.md), and [CR-015-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-015-ISSUE.md).

**Planning Mode:** FEATURE-PLANNING

**Referenced artifacts:**
- ISSUE: [CR-015-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-015-ISSUE.md)
- FEASIBILITY-ASSESSMENT: [CR-015-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-015-FEASIBILITY-ASSESSMENT.md) (Planning Readiness: `READY-FOR-PLANNING`)
- DOMAIN: [CAKRA-DOMAIN.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domain/CAKRA-DOMAIN.md) / Work Package Domain
- ARCHITECTURE: [CR-015-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-015-ARCHITECTURE.md)
- UI Layout Spec: Work Package Screen (`SCR-WP-001`)

**Architecture Applicability:** ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers:
1. **Database Schema & Migration (`Cakra.Api`)**:
   - Create migration script `0014_workpackage_requests_sort_order.sql` adding `[SortOrder] INT NOT NULL CONSTRAINT [DF_WorkPackageRequests_SortOrder] DEFAULT 0` to `[workpackage].[WorkPackageRequests]`.
   - Backfill sequential `SortOrder` for existing active links ordered by `AddedAt ASC`.
   - Add non-clustered index `[IX_WorkPackageRequests_WorkPackageId_SortOrder]` on `([WorkPackageId], [SortOrder])`.
2. **Domain & Persistence Updates (`Cakra.Modules.WorkPackage`)**:
   - Update `WorkPackageRequest.cs` entity to expose `SortOrder` property and mutation method.
   - Update `WorkPackage.cs` aggregate root:
     - `AddRequest`: assign next sequential `SortOrder` (`Max(SortOrder) + 1`).
     - `ReorderRequests(IReadOnlyList<Guid> orderedRequestIds)`: validate active scope membership, unique IDs, status invariants (`DRAFT` / `ACTIVE` only), and re-index `SortOrder`.
   - Update `WorkPackageRepository.cs` to rehydrate `SortOrder` and execute batch transactional update of sort orders.
3. **Application Services, Scope Query & API Controller (`Cakra.Modules.WorkPackage` & `Cakra.Api`)**:
   - Update `WorkPackageQueryService.cs` to return active requests sorted by `wpr.SortOrder ASC, wpr.AddedAt ASC`.
   - Implement `ReorderWorkPackageRequestsCommand` in `IWorkPackageService` and `WorkPackageService`.
   - Add `PUT /api/v1/work-packages/{id}/requests/reorder` endpoint in `WorkPackagesController.cs`.
4. **Frontend Drag-and-Drop Implementation (`Cakra.Web`)**:
   - Add `reorderWorkPackageRequests` API helper in `src/api/workpackages.ts` (or `src/api/http.ts`).
   - In `WorkPackageView.vue`:
     - Add grip handle icon (`bi bi-grip-vertical`) to each item in the active scope list.
     - Add HTML5 drag-and-drop event handlers (`dragstart`, `dragover`, `drop`, `dragend`) with visual drop styling.
     - Implement optimistic UI reordering and background save.
     - Implement automatic rollback to previous order with error notification on API failure.
     - Disable/hide drag handles when Work Package is `CLOSED`.
5. **Automated Unit & Integration Testing (`Cakra.Tests.Unit` & `Cakra.Tests.Integration`)**:
   - Add unit tests for `WorkPackage.ReorderRequests` and `WorkPackage.AddRequest` sort order assignment.
   - Add integration tests verifying API reorder endpoint and scope query ordering.

---

# 3. Dependencies

**External Dependencies:** None.

**Slice Dependencies:** Listed in `Depends On` field for each slice below.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---------|---------|---------|---------|
| P1 - Database Schema & Domain Persistence | IMPLEMENTED | GO | 2/2 |
| P2 - Query Ordering, Application Service & API | IMPLEMENTED | GO | 2/2 |
| P3 - Frontend Drag-and-Drop UX | IMPLEMENTED | GO | 1/1 |
| P4 - Automated Testing & Verification | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Database Schema & Domain Persistence

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P1-S01

**Title:** Database Migration Script for Work Package Request SortOrder

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Create migration script `0014_workpackage_requests_sort_order.sql` to add `[SortOrder] INT NOT NULL DEFAULT 0` and index `[IX_WorkPackageRequests_WorkPackageId_SortOrder]` to `[workpackage].[WorkPackageRequests]`, backfilling existing rows partitioned by `[WorkPackageId]` ordered by `[AddedAt] ASC`.

**Depends On:** None

**Repository:** Cakra.Api

**Completion Criteria:**
- Script `0014_workpackage_requests_sort_order.sql` exists in `cakra/src/backend/Cakra.Api/Migrations/Scripts/`.
- Checks `IF COL_LENGTH(N'[workpackage].[WorkPackageRequests]', N'SortOrder') IS NULL` before adding column.
- Backfills sequential `SortOrder` using `ROW_NUMBER() OVER (PARTITION BY [WorkPackageId] ORDER BY [AddedAt] ASC) - 1`.
- Creates index `[IX_WorkPackageRequests_WorkPackageId_SortOrder]` on `([WorkPackageId], [SortOrder])`.
- Script is idempotent and runs cleanly via DbUp on application startup.

**Implementation Notes:**
- Created idempotent migration script `0014_workpackage_requests_sort_order.sql` in `cakra/src/backend/Cakra.Api/Migrations/Scripts/`.
- Script adds `[SortOrder] INT NOT NULL CONSTRAINT [DF_WorkPackageRequests_SortOrder] DEFAULT 0` to `[workpackage].[WorkPackageRequests]` if the column does not already exist.
- Performs backfill using `ROW_NUMBER() OVER (PARTITION BY [WorkPackageId] ORDER BY [AddedAt] ASC) - 1` to assign zero-based contiguous sort order for existing request links.
- Creates non-clustered index `[IX_WorkPackageRequests_WorkPackageId_SortOrder]` on `([WorkPackageId], [SortOrder])` including `([RequestId], [AddedAt], [RemovedAt])` if not already present.
- Verified compilation with `dotnet build cakra/src/backend/Cakra.Api/Cakra.Api.csproj`.

**Changed Files:**
- `cakra/src/backend/Cakra.Api/Migrations/Scripts/0014_workpackage_requests_sort_order.sql` (created)

---

### P1-S02

**Title:** Domain Entity, Aggregate Root & Repository Updates for SortOrder

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Update `WorkPackageRequest` entity to include `SortOrder`, update `WorkPackage` aggregate root with `ReorderRequests` method and sequential `SortOrder` on `AddRequest`, and update `WorkPackageRepository` to rehydrate and batch-update sort orders.

**Depends On:** P1-S01

**Repository:** Cakra.Modules.WorkPackage

**Completion Criteria:**
- `WorkPackageRequest.cs`:
  - Exposes `int SortOrder { get; private set; }`.
  - Constructor and `Rehydrate` factory accept `int sortOrder`.
  - Internal `SetSortOrder(int sortOrder)` method updates `SortOrder` and `UpdatedAt`.
- `WorkPackage.cs`:
  - `AddRequest` assigns `SortOrder = Max(active.SortOrder) + 1` (or `0` if first).
  - `ReorderRequests(IReadOnlyList<Guid> orderedRequestIds)` validates:
    - Status is not `CLOSED`.
    - `orderedRequestIds` count equals `ActiveRequests.Count()`.
    - Every ID exists in `ActiveRequests` with no duplicates.
    - Updates `SortOrder` for each item to match its index position in `orderedRequestIds`.
- `IWorkPackageRepository.cs` & `WorkPackageRepository.cs`:
  - Rehydration mappings map `SortOrder` column to `WorkPackageRequest`.
  - `SaveAsync` updates modified `WorkPackageRequest` rows including `SortOrder` within the aggregate transaction.

**Implementation Notes:**
- Updated `WorkPackageRequest.cs` with `SortOrder` property, updated constructor with `int sortOrder = 0`, added `internal void SetSortOrder(int sortOrder, DateTime? updatedAt = null)`, and updated `Rehydrate` to accept and map `int sortOrder = 0`.
- Updated `WorkPackage.cs`:
  - `ActiveRequests` ordered by `SortOrder ASC, AddedAt ASC`.
  - `AddRequestInternal` assigns `SortOrder = (Max(active.SortOrder) ?? -1) + 1`.
  - Implemented `ReorderRequests(IReadOnlyList<Guid> orderedRequestIds, DateTime? reorderedAtUtc = null)` enforcing non-CLOSED status, cardinality matching active request count, absence of duplicates, and validating existence of each request ID in active memberships before updating `SortOrder`.
- Updated `IWorkPackageRepository.cs` and `WorkPackageRepository.cs`:
  - Added `SaveAsync` transactional batch save to `IWorkPackageRepository`.
  - Updated Dapper queries in `GetByIdAsync`, `GetAllAsync`, and `GetMembershipsByWorkPackageIdAsync` to query `[SortOrder]` and sort by `[SortOrder] ASC, [AddedAt] ASC`.
  - Updated `AddRequestMembershipAsync` and `UpdateRequestMembershipAsync` to include `[SortOrder]`.
  - Updated `AddAsync` and `UpdateAsync` to execute transactional batch persistence for WorkPackage and its request memberships including `[SortOrder]`.
  - Updated `WorkPackageRequestRow` Dapper row mapping to rehydrate `SortOrder`.
- Verified compilation and test pass across all projects via `dotnet test cakra/tests/backend/Cakra.Tests.Unit/Cakra.Tests.Unit.csproj`.

**Changed Files:**
- `cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackageRequest.cs` (modified)
- `cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackage.cs` (modified)
- `cakra/src/backend/Cakra.Modules.WorkPackage/Persistence/IWorkPackageRepository.cs` (modified)
- `cakra/src/backend/Cakra.Modules.WorkPackage/Persistence/WorkPackageRepository.cs` (modified)
- `cakra/tests/backend/Cakra.Tests.Unit/WorkPackage/WorkPackageServiceTests.cs` (modified)

---

## P2 - Query Ordering, Application Service & API

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P2-S03

**Title:** Update Work Package Scope Query Ordering

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Update `WorkPackageQueryService.GetWorkPackageScopeAsync` to retrieve linked requests ordered by `wpr.SortOrder ASC, wpr.AddedAt ASC`, and map `SortOrder` in `WorkPackageScopeItemDto`.

**Depends On:** P1-S02

**Repository:** Cakra.Modules.WorkPackage

**Completion Criteria:**
- `WorkPackageQueryService.cs` SQL query orders results by `wpr.SortOrder ASC, wpr.AddedAt ASC`.
- `WorkPackageScopeItemDto` includes `SortOrder` property.
- Query maps `SortOrder` correctly from database results.

**Implementation Notes:**
- Added `SortOrder` property (`int SortOrder { get; init; }`) to `WorkPackageRequestDto` and updated `WorkPackageScopeItemDto.FromDomain` to map `membership.SortOrder`.
- Updated SQL queries in `WorkPackageQueryService.cs` (`GetWorkPackageScopeAsync` and `ListWorkPackagesAsync`) to select `wpr.[SortOrder]` and order results by `wpr.[SortOrder] ASC, wpr.[AddedAt] ASC, wpr.[CreatedAt] ASC, wpr.[Id] ASC`.
- Updated `WorkPackageMembershipQueryRow` to include `SortOrder` and map to `WorkPackageScopeItemDto`.
- Verified compilation and test pass across all backend projects via `dotnet test`.

**Changed Files:**
- `cakra/src/backend/Cakra.Modules.WorkPackage/Models/WorkPackageDto.cs` (modified)
- `cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageQueryService.cs` (modified)

---

### P2-S04

**Title:** Reorder Command, Application Service Method & API Controller Endpoint

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Implement `ReorderWorkPackageRequestsCommand` in `IWorkPackageService` and `WorkPackageService`, and expose `PUT /api/v1/work-packages/{id}/requests/reorder` in `WorkPackagesController.cs`.

**Depends On:** P2-S03

**Repository:** Cakra.Modules.WorkPackage, Cakra.Api

**Completion Criteria:**
- `ReorderWorkPackageRequestsCommand` defined with `Guid WorkPackageId` and `IReadOnlyList<Guid> OrderedRequestIds`.
- `IWorkPackageService.cs` and `WorkPackageService.cs` implement `ReorderRequestsAsync`:
  - Loads `WorkPackage` aggregate.
  - Calls `workPackage.ReorderRequests(command.OrderedRequestIds)`.
  - Saves aggregate via repository.
- `WorkPackagesController.cs`:
  - `[HttpPut("{id:guid}/requests/reorder")]` endpoint accepting `ReorderWorkPackageRequestsRequest` (`List<Guid> OrderedRequestIds`).
  - Returns `200 OK` or `204 No Content` on success.
  - Handles `WorkPackageNotFoundException` (`404`), `BusinessRuleViolationException` / `InvalidWorkPackageStateTransitionException` (`400` / `409`).

**Implementation Notes:**
- Defined `ReorderWorkPackageRequestsCommand(Guid WorkPackageId, IReadOnlyList<Guid> OrderedRequestIds)` and validator `ReorderWorkPackageRequestsCommandValidator` in `WorkPackageCommands.cs`.
- Extended `IWorkPackageService` with `ReorderRequestsAsync` and convenience alias `ReorderRequests`.
- Implemented `ReorderRequestsAsync` and MediatR `Handle(ReorderWorkPackageRequestsCommand, CancellationToken)` in `WorkPackageService`: loads aggregate, invokes `workPackage.ReorderRequests(orderedRequestIds, now)`, persists aggregate and memberships via `_workPackageRepository.SaveAsync`, and dispatches domain events.
- Exposed `[HttpPut("{id:guid}/requests/reorder")]` endpoint on `WorkPackagesController.cs` accepting `ReorderWorkPackageRequestsBody` / `ReorderWorkPackageRequestsRequest`, mapping `InvalidWorkPackageStateTransitionException` to `409 Conflict`, `WorkPackageDomainException` / `ArgumentException` to `400 Bad Request`, and missing packages to `404 Not Found`.
- Verified compilation and test pass across modules with `dotnet build cakra/src/backend/Cakra.Api/Cakra.Api.csproj` and `dotnet test cakra/tests/backend/Cakra.Tests.Unit/Cakra.Tests.Unit.csproj`.

**Changed Files:**
- `cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageCommands.cs` (modified)
- `cakra/src/backend/Cakra.Modules.WorkPackage/Services/IWorkPackageService.cs` (modified)
- `cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageService.cs` (modified)
- `cakra/src/backend/Cakra.Api/Controllers/WorkPackagesController.cs` (modified)

---

## P3 - Frontend Drag-and-Drop UX

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P3-S05

**Title:** Frontend Drag-and-Drop Implementation in WorkPackageView

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Add HTTP client reorder helper and implement HTML5 drag-and-drop reordering with dedicated grip handle, optimistic UI updates, background auto-save, and error rollback in `WorkPackageView.vue`.

**Depends On:** P2-S04

**Repository:** Cakra.Web

**Completion Criteria:**
- `src/api/workpackages.ts` (or `requests.ts`):
  - Add `reorderWorkPackageRequests(workPackageId: string, orderedRequestIds: string[])` calling `PUT /api/v1/work-packages/${workPackageId}/requests/reorder`.
- `WorkPackageView.vue`:
  - Add grip handle `<i class="bi bi-grip-vertical"></i>` to active request list items when `canModifyPackage` is true.
  - Bind drag-and-drop events (`dragstart`, `dragover`, `drop`, `dragend`).
  - Add visual feedback styles (drop indicator, dragging opacity, cursor grab/grabbing).
  - On drop, immediately reorder `activeScopeItems` / `scopeItems` locally and dispatch `reorderWorkPackageRequests`.
  - If API call fails, display error alert and roll back the list to the prior order snapshot.
  - Ensure drag handles and drag events are disabled when the Work Package is `CLOSED`.

**Implementation Notes:**
- Created `src/api/workpackages.ts` exporting `reorderWorkPackageRequests(workPackageId: string, orderedRequestIds: string[])` which issues `PUT /api/v1/work-packages/${workPackageId}/requests/reorder` along with full typed DTOs and CRUD/lifecycle service wrappers.
- In `WorkPackageView.vue`:
  - Added dedicated grip handle `<span class="drag-handle ..."><i class="bi bi-grip-vertical"></i></span>` conditionally rendered only when `canModifyPackage` is true (status is `DRAFT` or `ACTIVE`).
  - Implemented drag-and-drop event handlers: `onDragStart`, `onDragOver`, `onDragLeave`, `onDragEnd`, and `onDrop`.
  - Bound `:draggable="canModifyPackage"` and drag state classes (`is-dragging`, `drop-target`) to the active scope list items.
  - Added scoped CSS for `.drag-handle`, `.scope-request-item.is-dragging`, and `.scope-request-item.drop-target`.
  - Implemented optimistic local list reordering on drop with background `reorderWorkPackageRequests` dispatch.
  - Implemented automatic rollback to prior snapshot and error alert notification upon API failure.
  - Verified drag handle and event handlers are disabled when the Work Package is in `CLOSED` state.
- Verified TypeScript compilation and frontend bundle build via `npm run build` (`vue-tsc --noEmit && vite build`).

**Changed Files:**
- `cakra/src/frontend/Cakra.Web/src/api/workpackages.ts` (created)
- `cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue` (modified)

---

## P4 - Automated Testing & Verification

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P4-S06

**Title:** Unit and Integration Tests for Work Package Reordering

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Add unit tests covering domain aggregate reordering logic and integration tests covering the reorder API endpoint and scope query ordering.

**Depends On:** P3-S05

**Repository:** Cakra.Tests.Unit, Cakra.Tests.Integration

**Completion Criteria:**
- Unit tests in `Cakra.Tests.Unit`:
  - `WorkPackage_AddRequest_AssignsSequentialSortOrder`
  - `WorkPackage_ReorderRequests_UpdatesSortOrderCorrectly`
  - `WorkPackage_ReorderRequests_WhenClosed_ThrowsException`
  - `WorkPackage_ReorderRequests_WithMismatchedIds_ThrowsException`
- Integration tests in `Cakra.Tests.Integration`:
  - Test `PUT /api/v1/work-packages/{id}/requests/reorder` updates sort orders in database.
  - Test `GET /api/v1/work-packages/{id}/scope` returns items in the updated sort order.
  - All existing unit and integration tests pass cleanly.

**Implementation Notes:**
- Added unit tests in `WorkPackageDomainTests.cs` (`Cakra.Tests.Unit`):
  - `WorkPackage_AddRequest_AssignsSequentialSortOrder`: verifies new requests receive sequential 0-based contiguous sort orders.
  - `WorkPackage_ReorderRequests_UpdatesSortOrderCorrectly`: tests reordering in both `DRAFT` and `ACTIVE` states and verifies `ActiveRequests` ordering and `UpdatedAt` timestamp.
  - `WorkPackage_ReorderRequests_WhenClosed_ThrowsException`: verifies that reordering requests on a `CLOSED` package throws `InvalidWorkPackageStateTransitionException`.
  - `WorkPackage_ReorderRequests_WithMismatchedIds_ThrowsException`: verifies domain exception on count mismatch, non-active/foreign request ID, duplicate IDs, and null parameter.
- Added application service unit tests in `WorkPackageServiceTests.cs`:
  - `ReorderRequests_WithValidOrder_ReordersMembershipsAndPersists`: verifies `ReorderRequestsAsync` updates domain aggregate and persists sort orders through repository.
  - `ReorderRequests_WithNonexistentWorkPackage_ThrowsKeyNotFoundException`: verifies not found handling.
  - `CommandValidators_ValidateRequiredFields`: validates `ReorderWorkPackageRequestsCommandValidator` constraints.
- Updated `WorkPackageDto.cs` to order `ActiveRequests` and `FromDomain` mapping by `SortOrder ASC, AddedAt ASC`.
- Added integration test in `WorkPackagesControllerTests.cs` (`Cakra.Tests.Integration`):
  - Added `PUT /api/v1/work-packages/{id}/requests/reorder` to unauthenticated 401 Unauthorized assertions.
  - `Reorder_requests_endpoint_updates_sort_orders_in_database_and_returns_sorted_scope`: tests reorder API endpoint with 3 requests, verifies `GET /api/v1/work-packages/{id}/scope` returns sorted items with updated `SortOrder`, verifies direct database query on `[workpackage].[WorkPackageRequests]`, and verifies error cases (mismatched ID count 400, foreign ID 400, duplicate IDs 400, not found package 404, and closed package 409).
- Verified test suite execution:
  - `Cakra.Tests.Unit`: 364 tests passed (0 failed).
  - `Cakra.Tests.Integration`: 172 tests passed (0 failed).

**Changed Files:**
- `cakra/tests/backend/Cakra.Tests.Unit/WorkPackage/WorkPackageDomainTests.cs` (modified)
- `cakra/tests/backend/Cakra.Tests.Unit/WorkPackage/WorkPackageServiceTests.cs` (modified)
- `cakra/tests/backend/Cakra.Tests.Integration/WorkPackage/WorkPackagesControllerTests.cs` (modified)
- `cakra/src/backend/Cakra.Modules.WorkPackage/Models/WorkPackageDto.cs` (modified)
- `cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageCommands.cs` (modified)

---

# 6. Execution Protocol

This plan is executed under the Knowledge-Centric SDLC Implement-Review loop:
1. Slices must be executed in order according to their dependencies.
2. For each slice, `ica-implementer` implements the required code changes, tests the slice, and transitions implementation status to `IMPLEMENTED`.
3. `ica-reviewer` independently verifies the slice changes against completion criteria and architecture, and issues `GO` (or `NO-GO`).
4. Once all slices have achieved `GO`, the plan is marked `COMPLETED` and routed to test package creation (`ica-tester`).
