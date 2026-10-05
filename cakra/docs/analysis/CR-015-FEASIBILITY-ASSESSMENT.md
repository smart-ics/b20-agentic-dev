---
Title: Feasibility Assessment for Task Drag-and-Drop Reordering in Work Package
Code: CR-015
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-05
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Feature being assessed: Task Drag-and-Drop Reordering in Work Package, per request in [CR-015-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-015-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-015-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-015-ISSUE.md)
- DOMAIN: [CAKRA-DOMAIN.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domain/CAKRA-DOMAIN.md) / Work Package Domain
- ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- UI Layout Spec: Work Package Screen (`SCR-WP-001`)

## Objective

Assess the technical feasibility, baseline codebase state, interaction gaps, architectural impact, and planning readiness to:

1. Enable drag-and-drop reordering of linked operational requests (tasks) within the scope section of a selected Work Package in [`WorkPackageView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue) (`SCR-WP-001`).
2. Add explicit sequence tracking (`SortOrder INT NOT NULL DEFAULT 0`) to `[workpackage].[WorkPackageRequests]` and populate initial values for existing records based on `AddedAt ASC`.
3. Support batch reordering via a dedicated API endpoint `PUT /api/v1/work-packages/{id}/requests/reorder` accepting `{ "orderedRequestIds": ["guid1", "guid2", ...] }`.
4. Enforce Work Package status validation rules: reordering is allowed in `DRAFT` and `ACTIVE` states, but locked/disabled in `CLOSED` state.
5. Provide a responsive drag affordance using a dedicated grip handle icon (`bi bi-grip-vertical`), optimistic UI list updates upon drop, auto-save to the backend, and automatic rollback on failure.
6. Ensure newly added requests to a Work Package are positioned at the end of the sequence by default.

---

# 2. Current State

Summarize relevant findings from current artifacts and codebase. This section contains facts only.

## Existing Behavior

1. **Work Package Screen ([`WorkPackageView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue))**:
   - Renders the linked operational requests under the "Scope (Linked Requests)" section using a standard Bootstrap list group (`<ul class="list-group list-group-flush">`).
   - Each item displays request title, status badge, type badge, priority, owner, customer/product info, and a Remove button (when editable).
   - Items are ordered purely in the sequence returned by the API (which orders by `AddedAt ASC`).
   - There is no drag handle, no drag-and-drop event handling, and no capability for users to re-position tasks.
2. **Backend Domain Aggregate & Entity ([`WorkPackage.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackage.cs) / [`WorkPackageRequest.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackageRequest.cs))**:
   - `WorkPackage` aggregate manages constituent requests via `_requests` (`List<WorkPackageRequest>`).
   - `WorkPackageRequest` contains `Id`, `WorkPackageId`, `RequestId`, `AddedAt`, `RemovedAt`, `CreatedAt`, `UpdatedAt`.
   - `WorkPackageRequest` does not have a `SortOrder` property.
   - `WorkPackage` does not have domain methods to reorder active requests.
3. **Database Schema ([`0007_workpackage_tables.sql`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Migrations/Scripts/0007_workpackage_tables.sql))**:
   - Table `[workpackage].[WorkPackageRequests]` contains `[Id]`, `[WorkPackageId]`, `[RequestId]`, `[AddedAt]`, `[RemovedAt]`, `[CreatedAt]`, `[UpdatedAt]`.
   - There is no `[SortOrder]` column or index on `[WorkPackageId], [SortOrder]`.
4. **Backend Query & Command Services ([`WorkPackageQueryService.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageQueryService.cs) / [`WorkPackageService.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageService.cs))**:
   - `GetWorkPackageScopeAsync` orders results by `wpr.AddedAt ASC`.
   - `WorkPackageService` supports `CreateWorkPackage`, `UpdateObjective`, `AssignOwner`, `ActivateWorkPackage`, `CloseWorkPackage`, `AddRequestToWorkPackage`, and `RemoveRequestFromWorkPackage`.
   - There is no command or endpoint for reordering requests.

## Existing Constraints

1. **Transactional Integrity & Concurrency**:
   - Reordering updates multiple membership rows for a single Work Package. All order index updates must execute atomically in a single database transaction.
2. **Work Package Status Rules**:
   - Modifications (including scope additions/removals) are prohibited once a Work Package is `CLOSED`. Reordering must respect this same rule.
3. **Optimistic UI Responsiveness**:
   - Reordering should feel immediate without page reloads. In case of API failure, the client-side list state must roll back to avoid visual desynchronization.

