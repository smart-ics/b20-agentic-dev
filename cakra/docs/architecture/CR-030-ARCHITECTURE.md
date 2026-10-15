---
Title: Default Task Assignee in Work Packages Architecture (CR-030)
Code: CR-030
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-15
---

# 1. Overview

This architecture defines the technical realization for Change Request `CR-030`: Default Task Assignee to Work Package Owner with Replacement Capability in `SCR-WP-001` (`WorkPackageView.vue`).

It consumes and realizes the approved feasibility decisions from [CR-030-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-030-FEASIBILITY-ASSESSMENT.md), establishing:

1. **Initial Assignee Support on Request Intake**:
   - Extension of `RecordRequestCommand` and REST API `POST /api/v1/requests` to accept an optional `OwnerPersonId` (or `AssigneePersonId`).
   - Atomic lifecycle transition in `RequestService.RecordRequestAsync`: when `OwnerPersonId` is provided, the request is validated, recorded, and transitioned immediately to `RequestStatus.Assigned` with an authoritative assignment audit record logged in `[request].[RequestAssignments]`. When omitted, the request is recorded in `RequestStatus.Captured` unassigned.
2. **Frontend Typed API Client & Parsing Extensions (`requests.ts`)**:
   - Extension of `CandidateTaskItem` to include `assigneePersonId?: string | null`.
   - Enhancement of `parseCandidateTaskLine` and `parseTaskListTasks` to accept an optional `defaultAssigneePersonId` so newly parsed tasks automatically have their assignee initialized to the specified person.
   - Updating `CreateRequestPayload` / `RecordRequestPayload` to include `ownerPersonId?: string | null` and `assigneePersonId?: string | null`.
3. **Interactive Candidate Preview Replacement (`WorkPackageView.vue`)**:
   - Inline assignee selector `<select>` on every candidate task row in both the **Create Work Package Modal** and the **Scope Management Quick Bulk Add panel**, pre-filled with the Work Package Owner.
   - Support for selecting any active organization person or explicitly choosing **"Unassigned"**.
   - Reactive synchronization in the Create Modal: changing the overall Work Package Owner automatically updates candidate tasks that have not been manually overridden.
   - Direct raw submission fallback: submitting raw text without preview parsing automatically applies the Work Package Owner to all generated requests.
4. **Scope Management List Visibility & Inline Reassignment (`WorkPackageView.vue`)**:
   - Scope list items (`scope-request-item` in `activeScopeItems`) display the assigned owner's name/badge.
   - For open packages (`canModifyPackage`), each active task item provides an inline reassignment dropdown to reassign ownership directly via `POST /api/v1/requests/{id}/assign` without leaving `SCR-WP-001`.
5. **Non-Cascading Invariant Enforcement**:
   - The package owner serves strictly as an initial default at task creation time. Reassigning the Work Package Owner or linking existing requests preserves all existing task assignments without cascading overrides.

---

# 2. Architectural Basis

## Business Context

