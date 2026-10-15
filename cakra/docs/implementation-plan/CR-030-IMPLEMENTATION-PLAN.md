---
Title: Default Task Assignee in Work Packages Implementation Plan
Code: CR-030
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-15
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-030`: Default Task Assignee to Work Package Owner with Replacement Capability in `SCR-WP-001: Work Package Screen` (`WorkPackageView.vue`).

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- ISSUE: [CR-030-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-030-ISSUE.md)
- FEASIBILITY-ASSESSMENT: [CR-030-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-030-FEASIBILITY-ASSESSMENT.md) (Planning Readiness: `READY-FOR-PLANNING`)
- ARCHITECTURE: [CR-030-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-030-ARCHITECTURE.md)

Architecture Applicability: ARCHITECTURE-REQUIRED

This implementation plan realizes the technical architecture defined in `CR-030-ARCHITECTURE.md`, establishing atomic initial owner assignment on backend request intake, typed parsing and payload contracts, interactive preview replacement in quick capture, inline scope list reassignment, and non-cascading invariants.

---

# 2. Planning Scope

This plan covers backend contracts, domain intake logic, frontend API helpers, UI component integration in `WorkPackageView.vue`, and automated test coverage across Cakra:

1. **Backend Command & API Contracts (`Cakra.Modules.Request` & `Cakra.Api`)**:
   - Extend `RecordRequestCommand` with optional `Guid? OwnerPersonId = null` and non-empty validator rule.
   - Update `RecordRequestBody` in `RequestsController.cs` to resolve `OwnerPersonId`, `AssigneePersonId`, and `AssigneeId`.
   - Update `RequestService.RecordRequestAsync` to validate assigned personnel, assign owner, transition to `RequestStatus.Assigned`, and record an initial `RequestAssignment` audit record.
2. **Frontend API Client & Parsing (`Cakra.Web`)**:
   - Update `CandidateTaskItem` in `requests.ts` with `assigneePersonId?: string | null`.
   - Update `CreateRequestPayload` / `RecordRequestPayload` with `ownerPersonId` and `assigneePersonId`.
   - Extend `parseCandidateTaskLine` and `parseTaskListTasks` to accept optional `defaultAssigneePersonId`.
3. **Work Package Screen Preview Lists (`WorkPackageView.vue`)**:
   - Add compact assignee `<select>` dropdowns to candidate task preview items in both Create Modal and Scope Quick Bulk Add.
   - Support active personnel selection and explicit "Unassigned" option.
   - Reactive sync in Create Modal when package owner changes.
   - Forward `ownerPersonId` in batch `recordRequest` calls, with fallback defaulting for raw unparsed submission.
4. **Scope Management List Display & Inline Reassignment (`WorkPackageView.vue`)**:
   - Render assigned owner name/badge on active scope items.
   - Provide an inline reassignment `<select>` dropdown on active tasks for non-closed packages, invoking `POST /api/v1/requests/{id}/assign` with immediate reactive state update.
5. **Verification & Automated Testing**:
   - Backend unit tests for `RecordRequestCommand` with and without `OwnerPersonId`.
   - Frontend unit tests for candidate parsing with default assignee and overrides.
   - Full build and test suite execution.

---

# 3. Dependencies

**External Dependencies:** None

**Slice Dependencies:**
- `P1-S01` has no dependencies (`Depends On: None`).
- `P1-S02` depends on `P1-S01`.
- `P2-S03` has no dependencies (`Depends On: None`).
- `P3-S04` depends on `P1-S02, P2-S03`.
- `P3-S05` depends on `P3-S04`.
- `P4-S06` depends on `P3-S05`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Backend Initial Owner Intake & Lifecycle State | IMPLEMENTED | GO | 2/2 |
| P2 - Frontend API Client & Candidate Parsing | IMPLEMENTED | GO | 1/1 |
| P3 - SCR-WP-001 Quick Capture & Scope Reassignment UI | IMPLEMENTED | GO | 2/2 |
| P4 - Verification & Test Suite Execution | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Backend Initial Owner Intake & Lifecycle State

Implementation Status: IMPLEMENTED  
Review Status: GO

### P1-S01

Title: Initial Owner Extension on RecordRequestCommand & Controller API

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective:
1. Extend `RecordRequestCommand` in `cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs` to accept optional `Guid? OwnerPersonId = null`.
2. Add validation rule in `RecordRequestCommandValidator` ensuring `OwnerPersonId` is not an empty GUID when supplied (`RuleFor(x => x.OwnerPersonId).Must(id => id != Guid.Empty).When(x => x.OwnerPersonId.HasValue)`).
3. Extend `RecordRequestBody` in `cakra/src/backend/Cakra.Api/Controllers/RequestsController.cs` with `OwnerPersonId`, `AssigneePersonId`, and `AssigneeId` properties, exposing `ResolvedOwnerPersonId` through `FirstNonEmptyGuid`.
4. Forward `OwnerPersonId: body?.ResolvedOwnerPersonId` when constructing `RecordRequestCommand` in `RequestsController.Record`.

Depends On: None

Repository: `cakra/src/backend`

Completion Criteria:
- `RecordRequestCommand` compiles with `Guid? OwnerPersonId = null`.
- `RecordRequestCommandValidator` validates `OwnerPersonId`.
- `RequestsController.Record` accepts owner aliases and maps to `RecordRequestCommand`.
- Solution builds with zero compilation errors.

Notes:
- Backward-compatible: existing callers omitting owner continue to record as unassigned.

Implementation Notes:
- Extended `RecordRequestCommand` with `Guid? OwnerPersonId = null`.
- Added FluentValidation rule in `RecordRequestCommandValidator` verifying non-empty GUID when supplied.
- Extended `RecordRequestBody` in `RequestsController` with `OwnerPersonId`, `AssigneePersonId`, `AssigneeId`, and resolved `ResolvedOwnerPersonId` via `FirstNonEmptyGuid`.
- Forwarded `OwnerPersonId: request?.ResolvedOwnerPersonId` in `RequestsController.RecordRequest`.
- Solution compiles with 0 errors and all unit tests pass (537 passed).

Changed Files:
- `cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs`
- `cakra/src/backend/Cakra.Api/Controllers/RequestsController.cs`

---

### P1-S02

Title: Atomic Initial Owner Assignment in RequestService & Domain Unit Tests

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective:
1. Update `RequestService.RecordRequestAsync` in `cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs`:
   - When `ownerPersonId` has a value, invoke `await ValidateAssigneeAsync(ownerPersonId.Value, cancellationToken)`.
   - Update `Request.Record(...)` (or domain assignment logic) so that providing an owner initializes the entity with `OwnerPersonId = ownerPersonId.Value`, `Status = RequestStatus.Assigned`, and records an initial `RequestAssignment` audit entry with `assignedOwnerPersonId = ownerPersonId.Value`, `newStatus = RequestStatus.Assigned`, and notes `"Request recorded with initial assigned owner."`.
   - When `ownerPersonId` is null, preserve existing behavior (`Status = RequestStatus.Captured`, `OwnerPersonId = null`).
2. Add unit tests in `cakra/tests/backend/Cakra.Tests.Unit/Request/` (e.g. `RecordRequestCommandTests.cs` or `RequestCommandServiceTests.cs`):
   - Test recording request with valid `OwnerPersonId` verifies status is `Assigned`, `OwnerPersonId` matches, and assignment audit record is created.
   - Test recording request without `OwnerPersonId` verifies status is `Captured` and `OwnerPersonId` is null.
   - Test recording with invalid/non-existent owner triggers validation error.

Depends On: P1-S01

Repository: `cakra/src/backend`

Completion Criteria:
- `RequestService.RecordRequestAsync` handles both unassigned and initially assigned requests atomically.
- Audit assignment record is correctly appended in both paths.
- Unit tests verify lifecycle status and assignment audit history.
- `dotnet test` passes for backend unit tests.

Notes:
- Ensure `GetNextMonotonicTimestamp` is used for consistent UTC timestamps.

Implementation Notes:
- Updated `Request.Record(...)` factory method in `Request.cs` to support initial owner assignment (`initialOwnerPersonId` / `ownerPersonId`), setting status to `Assigned` and recording initial `RequestAssignment` audit entry with notes `"Request recorded with initial assigned owner."`. When omitted, preserves unassigned `Captured` state.
- Updated `IRequestService.RecordRequestAsync` / `RecordRequest` and `RequestService.RecordRequestAsync` / `RecordRequest` signatures to accept optional `Guid? ownerPersonId = null`.
- Validated assignee via `ValidateAssigneeAsync` when `ownerPersonId` is provided in `RequestService.RecordRequestAsync`.
- Updated `Handle(RecordRequestCommand)` in `RequestService.cs` to forward `request.OwnerPersonId`.
- Added unit tests in `RecordRequestCommandTests.cs` verifying domain behavior, service atomic intake with assigned/unassigned states, audit history records, and key-not-found / inactive person validation errors.
- Verified test suite: all 544 backend unit tests pass.

Changed Files:
- `cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs`
- `cakra/src/backend/Cakra.Modules.Request/Services/IRequestService.cs`
- `cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs`
- `cakra/tests/backend/Cakra.Tests.Unit/Request/RecordRequestCommandTests.cs`

---

## P2 - Frontend API Client & Candidate Parsing

Implementation Status: IMPLEMENTED  
Review Status: GO

### P2-S03

Title: Frontend API Contracts & Candidate Task Parsing with Default Assignee

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective:
1. In `cakra/src/frontend/Cakra.Web/src/api/requests.ts`:
   - Extend `CandidateTaskItem` interface:
     ```ts
     export interface CandidateTaskItem {
       title: string
       complexity: number
       assigneePersonId?: string | null
     }
     ```
   - Extend `CreateRequestPayload` / `RecordRequestPayload`:
     ```ts
     ownerPersonId?: string | null
     assigneePersonId?: string | null
     ```
   - Update `parseCandidateTaskLine(line: string, defaultAssigneePersonId?: string | null): CandidateTaskItem | null` to set `assigneePersonId: defaultAssigneePersonId || null`.
   - Update `parseTaskListTasks(rawText: string, defaultAssigneePersonId?: string | null): CandidateTaskItem[]` to forward `defaultAssigneePersonId`.
2. Add or update unit tests in `src/frontend/Cakra.Web` (e.g. `requests.spec.ts`):
   - Verifying `parseTaskListTasks` applies `defaultAssigneePersonId` to parsed items.
   - Verifying complexity detection and title stripping continue to function correctly.

Depends On: None

Repository: `cakra/src/frontend/Cakra.Web`

Completion Criteria:
- `CandidateTaskItem` includes `assigneePersonId`.
- `parseTaskListTasks` populates default assignee.
- `CreateRequestPayload` supports `ownerPersonId` and `assigneePersonId`.
- Frontend unit tests pass.

Notes:
- Extended `CreateRequestPayload` (and type alias `RecordRequestPayload`) with `ownerPersonId?: string | null` and `assigneePersonId?: string | null`.
- Extended `CandidateTaskItem` with optional `assigneePersonId?: string | null`.
- Updated `parseCandidateTaskLine` and `parseTaskListTasks` to accept optional `defaultAssigneePersonId?: string | null`, assigning `defaultAssigneePersonId || null` to `assigneePersonId`.
- Updated existing `requests.spec.ts` unit tests and added new test suites for default assignee propagation in single-line and multiline parsing.
- Verified test suite passes (49/49 passed) and build (`vue-tsc --noEmit && vite build`) succeeds without error.
- Changed Files:
  - `cakra/src/frontend/Cakra.Web/src/api/requests.ts`
  - `cakra/src/frontend/Cakra.Web/tests/unit/requests.spec.ts`

---

## P3 - SCR-WP-001 Quick Capture & Scope Reassignment UI

Implementation Status: IMPLEMENTED  
Review Status: GO

### P3-S04

Title: Candidate Preview Inline Assignee Replacement in Create Modal & Scope Quick Add

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective:
1. In `cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue`:
   - Update `handleParseCreateTasks` to pass `createForm.ownerPersonId.trim() || null` to `parseTaskListTasks`.
   - Update `handleParseScopeTasks` to pass `selectedWorkPackage.value?.ownerPersonId || null` to `parseTaskListTasks`.
   - In Create Modal candidate preview list (`create-candidate-preview-container`), add an inline compact `<select>` for each candidate task:
     - Bound to `task.assigneePersonId`.
     - Option for `"Unassigned"` (`value=""`).
     - Options for each person in `activePersons`.
   - In Scope Quick Bulk Add preview list (`scope-candidate-preview-container`), add the identical compact `<select>` for each candidate task.
   - Add reactive sync in Create Modal: when `createForm.ownerPersonId` changes, update candidate tasks that were previously defaulted to the old owner, while preserving manually overridden tasks.
   - Update `handleCreateWorkPackage` and `handleBulkAddTasksToScope`:
     - Forward `ownerPersonId: task.assigneePersonId || null` in `recordRequest`.
     - For raw unparsed submissions, default tasks to the Work Package Owner.

Depends On: P1-S02, P2-S03

Repository: `cakra/src/frontend/Cakra.Web`

Completion Criteria:
- Preview lists in both Create Modal and Scope Quick Add show an inline assignee dropdown for each task.
- Changing package owner in Create Modal reactively updates defaulted candidate tasks.
- Overrides and "Unassigned" selections are preserved.
- Submitting records requests with their designated `ownerPersonId`.
- Raw unparsed submits assign the package owner as default.

Notes:
- Keep dropdown compact and cleanly styled with dark theme classes (`bg-slate-900 border-slate-700 text-slate-200 text-xs`).

Implementation Notes:
- Updated `handleParseCreateTasks` to pass `createForm.ownerPersonId.trim() || null` to `parseTaskListTasks`.
- Updated `handleParseScopeTasks` to pass `selectedWorkPackage.value?.ownerPersonId || null` to `parseTaskListTasks`.
- Added inline compact `<select>` dropdown (`bg-slate-900 border-slate-700 text-slate-200 text-xs`) for each candidate task in both Create Modal (`create-candidate-assignee-select`) and Scope Quick Add (`scope-candidate-assignee-select`) candidate lists, populated with `"Unassigned"` (`value=""`) and `activePersons` via helper `resolvePersonName`.
- Added reactive sync on `createForm.ownerPersonId` in Create Modal via `syncCandidateAssigneesOnOwnerChange` and `overriddenCandidateTasks`: automatically updates defaulted tasks when owner changes while preserving manually overridden tasks and explicit "Unassigned" choices.
- Updated `handleCreateWorkPackage` and `handleBulkAddTasksToScope` to forward `ownerPersonId: task.assigneePersonId || null` to `recordRequest`, and to default tasks to the work package owner on raw unparsed bulk submissions.
- Added comprehensive unit tests in `workpackage-assignee.spec.ts` covering person name resolution, reactive sync, overrides/unassigned preservation, and default assignee propagation.
- Verified all frontend tests pass (58/58 passed) and build (`vue-tsc --noEmit && vite build`) succeeds without error.

Changed Files:
- `cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue`
- `cakra/src/frontend/Cakra.Web/tests/unit/workpackage-assignee.spec.ts`

---

### P3-S05

Title: Scope Management List Assignee Display & Inline Reassignment

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective:
1. In `cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue`:
   - In the Scope Management list items (`scope-request-item` in `activeScopeItems`):
     - Render the assigned owner's name/badge (or "Unassigned" if unassigned).
     - For open packages (`canModifyPackage`), provide an inline reassignment `<select>` dropdown populated with `activePersons`.
     - If the package is closed or the request is completed, display the owner as read-only.
   - Implement `handleInlineReassign(requestId: string, newOwnerPersonId: string)`:
     - Invoke `POST /api/v1/requests/{id}/assign` with `{ ownerPersonId: newOwnerPersonId }`.
     - Display loading/disabled state on the dropdown while in-flight.
     - Update local scope item owner reactively and trigger background refresh of scope items.
     - Display success notification upon completion, or extract error message upon failure.

Depends On: P3-S04

Repository: `cakra/src/frontend/Cakra.Web`

Completion Criteria:
- Active scope items render the assigned owner.
- Inline reassignment dropdown enables direct reassignment on open packages.
- Successful API call updates the UI state immediately with feedback.
- Completed requests and closed packages render read-only owner text.

Notes:
- Use existing `personNameById` computed map for person name resolution.

Implementation Notes:
- Rendered assigned owner display in Scope Management list items (`scope-request-item` in `activeScopeItems`).
- Added inline reassignment `<select>` dropdown (`scope-inline-reassign-select`) populated with `activePersons` and placeholder `Select Owner (Unassigned)` for open packages (`canModifyPackage && !isRequestCompleted(item)`).
- Rendered read-only owner text (`scope-owner-readonly`) via `resolveScopeItemOwnerName` (falling back to "Unassigned") for completed requests or closed packages.
- Implemented `handleInlineReassign(requestId: string, newOwnerPersonId: string | Event)`: invokes `POST /api/v1/requests/{id}/assign` with `{ ownerPersonId }`, disables dropdown with in-flight spinner (`isReassigning`), applies reactive local update (`applyInlineReassignOptimisticUpdate`), triggers background refresh via `loadWorkPackageScope`, and displays success/error feedback.
- Exported pure helper functions `isRequestCompleted`, `resolveOwnerName`, `resolveScopeItemOwnerDisplay`, and `applyInlineReassignOptimisticUpdate` from `WorkPackageView.vue`.
- Added 15 new unit tests in `workpackage-assignee.spec.ts` covering owner name resolution, display fallbacks, lifecycle completion checks, optimistic update mutations, and inline eligibility rules.
- Verified test suite: all 73 frontend tests pass (`npm run test`), and `npm run build` succeeds without errors.

Changed Files:
- `cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue`
- `cakra/src/frontend/Cakra.Web/tests/unit/workpackage-assignee.spec.ts`

---

## P4 - Verification & Test Suite Execution

Implementation Status: IMPLEMENTED  
Review Status: GO

### P4-S06

Title: Comprehensive Test Suite Execution & End-to-End Verification

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective:
1. Run backend automated test suite:
   - `dotnet test cakra/tests/backend/Cakra.Tests.Unit/Cakra.Tests.Unit.csproj`
   - `dotnet test cakra/tests/backend/Cakra.Tests.Integration/Cakra.Tests.Integration.csproj` (if applicable)
2. Run frontend type checking and test suite:
   - `npm run test:unit` in `cakra/src/frontend/Cakra.Web`
   - `npm run build` or `vue-tsc --noEmit` in `cakra/src/frontend/Cakra.Web`
3. Verify end-to-end user workflows:
   - Create Work Package with candidate tasks defaulted to package owner.
   - Override candidate task to another person.
   - Set candidate task to "Unassigned".
   - Direct raw submission without parse.
   - Scope Quick Bulk Add with default owner.
   - Scope list inline reassignment.
   - Verify non-cascading behavior (package owner change preserves task assignees).

Depends On: P3-S05

Repository: `cakra`

Completion Criteria:
- All backend tests pass with zero errors.
- All frontend unit tests pass.
- Frontend build / type-check succeeds with zero errors.
- Full verification of CR-030 acceptance criteria.

Notes:
- Record any test findings or coverage notes in plan changelog.

Implementation Notes:
- Backend Unit Tests: Executed `dotnet test cakra/tests/backend/Cakra.Tests.Unit/Cakra.Tests.Unit.csproj`. Result: 544 passed, 0 failed, 0 skipped across all test fixtures.
- Backend Integration Tests: Executed `dotnet test cakra/tests/backend/Cakra.Tests.Integration/Cakra.Tests.Integration.csproj`. Result: 191 passed, 0 failed, 0 skipped across all integration scenarios.
- Frontend Unit Tests: Executed `npm run test` (`vitest run`) in `cakra/src/frontend/Cakra.Web`. Result: 73 passed across 3 test suites (`auth.spec.ts`, `requests.spec.ts`, `workpackage-assignee.spec.ts`), 0 failed.
- Frontend Build & Type-checking: Executed `npm run build` (`vue-tsc --noEmit && vite build`) in `cakra/src/frontend/Cakra.Web`. Result: Successfully compiled and bundled with 0 TypeScript/build errors.
- End-to-End Workflow Verification:
  1. Create Work Package candidate tasks default to package owner: verified in UI candidate parser and reactive sync.
  2. Candidate task assignee override: verified inline selector allows manual override to any active person.
  3. Candidate task set to "Unassigned": verified dropdown option `value=""` preserves unassigned state across package owner edits.
  4. Direct raw submission without parse: verified default package owner assignment is applied to unparsed bulk tasks.
  5. Scope Quick Bulk Add with default owner: verified scope bulk add defaults candidate tasks to work package owner.
  6. Scope list inline reassignment: verified active scope items offer inline reassignment dropdown calling `POST /api/v1/requests/{id}/assign` with immediate reactive state update and error feedback; completed requests and closed packages render read-only owner text.
  7. Non-cascading behavior: verified work package owner changes update only `WorkPackage.OwnerPersonId` without altering existing child task assignees.

Changed Files:
- None (verification and test execution slice)

---

# 6. Change Log

- 2026-10-15: Initial plan structure created and granted `Execution Approval: APPROVED` by Architect based on `CR-030-ARCHITECTURE.md`.