---

# 3. Gap Analysis

Identify gaps between the requested change and the current system.

| ID | Severity | Gap |
|------|------|------|
| GAP-001 | CRITICAL | Table `[workpackage].[WorkPackageRequests]` lacks a `SortOrder INT NOT NULL` column and an index on `([WorkPackageId], [SortOrder])`. |
| GAP-002 | CRITICAL | `WorkPackageRequest` domain entity and `WorkPackage` aggregate root do not model or maintain sequence order for active requests. |
| GAP-003 | MAJOR | `WorkPackagesController` and `IWorkPackageService` lack an endpoint and command to handle batch reordering of linked requests. |
| GAP-004 | MAJOR | `WorkPackageView.vue` lacks drag-and-drop affordance (drag handle, draggable rows, drop handlers, visual indicators). |
| GAP-005 | MINOR | `WorkPackageQueryService` orders scope items by `AddedAt ASC` instead of `SortOrder ASC, AddedAt ASC`. |

---

# 4. Open Questions

All open questions have been evaluated and resolved through the `/grill-me` alignment interview:

| ID | Question | Impact | Resolution |
|------|------|------|------|
| OQ-001 | What specific drag-and-drop interaction and scope is needed? | Defines functional scope of reordering. | **Resolved**: Reordering linked operational Requests (tasks) within a single Work Package to define their sequence/execution order. |
| OQ-002 | How should the reordered sequence be persisted? | Determines database schema, entity design, and API contract. | **Resolved**: Persist an explicit integer `SortOrder` in `workpackage.WorkPackageRequests` updated via a batch reorder endpoint (`PUT /api/v1/work-packages/{id}/requests/reorder`). |
| OQ-003 | Under which Work Package statuses should drag-and-drop reordering be allowed? | Governs domain validation and UI permissions. | **Resolved**: Allow reordering in both `DRAFT` and `ACTIVE` statuses; disable/lock reordering when the Work Package is `CLOSED` (read-only). |
| OQ-004 | What user interaction and visual affordance should be provided? | Defines UI component interaction and styling. | **Resolved**: Dedicated drag handle icon (`bi bi-grip-vertical`) on each task row with optimistic UI update and automatic backend save on drop. |
| OQ-005 | How should the UI handle an API failure when saving the new order? | Governs error recovery and rollback behavior. | **Resolved**: Display an error alert and automatically roll back the task list to its previous order. |

---

# 5. Assumptions

Document assumptions made during the assessment.

| ID | Assumption |
|------|------|
| ASM-001 | Reordering only affects active linked requests (`RemovedAt IS NULL`). Inactive/removed requests retain their historical sort order or are disregarded during active display. |
| ASM-002 | The number of active linked requests in a typical Work Package is within a manageable range (dozens to a few hundred), making batch reordering of IDs in a single `PUT` payload performant and reliable. |
| ASM-003 | HTML5 Native Drag and Drop API provides sufficient cross-browser reliability for reordering list items in Vue 3 without introducing heavy third-party npm dependencies. |
| ASM-004 | Existing records in `workpackage.WorkPackageRequests` will have their `SortOrder` backfilled with sequential numbers partitioned by `WorkPackageId` and ordered by `AddedAt ASC` in the migration script. |

---

# 6. Risks

Document identified risks, impacts, and mitigations.

| ID | Risk | Impact | Mitigation |
|------|------|------|------|
| RISK-001 | Concurrency conflicts if two users reorder the same Work Package simultaneously. | Moderate | The batch reorder endpoint verifies that all provided request IDs match the active scope of the package and updates them atomically in a transaction. |
| RISK-002 | Accidental drag actions when users attempt to click request links or select text. | Moderate | Restrict the draggable trigger exclusively to the dedicated drag handle (`handle` / `bi bi-grip-vertical`) instead of making the entire card draggable. |
| RISK-003 | Transient network failure during save leaving the UI in an out-of-sync state. | Moderate | Preserve a snapshot of the prior order before initiating the drop event; immediately revert to the snapshot and display an error message if the API call fails. |

---

# 7. Recommendations

Recommend possible approaches for addressing major gaps.

## Option A: Dedicated Batch Reorder Endpoint with SortOrder Column (Recommended)

1. Add `SortOrder INT NOT NULL DEFAULT 0` column to `[workpackage].[WorkPackageRequests]`.
2. Expose `PUT /api/v1/work-packages/{id}/requests/reorder` accepting a list of request IDs in the desired order.
3. Use HTML5 Drag & Drop with a dedicated drag handle icon on each scope item in `WorkPackageView.vue`.
4. Perform optimistic list reordering and dispatch the reorder API call; roll back on failure.