- ISSUE: [CR-030-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-030-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- DOMAIN: [work-package-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/work-package-domain.md)
- FEATURE: [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md)
- FEATURE: [FEAT-REQ-002-assign-request-owner.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-002-assign-request-owner.md)
- UI LAYOUT: [Work Package Screen (`SCR-WP-001`)](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-030-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-030-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

This architecture consumes and realizes the approved decisions:
- `GAP-001`, `GAP-002`, `OQ-001`: Target entity is constituent `Request`; `RecordRequestCommand` accepts optional `OwnerPersonId` and records initial assignment to `ASSIGNED` atomically.
- `GAP-003`, `GAP-004`, `OQ-002`: Inline assignee dropdown on candidate task items in Create modal and Scope bulk add preview lists.
- `OQ-003`: Non-cascading invariant: package owner changes and request linking never overwrite existing task assignees.
- `OQ-004`: "Unassigned" option is explicitly supported during capture.
- `GAP-005`, `OQ-005`: Scope Management list displays assigned owner and provides inline reassignment dropdown calling `POST /api/v1/requests/{id}/assign`.

---

# 3. Scope

## Included

1. **Backend Command & Contract Updates (`Cakra.Modules.Request`)**:
   - Extend `RecordRequestCommand` with `Guid? OwnerPersonId = null` (and validator rule ensuring non-empty GUID if provided).
   - Extend `RecordRequestBody` in `RequestsController.cs` to bind `OwnerPersonId`, `AssigneePersonId`, and `AssigneeId` to `ResolvedOwnerPersonId`.
   - Update `RequestService.RecordRequestAsync` to validate the owner against `IOrganizationQueryService`, record the entity, set owner and `RequestStatus.Assigned`, and record an initial `RequestAssignment` audit entry.
2. **Frontend Typed API Client & Parsing (`requests.ts`)**:
   - Update `CandidateTaskItem` interface to include `assigneePersonId?: string | null`.
   - Update `CreateRequestPayload` / `RecordRequestPayload` with `ownerPersonId?: string | null` and `assigneePersonId?: string | null`.
   - Update `parseCandidateTaskLine` and `parseTaskListTasks` to accept optional `defaultAssigneePersonId`.
3. **Work Package Screen Preview Lists (`WorkPackageView.vue`)**:
   - Add compact assignee `<select>` dropdown to candidate task rows in the Create Work Package modal (`createCandidateTasks`) and Scope Quick Bulk Add (`scopeCandidateTasks`).
   - Populate dropdown with `activePersons` and an explicit "Unassigned" option (`value=""`).
   - Reactive sync in Create Modal: when `createForm.ownerPersonId` changes, update tasks whose assignee matched the previous owner or is unset.
   - Update `handleCreateWorkPackage` and `handleBulkAddTasksToScope` to pass `ownerPersonId: task.assigneePersonId` in `recordRequest`.
   - Support raw unparsed submit: default raw tasks to the Work Package Owner.
4. **Scope Management List Display & Inline Reassignment (`WorkPackageView.vue`)**:
   - Add assignee indicator (person name / icon badge) to each linked task in `activeScopeItems`.
   - Add an inline reassignment dropdown for active tasks on non-closed packages, invoking `POST /api/v1/requests/{id}/assign` with immediate reactive state update.
5. **Unit & Integration Verification**:
   - Unit tests in `Cakra.Tests.Unit` verifying `RecordRequestCommand` with and without `OwnerPersonId`.
   - Unit tests in `Cakra.Web` (`requests.spec.ts` or view tests) verifying candidate parsing with default assignee and inline override.

## Excluded

- Database schema migrations (existing schema `[request].[Requests]` and `[request].[RequestAssignments]` already supports nullable `OwnerPersonId` and `ASSIGNED` status).
- Modifying `RequestSubTask` checklist items (sub-tasks are separate child entities managed via `SCR-REQ-003`).
- Automatic cascading updates when Work Package owner changes on an existing package (strictly forbidden by non-cascading invariant).

---

# 4. Technical Decisions

## TD-001: Initial Owner Extension on `RecordRequestCommand` & `RecordRequestBody`
- In `Cakra.Modules.Request.Services.RequestCommands.cs`:
  ```csharp
  public sealed record RecordRequestCommand(
      string Title,
      string? Description = null,
      Guid? CustomerId = null,
      Guid? ProductId = null,
      string RequestType = "GENERAL",
      string Priority = "NORMAL",
      Guid? ActorPersonId = null,
      Guid? WorkPackageId = null,
      int? Complexity = null,
      IReadOnlyList<InitialSubTaskDto>? InitialSubTasks = null,
      DateTime? Deadline = null,
      Guid? OwnerPersonId = null) : IRequest<RequestDto>;
  ```
- In `RecordRequestCommandValidator.cs`:
  ```csharp
  RuleFor(x => x.OwnerPersonId)
      .Must(id => id != Guid.Empty)
      .When(x => x.OwnerPersonId.HasValue)
      .WithMessage("Owner person ID cannot be empty.");
  ```
- In `RequestsController.RecordRequestBody`:
  ```csharp
  public Guid? OwnerPersonId { get; set; }
  public Guid? AssigneePersonId { get; set; }
  public Guid? AssigneeId { get; set; }

  public Guid? ResolvedOwnerPersonId =>
      FirstNonEmptyGuid(OwnerPersonId, AssigneePersonId, AssigneeId);
  ```
  And in `RequestsController.Record`:
  ```csharp
  OwnerPersonId: body?.ResolvedOwnerPersonId
  ```

## TD-002: Atomic Intake Lifecycle State Transition (`Captured` vs `Assigned`)
In `RequestService.RecordRequestAsync`:
1. When `ownerPersonId` has a value:
   - Validate that the owner exists and is an active person:
     ```csharp
     await ValidateAssigneeAsync(ownerPersonId.Value, cancellationToken);
     ```
   - In `Request.Record(...)` (or domain factory method), support optional `initialOwnerPersonId`:
     - Entity properties: `OwnerPersonId = initialOwnerPersonId`, `Status = RequestStatus.Assigned`.
     - Initial assignment audit entry:
       ```csharp
       var initialAssignment = RequestAssignment.Create(
           requestId: id,
           previousOwnerPersonId: null,
           assignedOwnerPersonId: initialOwnerPersonId.Value,
           actorPersonId: resolvedActorId,
           previousStatus: null,
           newStatus: RequestStatus.Assigned,
           assignedAtUtc: now,
           notes: "Request recorded with initial assigned owner.");
       ```
2. When `ownerPersonId` is null:
   - Entity properties: `OwnerPersonId = null`, `Status = RequestStatus.Captured`.
   - Initial assignment audit entry notes: `"Request recorded in CAPTURED state."` (existing behavior).

## TD-003: Frontend Candidate Task Interface & Parser Extension
In `src/frontend/Cakra.Web/src/api/requests.ts`:
```typescript
export interface CandidateTaskItem {
  title: string
  complexity: number
  assigneePersonId?: string | null
}

export function parseCandidateTaskLine(
  line: string,
  defaultAssigneePersonId?: string | null,
): CandidateTaskItem | null {
  // 1. Clean line, extract complexity
  // ...
  return {
    title: cleanedTitle,
    complexity: parsedComplexity,
    assigneePersonId: defaultAssigneePersonId || null,
  }
}

export function parseTaskListTasks(
  rawText: string,
  defaultAssigneePersonId?: string | null,
): CandidateTaskItem[] {
  if (!rawText || !rawText.trim()) return []
  return rawText
    .split(/\r?\n/)
    .map((line) => parseCandidateTaskLine(line, defaultAssigneePersonId))
    .filter((item): item is CandidateTaskItem => item !== null)
}
```

## TD-004: Reactive Defaulting & Replacement in Candidate Preview Lists
In `WorkPackageView.vue`:
1. When parsing tasks:
   - In Create Modal:
     ```typescript
     function handleParseCreateTasks(): void {
       const defaultAssignee = createForm.ownerPersonId.trim() || null
       const parsed = parseTaskListTasks(createRawTasks.value, defaultAssignee)
       // ...
     }
     ```
   - In Scope Quick Bulk Add:
     ```typescript
     function handleParseScopeTasks(): void {
       const defaultAssignee = selectedWorkPackage.value?.ownerPersonId || null
       const parsed = parseTaskListTasks(scopeRawTasks.value, defaultAssignee)
       // ...
     }
     ```
2. Reactive synchronization on Package Owner change in Create Modal:
   - Track `previousOwnerPersonId`.
   - When `createForm.ownerPersonId` changes, iterate through `createCandidateTasks`:
     If a candidate's `assigneePersonId` matches `previousOwnerPersonId` or is null/empty, update it to the new `createForm.ownerPersonId`.
     If a candidate's `assigneePersonId` was explicitly overridden to someone else, preserve that override.
3. Preview List UI:
   - Each candidate item in `create-candidate-tasks-list` and `scope-candidate-tasks-list` contains:
     - Index badge `#idx`
     - Complexity badge `C1` / `1 pt`
     - Title text (truncated)
     - Compact `<select>` for assignee:
       - Options:
         - `<option value="">Unassigned</option>`
         - `<option v-for="p in activePersons" :key="p.id" :value="p.id">{{ resolvePersonName(p) }}</option>`
       - Bound via `v-model="task.assigneePersonId"`.
     - Delete button `✕`.
4. Submission Dispatch:
   - `recordRequest({ ..., ownerPersonId: task.assigneePersonId || null })`.
   - For raw unparsed submission:
     ```typescript
     if (tasksToRecord.length === 0 && createRawTasks.value.trim().length > 0) {
       tasksToRecord.push(...parseTaskListTasks(createRawTasks.value, createForm.ownerPersonId))
     }
     ```

## TD-005: Inline Scope List Reassignment & Feedback Flow
In `WorkPackageView.vue`:
1. Rendering in `scope-request-item` (`activeScopeItems`):
   - Display current assignee badge:
     ```html
     <div class="flex items-center gap-1.5 text-xs text-slate-300">
       <i class="bi bi-person text-cyan-400"></i>
       <select
         v-if="canModifyPackage"
         class="bg-slate-900 border border-slate-700 text-slate-200 text-xs rounded px-1.5 py-0.5 focus:border-cyan-400"
         :value="item.ownerPersonId || ''"
         :disabled="isReassigning === item.requestId || isRequestCompleted(item)"
         @change="handleInlineReassign(item.requestId, $event)"
       >
         <option value="" disabled>Select Owner</option>
         <option v-for="p in activePersons" :key="p.id" :value="p.id">
           {{ resolvePersonName(p) }}
         </option>
       </select>
       <span v-else class="text-slate-400 font-mono text-[11px]">
         {{ resolveOwnerName(item.ownerPersonId) || 'Unassigned' }}
       </span>
     </div>
     ```
2. Reassignment Handler:
   - Calls `httpClient.post(`/requests/${requestId}/assign`, { ownerPersonId: newOwnerId })`.
   - Updates local scope item owner reactively and reloads scope list / work package stats asynchronously.
   - Displays a success toast or notification confirming the reassignment.

## TD-006: Non-Cascading Invariant Preservation
- In `WorkPackageService.AssignWorkPackageOwnerAsync`:
  Only modifies `workPackage.ChangeOwner(...)`. No updates are performed on linked `[workpackage].[WorkPackageRequests]` or `[request].[Requests]`.
- In `WorkPackageService.AddRequestToWorkPackageAsync`:
  Only creates the membership link `workPackage.AddRequest(...)`. Does not modify the linked request's `OwnerPersonId` or status.

---

# 5. Component Responsibilities

| Component | Layer | Responsibility |
|-----------|-------|----------------|
| `RecordRequestCommand` | Backend Application (`Cakra.Modules.Request`) | Contract defining request recording parameters including optional `OwnerPersonId`. |
| `RecordRequestCommandValidator` | Backend Application (`Cakra.Modules.Request`) | Validates that `OwnerPersonId`, when supplied, is not empty. |
| `RequestsController` | Backend API (`Cakra.Api`) | Binds HTTP payload to `RecordRequestCommand` resolving `OwnerPersonId` aliases. |
| `RequestService` | Backend Domain Service (`Cakra.Modules.Request`) | Orchestrates validation against `IOrganizationQueryService`, invokes domain factory, records assignment audit log, and persists entity. |
| `Request` | Backend Domain Entity (`Cakra.Modules.Request`) | Aggregate root creating initial state (`Captured` or `Assigned`) and domain events (`RequestRecorded`, `RequestAssigned`). |
| `requests.ts` | Frontend API Client (`Cakra.Web`) | Exposes typed interfaces (`CandidateTaskItem`, `CreateRequestPayload`) and parsing functions with default assignee support. |
| `WorkPackageView.vue` | Frontend Screen (`Cakra.Web`) | Manages candidate preview lists with inline assignee selectors, handles raw submit defaults, displays Scope list assignees, and handles inline reassignments. |

---

# 6. Integration Design

| Source | Target | Transport | Purpose |
|--------|--------|-----------|---------|
| `WorkPackageView.vue` (Create Modal) | `RequestsController.Record` | HTTP `POST /api/v1/requests` | Creates each candidate request with inherited package context and chosen `ownerPersonId`. |
| `WorkPackageView.vue` (Scope Quick Add) | `RequestsController.Record` | HTTP `POST /api/v1/requests` | Creates each candidate request for the active package with chosen `ownerPersonId`. |
| `WorkPackageView.vue` (Scope List) | `RequestsController.AssignRequestOwner` | HTTP `POST /api/v1/requests/{id}/assign` | Executes inline ownership reassignment on a constituent task. |
| `RequestsController` | `RequestService` | In-process (MediatR) | Dispatches `RecordRequestCommand` and `AssignRequestOwnerCommand`. |
| `RequestService` | `IOrganizationQueryService` | In-process query interface | Validates that `OwnerPersonId` is a valid, active person in the Organization domain. |
| `RequestService` | `IRequestRepository` | Dapper persistence | Inserts `Request` entity and initial `RequestAssignment` audit record. |

---

# 7. Data Ownership

| Data | Owner | Storage |
|------|-------|---------|
| Request State & Owner | `Cakra.Modules.Request` | `[request].[Requests]` (`OwnerPersonId`, `Status`) |
| Request Assignment History | `Cakra.Modules.Request` | `[request].[RequestAssignments]` |
| Work Package Metadata & Owner | `Cakra.Modules.WorkPackage` | `[workpackage].[WorkPackages]` (`OwnerPersonId`) |
| Work Package Membership | `Cakra.Modules.WorkPackage` | `[workpackage].[WorkPackageRequests]` |
| Personnel Master | `Cakra.Modules.Organization` | `[organization].[Persons]` |

---

# 8. Database Design

## New Tables
None.

## Modified Tables
None. Existing tables in `[request].[Requests]` and `[request].[RequestAssignments]` already define:
- `[request].[Requests].[OwnerPersonId] UNIQUEIDENTIFIER NULL`
- `[request].[Requests].[Status] NVARCHAR(30) NOT NULL` (supporting `'CAPTURED'`, `'ASSIGNED'`, etc.)
- `[request].[RequestAssignments].[AssignedOwnerPersonId] UNIQUEIDENTIFIER NULL`

## Relationships
- `[request].[Requests].[OwnerPersonId]` references `[organization].[Persons].[Id]` across module boundaries (logical identifier reference per Architecture §6; no database foreign key constraint).

## Migration Considerations
Zero schema migrations required. Fully backward compatible.

---

# 9. Cross-Cutting Concerns

1. **Audit & Traceability**:
   - When a request is created with an assigned owner, an initial `RequestAssignment` audit entry is created with `assignedOwnerPersonId = ownerPersonId`, `newStatus = RequestStatus.Assigned`, and `assignedAtUtc = now`.
   - When inline reassignment occurs in the Scope list, `AssignRequestOwnerAsync` appends a new `RequestAssignment` record capturing `previousOwnerPersonId`, `assignedOwnerPersonId`, `actorPersonId`, and timestamp.
2. **Security & Authorization**:
   - Creating requests with an owner requires standard authenticated actor context.
   - Inline reassignment in `SCR-WP-001` is guarded in the UI by `canModifyPackage` (Work Package not closed) and validated in the backend by `AssignRequestOwnerAsync`.
3. **Performance & Concurrency**:
   - Candidate task parsing runs in-memory on the client with zero latency.
   - Batch request recording leverages `Promise.allSettled`, executing concurrent requests without blocking container creation.

---

# 10. Implementation Constraints

1. **Dapper & Domain Invariants**:
   - All persistence must follow existing Dapper repository conventions without introducing Entity Framework.
2. **REST API Backward Compatibility**:
   - `RecordRequestCommand` must keep `OwnerPersonId` optional (`null` by default) so existing consumers recording unassigned requests continue to operate unchanged.
3. **No Direct Cross-Module Writes**:
   - `WorkPackageView.vue` and `Cakra.Modules.WorkPackage` must never alter `[request].[Requests]` directly; all assignment interactions must pass through authoritative Request API endpoints.
4. **Clean Code & Type Safety**:
   - Zero use of `any` in TypeScript files.
   - All Vue components must pass `vue-tsc` and Vitest test suites.

---

# 11. Acceptance Conditions

1. **Create Work Package Modal**:
   - Parsing tasks in the Create Modal initializes each candidate task's assignee dropdown to the selected Work Package Owner.
   - Changing the Work Package Owner dropdown in the modal updates un-overridden candidate tasks to the new owner.
   - Users can change any candidate task's assignee to another person or "Unassigned".
   - Submitting creates the Work Package and all candidate requests with their respective assignees and status `ASSIGNED` (or `CAPTURED` if unassigned).
   - Submitting raw text directly without clicking "Parse Tasks" records all requests assigned to the Work Package Owner.
2. **Scope Management Quick Bulk Add**:
   - Parsing tasks in Scope Quick Bulk Add initializes candidate assignees to the active Work Package Owner.
   - Users can override assignees before adding.
   - Added requests appear in the Scope list with their designated owner.
3. **Scope Management List Display & Reassignment**:
   - Each task in `activeScopeItems` renders the assigned person's name/badge.
   - Selecting a different owner in the inline dropdown immediately updates the request's owner via API and shows confirmation feedback.
4. **Backend Contracts & Verification**:
   - Unit tests pass for `RecordRequestCommand` with `OwnerPersonId` (verifying status `ASSIGNED` and assignment audit entry).
   - Backend unit and integration tests build and pass cleanly.
   - Frontend tests build and pass cleanly.
