---
Title: Architecture Specification for Task Drag-and-Drop Reordering in Work Package
Code: CR-015
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-05
---

# 1. Overview

This architecture artifact defines the technical realization for enabling drag-and-drop reordering of operational requests (tasks) within the scope list of a Work Package in [`WorkPackageView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue) (`SCR-WP-001`), as requested in [CR-015-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-015-ISSUE.md) and analyzed in [CR-015-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-015-FEASIBILITY-ASSESSMENT.md).

The target architecture introduces:

1. **Database Schema & Indexing**:
   - Extends `[workpackage].[WorkPackageRequests]` with `[SortOrder] INT NOT NULL CONSTRAINT [DF_WorkPackageRequests_SortOrder] DEFAULT 0`.
   - Creates non-clustered index `[IX_WorkPackageRequests_WorkPackageId_SortOrder]` on `([WorkPackageId], [SortOrder])`.
   - DbUp migration script `0014_workpackage_requests_sort_order.sql` backfills existing records partitioned by `[WorkPackageId]` ordered by `[AddedAt] ASC`.
2. **Domain Aggregate & Membership Model**:
   - Updates `WorkPackageRequest` entity to expose `SortOrder`.
   - Introduces `WorkPackage.ReorderRequests(IReadOnlyList<Guid> orderedRequestIds)` method on the `WorkPackage` aggregate root to enforce domain validation and re-assign sequence numbers.
   - When a new request is added via `WorkPackage.AddRequest`, its `SortOrder` is set to the next available sequence integer (`Max(SortOrder) + 1`).
3. **Batch Reordering API & Service Layer**:
   - Exposes `PUT /api/v1/work-packages/{id}/requests/reorder` accepting `{ "orderedRequestIds": ["guid1", "guid2", ...] }`.
   - Implements `ReorderWorkPackageRequestsCommand` in `IWorkPackageService` / `WorkPackageService`.
   - Updates `WorkPackageRepository` to persist the updated order of active memberships within a single atomic database transaction.
   - Enforces lifecycle status invariant: reordering is valid only when Work Package status is `DRAFT` or `ACTIVE`; returns `400 Bad Request` / `409 Conflict` if the Work Package is `CLOSED`.
4. **Scope Query Ordering**:
   - Updates `WorkPackageQueryService.GetWorkPackageScopeAsync` to return active requests ordered by `wpr.SortOrder ASC, wpr.AddedAt ASC`.
5. **Frontend Drag-and-Drop Affordance & Optimistic UI (`WorkPackageView.vue`)**:
   - Displays a dedicated grip handle icon (`bi bi-grip-vertical`) on each task item row in the active scope list.
   - Leverages HTML5 drag-and-drop events (`dragstart`, `dragover`, `drop`, `dragend`) with visual drop indicator styling.
   - Disables/hides drag handles when the Work Package is in `CLOSED` status.
   - Applies optimistic UI reordering on drop, dispatches the reorder API call in the background, and automatically reverts to the previous list state with an error alert if the save request fails.

---

# 2. Architectural Basis

## Business Context

- ISSUE: [CR-015-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-015-ISSUE.md)
- DOMAIN: [CAKRA-DOMAIN.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domain/CAKRA-DOMAIN.md) / Work Package Domain
- System Architecture: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- UI Layout Spec: Work Package Screen (`SCR-WP-001`)

## Analysis Input

- FEASIBILITY-ASSESSMENT: [CR-015-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-015-FEASIBILITY-ASSESSMENT.md)
  - Approved Decisions: `GAP-001` through `GAP-005`, `OQ-001` through `OQ-005`.
  - Architecture Applicability: `ARCHITECTURE-REQUIRED`.
  - Planning Readiness Gate: `READY-FOR-PLANNING` (granted).

```text
CR-015-ISSUE + CR-015-FEASIBILITY-ASSESSMENT
                     ↓
           CR-015-ARCHITECTURE
```

---

# 3. Scope

## Included

1. **Database Migration (`0014_workpackage_requests_sort_order.sql`)**:
   - Add `[SortOrder] INT NOT NULL DEFAULT 0` column to `[workpackage].[WorkPackageRequests]`.
   - Backfill sequential `SortOrder` for existing active links ordered by `AddedAt ASC`.
   - Add non-clustered index `[IX_WorkPackageRequests_WorkPackageId_SortOrder]` on `([WorkPackageId], [SortOrder])`.
2. **Domain Layer (`Cakra.Modules.WorkPackage`)**:
   - `WorkPackageRequest`: Add `SortOrder` property, constructor parameter, rehydration parameter, and internal `SetSortOrder(int sortOrder)` mutation method.
   - `WorkPackage`:
     - Update `AddRequest(Guid requestId, ...)` to assign the next available `SortOrder`.
     - Add `ReorderRequests(IReadOnlyList<Guid> orderedRequestIds)` to validate that all IDs match active scope requests and assign contiguous `SortOrder` indices (0, 1, 2, ...).
     - Update rehydration in repository.
3. **Application & Persistence Layer (`Cakra.Modules.WorkPackage`)**:
   - `ReorderWorkPackageRequestsCommand`: DTO containing `Guid WorkPackageId` and `IReadOnlyList<Guid> OrderedRequestIds`.
   - `IWorkPackageService` / `WorkPackageService`: Add `ReorderRequestsAsync` method to load aggregate, invoke `ReorderRequests`, and persist via repository.
   - `IWorkPackageRepository` / `WorkPackageRepository`: Add transactional batch update for request sort orders.
   - `WorkPackageQueryService`: Update `GetWorkPackageScopeAsync` SQL to `ORDER BY wpr.SortOrder ASC, wpr.AddedAt ASC`.
4. **API Layer (`Cakra.Api`)**:
   - Add `PUT /api/v1/work-packages/{id}/requests/reorder` endpoint to `WorkPackagesController.cs`.
   - Swagger documentation and route validation.
5. **Frontend Client (`Cakra.Web`)**:
   - Add `reorderWorkPackageRequests(workPackageId: string, orderedRequestIds: string[])` in `src/api/workpackages.ts` (or `src/api/http.ts`).
   - Update `WorkPackageView.vue`:
     - Add dedicated drag handle icon (`bi bi-grip-vertical`) to each item in `activeScopeItems`.
     - Implement HTML5 drag-and-drop handlers with visual drag state (`isDragging`, `draggedIndex`, `dropTargetIndex`).
     - Optimistic UI list reordering with immediate local state update.
     - Auto-save on drop with error toast/alert and automatic list rollback on API failure.
     - Conditionally enable drag-and-drop only when `canModifyPackage` is true (Work Package status is `DRAFT` or `ACTIVE`).
6. **Automated Testing**:
   - Unit tests in `Cakra.Tests.Unit` covering `WorkPackage.ReorderRequests`, validation rules, and sort order assignment on `AddRequest`.
   - Integration tests in `Cakra.Tests.Integration` verifying end-to-end `PUT /api/v1/work-packages/{id}/requests/reorder` and scope query order.

## Excluded

1. Cross-package drag-and-drop (moving a request from one work package to another).
2. Kanban board multi-column status transitions (this is strictly sequence/priority sorting within a package).
3. Reordering of sub-tasks inside a Request detail view (owned by `Cakra.Modules.Request`).

---

# 4. Technical Decisions

## TD-001: Explicit Sequence Integer Column on `[workpackage].[WorkPackageRequests]`

An integer column `[SortOrder] INT NOT NULL` is added to `[workpackage].[WorkPackageRequests]`.

### Schema & Index Specification

```sql
ALTER TABLE [workpackage].[WorkPackageRequests]
    ADD [SortOrder] INT NOT NULL CONSTRAINT [DF_WorkPackageRequests_SortOrder] DEFAULT 0;

CREATE NONCLUSTERED INDEX [IX_WorkPackageRequests_WorkPackageId_SortOrder]
    ON [workpackage].[WorkPackageRequests] ([WorkPackageId], [SortOrder])
    INCLUDE ([RequestId], [AddedAt], [RemovedAt]);
```

### Migration Backfill Logic

The migration script `0014_workpackage_requests_sort_order.sql` populates the initial `SortOrder` values for existing records using a window function:

```sql
WITH RankedRequests AS (
    SELECT
        [Id],
        ROW_NUMBER() OVER (PARTITION BY [WorkPackageId] ORDER BY [AddedAt] ASC) - 1 AS [NewSortOrder]
    FROM [workpackage].[WorkPackageRequests]
)
UPDATE wpr
SET wpr.[SortOrder] = rr.[NewSortOrder]
FROM [workpackage].[WorkPackageRequests] wpr
INNER JOIN RankedRequests rr ON wpr.[Id] = rr.[Id];
```

---

## TD-002: Batch Reorder API Contract & Atomic Persistence

A dedicated batch reordering endpoint is introduced to ensure atomic, single-roundtrip reordering.

### HTTP Contract

* **Endpoint**: `PUT /api/v1/work-packages/{id}/requests/reorder`
* **Content-Type**: `application/json`
* **Request Body**:
  ```json
  {
    "orderedRequestIds": [
      "2a8f9c1e-4567-4e89-8d12-abcdef123456",
      "7b1c3e5a-9876-4f12-9c34-fedcba654321",
      "9d4e2f6b-1234-4b56-7a89-0123456789ab"
    ]
  }
  ```
* **Success Response**: `200 OK` or `204 No Content`
* **Error Responses**:
  * `400 Bad Request`: Mismatched request ID list (e.g. missing active items, extraneous IDs, or duplicates).
  * `404 Not Found`: Work Package ID does not exist.
  * `409 Conflict`: Work Package is in `CLOSED` status.

---

## TD-003: Domain Aggregate Invariants & In-Memory Reordering

The `WorkPackage` aggregate root remains the authoritative boundary for business rules.

### Validation Rules in `WorkPackage.ReorderRequests`

1. **Status Invariant**: If `Status == WorkPackageStatus.Closed`, throw `InvalidWorkPackageStateTransitionException` or `BusinessRuleViolationException` ("Cannot reorder requests in a CLOSED work package.").
2. **Cardinality & Membership Invariant**:
   - The number of IDs in `orderedRequestIds` must match the count of active requests (`ActiveRequests.Count()`).
   - Every `RequestId` in `orderedRequestIds` must exist in `ActiveRequests`.
   - No duplicate IDs are allowed in `orderedRequestIds`.
3. **Sequential Re-indexing**:
   - For each `requestId` in `orderedRequestIds` at index `i`, set its `SortOrder = i`.
   - Update `UpdatedAt = DateTime.UtcNow`.

### Append-at-End Invariant in `WorkPackage.AddRequest`

When a new request is added to a Work Package:
```csharp
var nextSortOrder = _requests.Where(r => r.IsActive).Select(r => (int?)r.SortOrder).Max() ?? -1;
var newMembership = new WorkPackageRequest(Guid.NewGuid(), Id, requestId, timestamp, nextSortOrder + 1);
_requests.Add(newMembership);
```

---

## TD-004: Scope Query Ordering

In `WorkPackageQueryService.cs`, the query for `GetWorkPackageScopeAsync` is updated:

```sql
SELECT
    wpr.Id,
    wpr.Id AS MembershipId,
    wpr.WorkPackageId,
    wpr.RequestId,
    wpr.SortOrder,
    wpr.AddedAt,
    wpr.RemovedAt,
    CASE WHEN wpr.RemovedAt IS NULL THEN 1 ELSE 0 END AS IsActive,
    wpr.CreatedAt,
    wpr.UpdatedAt,
    r.Title,
    r.Title AS RequestTitle,
    r.Description,
    r.RequestType,
    r.Status,
    r.Status AS RequestStatus,
    r.Priority,
    r.OwnerPersonId,
    r.OwnerPersonId AS RequestOwnerPersonId,
    p.FullName AS OwnerName,
    p.FullName AS RequestOwnerName,
    r.CustomerId,
    c.Name AS CustomerName,
    r.ProductId,
    prd.Name AS ProductName
FROM [workpackage].[WorkPackageRequests] wpr
INNER JOIN [request].[Requests] r ON wpr.RequestId = r.Id
LEFT JOIN [organization].[Persons] p ON r.OwnerPersonId = p.Id
LEFT JOIN [customer].[Customers] c ON r.CustomerId = c.Id
LEFT JOIN [product].[Products] prd ON r.ProductId = prd.Id
WHERE wpr.WorkPackageId = @WorkPackageId
ORDER BY wpr.SortOrder ASC, wpr.AddedAt ASC;
```

---

## TD-005: Frontend HTML5 Drag-and-Drop UX & Rollback Architecture

### UX Interactions in `WorkPackageView.vue`

1. **Grip Handle**:
   - A dedicated `<span class="cursor-grab text-body-secondary me-2 drag-handle" draggable="true">` with `<i class="bi bi-grip-vertical"></i>` is rendered before the task title.
   - When the Work Package is `CLOSED`, the drag handle is hidden or disabled (`cursor-default text-muted`).
2. **Optimistic Local Update**:
   - On `drop`, the item is moved within `scopeItems` (or `activeScopeItems`) immediately.
   - An asynchronous call `reorderWorkPackageRequests(selectedWorkPackage.value.id, newOrderedIds)` is dispatched.
3. **Rollback on Error**:
   - The prior array of IDs is stored before mutating the list.
   - If the API call fails or times out, the local list is restored to the prior array, and `errorMessage.value` is populated with a clear error notification.

---

# 5. Component Responsibilities

| Component | Responsibility |
|------------|---------------|
| `0014_workpackage_requests_sort_order.sql` | Adds `[SortOrder]` column, creates index, backfills existing data. |
| `WorkPackageRequest.cs` | Models single Work Package request link with `SortOrder` property. |
| `WorkPackage.cs` | Enforces aggregate invariants: adds requests with sequential `SortOrder`, executes `ReorderRequests`. |
| `IWorkPackageRepository.cs` / `WorkPackageRepository.cs` | Persists updated sort orders in a transaction and rehydrates `WorkPackageRequest` entities. |
| `IWorkPackageService.cs` / `WorkPackageService.cs` | Orchestrates aggregate loading, command handling, and persistence for `ReorderWorkPackageRequestsCommand`. |
| `WorkPackageQueryService.cs` | Retrieves scope items ordered by `SortOrder ASC, AddedAt ASC`. |
| `WorkPackagesController.cs` | Exposes `PUT /api/v1/work-packages/{id}/requests/reorder` endpoint. |
| `WorkPackageView.vue` | Renders drag handles, handles HTML5 drag events, performs optimistic UI reordering, and manages rollback. |
| `Cakra.Web/src/api/workpackages.ts` | HTTP client wrapper for the reorder API endpoint. |

---

# 6. Integration Design

| Source | Target | Purpose |
|----------|----------|----------|
| `WorkPackageView.vue` | `api/workpackages.ts` | Dispatches batch reorder request on task drop. |
| `api/workpackages.ts` | `PUT /api/v1/work-packages/{id}/requests/reorder` | HTTP REST invocation. |
| `WorkPackagesController` | `IWorkPackageService.ReorderRequestsAsync` | Executes application command. |
| `WorkPackageService` | `WorkPackage.ReorderRequests` | Executes domain invariant validation and updates sort orders. |
| `WorkPackageService` | `IWorkPackageRepository.SaveAsync` | Persists domain entity changes and updated `SortOrder` values. |
| `WorkPackageQueryService` | `[workpackage].[WorkPackageRequests]` | Queries scope items ordered by `SortOrder ASC, AddedAt ASC`. |

---

# 7. Data Ownership

| Data | Owner |
|--------|--------|
| `[workpackage].[WorkPackages]` | `Cakra.Modules.WorkPackage` |
| `[workpackage].[WorkPackageRequests]` | `Cakra.Modules.WorkPackage` |
| `[request].[Requests]` | `Cakra.Modules.Request` (Read-only reference via `RequestId`) |

---

# 8. Database Design

## New Tables

None.

## Modified Tables

| Table | Change |
|---------|---------|
| `[workpackage].[WorkPackageRequests]` | Add `[SortOrder] INT NOT NULL CONSTRAINT [DF_WorkPackageRequests_SortOrder] DEFAULT 0`. Add index `[IX_WorkPackageRequests_WorkPackageId_SortOrder]` on `([WorkPackageId], [SortOrder])`. |

## Relationships

- Existing foreign key `[FK_WorkPackageRequests_WorkPackages]` from `[workpackage].[WorkPackageRequests](WorkPackageId)` to `[workpackage].[WorkPackages](Id)` remains unchanged.
- Pure logical reference from `[workpackage].[WorkPackageRequests](RequestId)` to `[request].[Requests](Id)` without cross-schema database foreign key (per CAKRA architecture standard §6).

## Migration Considerations

- Migration script `0014_workpackage_requests_sort_order.sql` will run automatically via DbUp on API startup.
- Uses `ROW_NUMBER() OVER (PARTITION BY WorkPackageId ORDER BY AddedAt ASC) - 1` to ensure all existing links receive a zero-based contiguous `SortOrder`.
- The script is idempotent using `IF COL_LENGTH(N'[workpackage].[WorkPackageRequests]', N'SortOrder') IS NULL`.

---

# 9. Cross-Cutting Concerns

## Concurrency & Transactional Consistency

- Batch reordering updates all active requests of a Work Package inside a single SQL transaction.
- If concurrent reorder requests occur, standard row/table locking ensures that the last committed transaction determines the authoritative sequence.

## UI Responsiveness & Error Handling

- Optimistic list updates prevent UI lag or jumping.
- Rollback mechanism guarantees that if the server rejects the request (e.g. package closed concurrently or network failure), the client state immediately reflects the true server state.

---

# 10. Implementation Constraints

1. **Dapper Persistence**: Use pure Dapper SQL with parameterized queries; zero ORMs (EF Core).
2. **Database Engine**: Microsoft SQL Server 2022 / Azure SQL Database.
3. **Architecture Boundaries**: Maintain zero cross-schema foreign keys. `WorkPackage` references `RequestId` as raw GUID.
4. **RESTful Semantics**: `PUT /api/v1/work-packages/{id}/requests/reorder` with clear error status codes (`400`, `404`, `409`).
5. **No Extra Frontend Dependencies**: Implement drag-and-drop using standard HTML5 drag events and Vue 3 reactive state.

---

# 11. Acceptance Conditions

1. `0014_workpackage_requests_sort_order.sql` executes idempotently and backfills existing rows in `[workpackage].[WorkPackageRequests]`.
2. Adding a new request to a Work Package assigns `SortOrder = Max(SortOrder) + 1`.
3. `PUT /api/v1/work-packages/{id}/requests/reorder` successfully reorders active requests in `DRAFT` and `ACTIVE` states.
4. `PUT /api/v1/work-packages/{id}/requests/reorder` returns `409 Conflict` or `400 Bad Request` if the Work Package is `CLOSED`.
5. `GET /api/v1/work-packages/{id}/scope` returns active requests ordered by `SortOrder ASC, AddedAt ASC`.
6. In `WorkPackageView.vue`, users can drag items via the grip handle to reposition them.
7. Dropping a task triggers an immediate UI update and dispatches a background save.
8. If the save fails, an error banner is displayed and the task list reverts to its previous order.
9. Drag handles are disabled/hidden when viewing a `CLOSED` Work Package.
10. All unit tests and integration tests pass without error.