### Advantages

- Direct, clean transactional consistency (all item order values updated in one roundtrip).
- Follows existing pattern in the system (e.g. `RequestSubTasks.SortOrder`).
- Dedicated drag handle avoids interference with links and buttons on the item row.
- Zero new external library dependencies required.

### Disadvantages

- Requires updating multiple rows on reorder (negligible performance cost for typical package sizes).

## Option B: Fractional Indexing (Lexicographical Rank)

Assign floating point or string rank values to items so that reordering only updates the dragged item.

### Advantages

- Only single row update per reorder.

### Disadvantages

- Requires complex re-normalization logic to avoid precision exhaustion.
- Overkill for typical list sizes in Work Packages.

---

# 8. Gap Closure

Record resolutions for gaps and open questions.

## GAP-001 / GAP-002: SortOrder Column & Domain Entity

### Decision

Add `SortOrder INT NOT NULL CONSTRAINT [DF_WorkPackageRequests_SortOrder] DEFAULT 0` to `[workpackage].[WorkPackageRequests]`. Update `WorkPackageRequest` domain entity to expose `SortOrder`. Add `ReorderRequests(IReadOnlyList<Guid> orderedRequestIds)` method on `WorkPackage` aggregate root.

### Rationale

Explicit integer sort order is standard across CAKRA modules (matches `RequestSubTasks`). Domain aggregate maintains domain consistency and invariants.

### Impact

Database schema migration required; domain entity and repository hydration updated.

### Architecture Impact

Requires updating `WorkPackageRequest` entity, aggregate root, repository queries, and adding a new migration script `0014_workpackage_requests_sort_order.sql`.

### Resolved By

User & SDLC Alignment

### Resolved Date

2026-10-05

---

## GAP-003: Batch Reorder API Endpoint

### Decision

Introduce `PUT /api/v1/work-packages/{id}/requests/reorder` accepting `ReorderWorkPackageRequestsCommand` (`List<Guid> OrderedRequestIds`). Verify Work Package status is `DRAFT` or `ACTIVE` and all IDs belong to active scope.

### Rationale

A dedicated batch reorder endpoint ensures single-transaction updates and clear REST semantics.

### Impact

New controller action, command, and handler in `Cakra.Modules.WorkPackage` and `Cakra.Api`.

### Architecture Impact

Extends `IWorkPackageService` and `WorkPackagesController`.

### Resolved By

User & SDLC Alignment

### Resolved Date

2026-10-05

---

## GAP-004 / GAP-005: Frontend Drag & Drop UX & Query Ordering

### Decision

Implement drag-and-drop in `WorkPackageView.vue` using native HTML5 drag events tied to a grip handle icon (`bi bi-grip-vertical`). Update `WorkPackageQueryService` to order active scope requests by `wpr.SortOrder ASC, wpr.AddedAt ASC`. Implement optimistic UI update with automatic rollback on API failure.

### Rationale

Provides seamless, intuitive UX matching user expectations without adding third-party library overhead.

### Impact

Modifications to `WorkPackageView.vue`, `requests.ts`/`workpackages.ts` client API, and query service SQL.

### Architecture Impact

None on architectural boundaries; frontend view component enhancement.

### Resolved By

User & SDLC Alignment

### Resolved Date

2026-10-05

---

# 9. Architecture Applicability

Record the Architecture Applicability decision.

## Decision

**ARCHITECTURE-REQUIRED**

## Rationale

This change introduces:
1. Database schema migration (new column `SortOrder` and index on `[workpackage].[WorkPackageRequests]`).
2. Domain model modifications (`WorkPackageRequest.SortOrder` and `WorkPackage.ReorderRequests`).
3. New API contract (`PUT /api/v1/work-packages/{id}/requests/reorder`).
4. Persistence layer repository updates for batch ordering.

Therefore, formal technical realization and architecture definition are required before implementation planning begins.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved
- [x] All required decisions recorded
- [x] All blocking open questions resolved
- [x] Architecture can be finalized or updated based on approved decisions

## Status

READY-FOR-PLANNING

## Notes

All feasibility gaps, open questions, and UX decisions for CR-015 have been fully analyzed and closed. The readiness checklist is complete. Architect evaluated and granted the READY-FOR-PLANNING gate for technical realization.
