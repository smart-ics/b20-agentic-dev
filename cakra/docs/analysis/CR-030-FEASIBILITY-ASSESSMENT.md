---
Title: Feasibility Assessment for Default Task Assignee in Work Packages (SCR-WP-001)
Code: CR-030
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-15
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Feature being assessed: Defaulting all constituent tasks (requests) captured within or added to a Work Package to the Work Package owner, providing inline replacement controls during quick capture preview and within the scope management list, and supporting explicit "Unassigned" selection, as formally captured in [CR-030-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-030-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-030-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-030-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- DOMAIN: [work-package-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/work-package-domain.md)
- FEATURE: [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md)
- FEATURE: [FEAT-REQ-002-assign-request-owner.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-002-assign-request-owner.md)
- REFERENCE ARCHITECTURE: [CR-016-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-016-ARCHITECTURE.md)
- REFERENCE ARCHITECTURE: [CR-019-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-019-ARCHITECTURE.md)
- REFERENCE ARCHITECTURE: [CR-028-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-028-ARCHITECTURE.md)

## Objective

Assess the technical feasibility, architectural constraints, gap closure resolutions, and planning readiness to:

1. Enable quick capture candidate tasks in both the Create Work Package modal and Scope Quick Bulk Add panel to pre-fill their default assignee with the Work Package Owner (`ownerPersonId`).
2. Provide an inline assignee selector dropdown on each candidate task item in the preview lists, allowing users to override the assignee with any active person or select "Unassigned".
3. Display the assigned owner on each constituent request item within the Scope Management list (`activeScopeItems` on `SCR-WP-001`) and provide an inline dropdown to quickly reassign ownership directly without navigating away.
4. Support passing an initial `OwnerPersonId` / `AssigneePersonId` in backend `RecordRequestCommand` and REST API `POST /api/v1/requests`, transitioning the newly recorded request directly to `ASSIGNED` status with an audit trail assignment log when an owner is provided.
5. Enforce non-cascading invariants: changing the Work Package owner or linking existing requests must never overwrite existing task assignments.

---

# 2. Current State

## Existing Behavior

1. **Backend Command & API Contracts (`Cakra.Modules.Request` / `RequestsController.cs`)**:
   - `RecordRequestCommand` in [RequestCommands.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs#L15-L27) defines properties for `Title`, `Description`, `CustomerId`, `ProductId`, `RequestType`, `Priority`, `ActorPersonId`, `WorkPackageId`, `Complexity`, `InitialSubTasks`, and `Deadline`. It does not accept an initial `OwnerPersonId` or `AssigneePersonId`.
   - `Request.Record(...)` in [Request.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs#L88-L162) unconditionally initializes the entity in `RequestStatus.Captured` with `OwnerPersonId = null` and logs an initial assignment to unassigned `Captured`.
   - Ownership assignment is handled via a separate command `AssignRequestOwnerCommand` (`POST /api/v1/requests/{id}/assign` / `/reassign`), which transitions `Captured -> Assigned` or reassigns an existing owner.
2. **Frontend API Client (`src/frontend/Cakra.Web/src/api/requests.ts`)**:
   - `CreateRequestPayload` does not include `ownerPersonId` or `assigneePersonId`.
   - `CandidateTaskItem` only contains `{ title: string, complexity: number }`.
   - Task parsing utility `parseTaskListTasks` only returns `{ title, complexity }` without assignee metadata.
3. **Frontend Work Package View (`src/frontend/Cakra.Web/src/views/WorkPackageView.vue`)**:
   - In the Create Work Package modal, candidate tasks are tracked in `createCandidateTasks = ref<CandidateTaskItem[]>([])`.
   - In the Scope Quick Bulk Add panel, candidate tasks are tracked in `scopeCandidateTasks = ref<CandidateTaskItem[]>([])`.
   - When calling `recordRequest`, both `handleCreateWorkPackage` and `handleBulkAddTasksToScope` omit owner information, resulting in all created requests landing in `CAPTURED` status without an assigned owner.
   - The candidate preview lists (`create-candidate-preview-container` and `scope-candidate-preview-container`) show index, complexity badge, title, and a delete button (`✕`), but no assignee dropdown or indicator.
   - The Scope Management list items (`scope-request-item` in `activeScopeItems`) display title, complexity, status, blockers, and state aging, but do not show the assigned owner or provide an inline reassignment dropdown.

## Existing Constraints

1. **Solution Neutrality & Domain Boundaries**:
   - Work Package is an operational grouping container (`workpackage.WorkPackages`) that links requests (`workpackage.WorkPackageRequests`) without owning or altering Request lifecycles (Architecture §11, Domain §5, §6).
   - Ownership and state transitions strictly belong to the `Request` aggregate root (`request.Requests`).
2. **Lifecycle State Transition Rules**:
   - A Request with an assigned owner must reside in `ASSIGNED`, `IN_PROGRESS`, or `PAUSED` state (Architecture CR-016 TD-001). A Request in `CAPTURED` state indicates unassigned triage.
   - When an owner is assigned upon creation, the initial state must reflect `ASSIGNED`, and an audit trail assignment entry must be recorded.
3. **Personnel Validation**:
   - Any assigned `OwnerPersonId` must reference a valid, active person in the Organization domain (`organization.Persons`).
4. **Scope Stability (Non-Cascading Invariant)**:
   - Changing the Work Package Owner (`POST /api/v1/work-packages/{id}/assign-owner`) must not cascade or alter the assigned owners of existing requests in the package.
   - Adding an existing request to the Work Package scope (`POST /api/v1/work-packages/{id}/requests`) must preserve the request's existing assignment state.

---

# 3. Gap Analysis

| ID | Severity | Gap |
|------|------|------|
| GAP-001 | CRITICAL | `RecordRequestCommand` and REST API endpoint (`POST /api/v1/requests`) do not accept an optional `OwnerPersonId` / `AssigneePersonId`, preventing initial owner assignment during batch request recording. |
| GAP-002 | CRITICAL | `Request.Record(...)` and `RequestService.RecordRequestAsync` do not assign an initial owner or transition the request to `ASSIGNED` status with assignment audit logging when an owner is provided at creation time. |
| GAP-003 | CRITICAL | `CandidateTaskItem` in `requests.ts` and `WorkPackageView.vue` lacks an `assigneePersonId` property, and quick-capture submission loops do not pass owner information to `recordRequest`. |
| GAP-004 | MAJOR | Candidate task preview lists in both the Create Work Package modal and the Scope Quick Bulk Add panel do not render an inline assignee selector dropdown. |
| GAP-005 | MAJOR | The Scope Management list items (`scope-request-item`) on `SCR-WP-001` do not display the assigned owner name/badge and lack an inline reassignment dropdown. |
| GAP-006 | MINOR | `parseTaskListTasks` does not accept an optional default assignee parameter to populate candidate tasks when parsed from multi-line text or raw submit. |

---

# 4. Open Questions

| ID | Question | Impact | Status |
|------|------|------|------|
| OQ-001 | Which exact entity represents the "task" whose default assignee is the package owner? | Identifies aggregate root boundary (Requests vs SubTasks). | CLOSED |
| OQ-002 | How and where should the task assignee be replaceable? | Determines UI placement (preview dropdown vs post-creation vs scope list). | CLOSED |
| OQ-003 | Does changing the Work Package owner later or linking existing requests cascade to existing tasks? | Prevents unintended mass reassignments and preserves aggregate autonomy. | CLOSED |
| OQ-004 | Can a task's assignee be cleared to "Unassigned", or is assignment strictly mandatory? | Determines if "Unassigned" is a valid option in the candidate dropdown. | CLOSED |
| OQ-005 | How should task assignees be displayed and managed in the Work Package Scope Management list? | Determines whether Scope list supports inline reassignment or requires navigation to SCR-REQ-001. | CLOSED |

---

# 5. Assumptions

| ID | Assumption |
|------|------|
| ASM-001 | Active personnel data (`activePersons`) is already fetched in `WorkPackageView.vue` for the Work Package owner dropdown and filter controls, making it immediately available for task assignee dropdowns without extra API calls. |
| ASM-002 | The existing `POST /api/v1/requests/{id}/assign` endpoint (handling `AssignRequestOwnerCommand`) is fully suitable for inline reassignments from the Scope Management list. |
| ASM-003 | In the Create Work Package modal, if candidate tasks have already been parsed with the default owner, and the user subsequently changes the modal's Work Package Owner dropdown, un-overridden candidate tasks should automatically track the new owner. |
| ASM-004 | Submitting raw text directly without prior parsing applies the Work Package Owner as the default assignee for all created requests. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|------|------|------|------|
| RISK-001 | Screen clutter and density in the candidate preview lists when adding an inline assignee dropdown next to title and complexity badges. | Medium | Use a compact, styled `<select>` element with truncated text matching existing dark slate styling, optimized for modal width. |
| RISK-002 | Accidental reassignment on rapid dropdown clicks in the Scope Management list. | Low | Provide instant visual feedback with loading state and toast notification, with disabled state while submission is in-flight. |
| RISK-003 | High latency during batch creation if `recordRequest` triggers separate synchronous assignment calls. | Medium | Incorporate initial assignment directly into `RecordRequestCommand` / `RequestService.RecordRequestAsync`, recording and assigning within a single transaction. |

---

# 7. Recommendations

## Option A (Recommended): First-Class Initial Owner Support in `RecordRequestCommand` & Reactive Inline UI
- **Backend**:
  - Add `Guid? OwnerPersonId = null` to `RecordRequestCommand` and `RecordRequestBody`.
  - In `RequestService.RecordRequestAsync`: if `ownerPersonId` is provided, validate person via `ValidateAssigneeAsync`, assign owner, set status to `RequestStatus.Assigned`, and record an initial assignment audit record. If null, record in `RequestStatus.Captured` as before.
- **Frontend**:
  - Update `CandidateTaskItem` to include `assigneePersonId?: string | null`.
  - Update `parseCandidateTaskLine` / `parseTaskListTasks` to accept optional `defaultAssigneePersonId`.
  - In `WorkPackageView.vue`:
    - In Create modal and Scope bulk add preview lists, render an inline compact `<select>` for assignee with options: `[Package Owner (Default)]`, `[Active Persons...]`, and `[Unassigned]`.
    - In Scope list (`activeScopeItems`), render an assignee badge and a quick-reassign dropdown calling `POST /api/v1/requests/{id}/assign`.
- **Advantages**: Single-roundtrip atomic creation, zero orphaned unassigned requests, immediate inline correction before and after creation, full audit compliance.
- **Disadvantages**: Minor backend command model extension.

## Option B: Frontend-Only Two-Step Orchestration (Record then Assign)
- Frontend calls `recordRequest` in `CAPTURED` status, then issues a sequential `POST /api/v1/requests/{id}/assign` for each created request.
- **Advantages**: No backend command signature change.
- **Disadvantages**: Doubles network requests during bulk capture, risks partial failure where requests are created but unassigned, creates unnecessary audit noise (`CAPTURED` immediately followed by `ASSIGNED`).

---

# 8. Gap Closure

## GAP-001 / GAP-002 / OQ-001: Target Task Entity and Initial Owner in Request Intake
### Decision
The target "tasks" are constituent `Request` entities in the Work Package. `RecordRequestCommand` and `POST /api/v1/requests` will accept an optional `OwnerPersonId` / `AssigneePersonId`. When provided, the request is recorded and immediately assigned to that owner in `ASSIGNED` status with an audit trail entry. If omitted or null, the request is recorded in `CAPTURED` status.
### Rationale
Aligns with Cakra's core aggregate boundaries where Requests represent actionable units of work. Recording with an initial owner atomically ensures proper lifecycle state and avoids redundant two-step HTTP calls.
### Impact
Requests created from quick capture immediately reflect assigned ownership.
### Architecture Impact
`Cakra.Modules.Request` command contract update and service handler enhancement.
### Resolved By
User & Analyst Alignment via `/grill-me`
### Resolved Date
2026-10-15

---

## GAP-003 / GAP-004 / OQ-002: Inline Assignee Replacement in Preview Lists
### Decision
Candidate task preview lists in both the Create modal and Scope Quick Bulk Add panel will display an inline compact assignee selector dropdown pre-filled with the Work Package Owner. Users can replace the assignee with any active person or select "Unassigned".
### Rationale
Gives operators complete upfront visibility and control over task distribution prior to committing batch creation.
### Impact
Transparent, zero-surprise bulk intake workflow.
### Architecture Impact
Frontend state and template extension in `WorkPackageView.vue` and `requests.ts`.
### Resolved By
User & Analyst Alignment via `/grill-me`
### Resolved Date
2026-10-15

---

## OQ-003: Non-Cascading Invariant on Owner Change or Request Linking
### Decision
The Work Package owner is strictly an initial default at task creation time. Modifying the Work Package owner on an existing package will never cascade or alter the assigned owners of existing tasks. Linking an existing request to the package will preserve its existing assigned owner.
### Rationale
Protects in-flight work and prevents team disruption caused by container-level metadata adjustments. Maintains Work Package's architectural boundary as an operational container rather than a lifecycle dictator.
### Impact
Guarantees assignment stability and prevents unintended ownership overrides.
### Architecture Impact
None (preserves existing WorkPackage domain behavior).
### Resolved By
User & Analyst Alignment via `/grill-me`
### Resolved Date
2026-10-15

---

## OQ-004: Unassigned Option Permitted
### Decision
The assignee dropdown explicitly includes an "Unassigned" option. The Work Package owner is a helpful default, not a mandatory constraint.
### Rationale
Provides operators the flexibility to defer assignment during brainstorming or triage without forcing artificial ownership.
### Impact
Unassigned tasks are created in `CAPTURED` status and remain available for subsequent triage.
### Architecture Impact
Supports nullable `OwnerPersonId` on intake.
### Resolved By
User & Analyst Alignment via `/grill-me`
### Resolved Date
2026-10-15

---

## GAP-005 / OQ-005: Scope List Visibility and Inline Reassignment
### Decision
Each task item in the Scope Management list (`activeScopeItems` on `SCR-WP-001`) will display the assigned owner's name/badge and offer an inline reassignment dropdown for authorized users. Changing the selection immediately executes `POST /api/v1/requests/{id}/assign` and refreshes the scope view.
### Rationale
Enables project managers and package owners to balance workload across team members directly from the cockpit without navigating away to individual request pages.
### Impact
Substantial operational efficiency gain in `SCR-WP-001`.
### Architecture Impact
Template and API invocation in `WorkPackageView.vue`.
### Resolved By
User & Analyst Alignment via `/grill-me`
### Resolved Date
2026-10-15

---

# 9. Architecture Applicability

## Decision

ARCHITECTURE-REQUIRED

## Rationale

This change modifies the core `RecordRequestCommand` contract in `Cakra.Modules.Request`, the API boundary in `RequestsController.cs`, and the domain intake workflow (`RequestService.RecordRequestAsync`) to transition a newly recorded request directly to `ASSIGNED` status with assignment audit logging. Additionally, it introduces cross-module operational coordination where `WorkPackageView.vue` consumes person metadata, manages initial task ownership, and executes inline reassignments on constituent requests. A formal Architecture specification is required to document the updated command contract, lifecycle state transition rules, and UI component behavior.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved
- [x] All required decisions recorded
- [x] All blocking open questions resolved
- [x] Architecture can be finalized or updated (Architecture evaluated as ARCHITECTURE-REQUIRED)

## Status

READY-FOR-PLANNING

## Notes

All feasibility gaps, open questions, and operational boundaries have been analyzed and closed based on user alignment. The Architect has verified the criteria and formally granted the `READY-FOR-PLANNING` gate. Architecture Applicability is confirmed as `ARCHITECTURE-REQUIRED`.

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-030-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-030-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- DOMAIN: [work-package-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/work-package-domain.md)
- FEATURE: [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md)
- FEATURE: [FEAT-REQ-002-assign-request-owner.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-002-assign-request-owner.md)

Referenced codebase locations:

- Screen component: [WorkPackageView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue)
- Request API client helper: [requests.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/requests.ts)
- Backend command: [RequestCommands.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs)
- Backend service: [RequestService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs)
- Backend controller: [RequestsController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/RequestsController.cs)

Referenced documents:

- Operational Manifesto: [operational-workflow.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/manifesto/operational-workflow.md)
