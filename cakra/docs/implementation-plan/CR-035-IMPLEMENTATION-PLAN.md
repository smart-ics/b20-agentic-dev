---
Title: Implementation Plan for Work Package Re-Open Capability (CR-035)
Code: CR-035
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-16
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-035`: Controlled capability to re-open a previously closed Work Package back into `DRAFT` state when constituent operational requests remain unfinished, enforcing domain invariants, cross-module status checks, Business Rule 9 conflict prevention, audit trails, and UI affordances across Cakra.

Deliver end-to-end realization across:
1. **Core Domain & Event Model (`Cakra.Modules.WorkPackage.Domain`)**:
   - Aggregate root mutation method `Reopen(reason, newDeadline, reopenedAtUtc)` on [`WorkPackage.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackage.cs) enforcing `CLOSED` $\rightarrow$ `DRAFT`, clearing `ClosedReason` and `ClosedAt`, and emitting domain event `WorkPackageReopened`.
   - Domain event `WorkPackageReopened` in `Cakra.Modules.WorkPackage.Domain.Events`.
2. **Application Service & MediatR Pipeline (`Cakra.Modules.WorkPackage.Services`)**:
   - `ReopenWorkPackageCommand` and FluentValidation validator in `WorkPackageCommands.cs`.
   - Multi-gate validation pipeline in `WorkPackageService.cs`:
     - **Authorization Gate**: Caller must be Work Package Owner, Management, or Administrator.
     - **Eligibility Gate**: Synchronous verification via `IRequestQueryService` that $\ge 1$ active constituent request has live status not in `['COMPLETED', 'CANCELLED', 'REJECTED']`. Packages with 0 requests or all finished requests are rejected.
     - **Integrity Gate (BR-9)**: Verify via `_workPackageRepository.IsRequestInActiveWorkPackageAsync` that no constituent unfinished request is currently claimed by another active or draft Work Package; reject with conflict details if detected.
3. **API Layer (`Cakra.Api`)**:
   - Dedicated endpoint `POST /api/v1/work-packages/{id}/reopen` in [`WorkPackagesController.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/WorkPackagesController.cs) mapping domain exceptions to standard HTTP error responses.
4. **Frontend API Client (`Cakra.Web`)**:
   - Type definitions `ReopenWorkPackagePayload` and client helper `reopenWorkPackage` in [`workpackages.ts`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/workpackages.ts).
5. **Frontend Screen (`Cakra.Web`)**:
   - Work Package view [`WorkPackageView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue) (`SCR-WP-001`):
     - Computed authorization and eligibility properties (`canReopen`, `hasUnfinishedRequests`, `reopenDisabledTooltip`).
     - Detail Panel action strip button "Re-Open Work Package" (disabled with tooltip when ineligible).
     - Interactive modal dialog featuring unfinished request count, expired-deadline warning alert, optional updated deadline picker, and mandatory justification textarea.
6. **Full-Stack Verification & Non-Regression**:
   - Comprehensive test suites across domain unit tests, service integration tests, controller API tests, and frontend build checks.

Planning Mode: FEATURE-PLANNING

Referenced artifacts:
- ISSUE: [CR-035-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-035-ISSUE.md)
- DOMAIN: [work-package-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/work-package-domain.md) (v1.2)
- FEASIBILITY-ASSESSMENT: [CR-035-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-035-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)
- ARCHITECTURE: [CR-035-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-035-ARCHITECTURE.md) (authoritative target architecture)

Architecture Applicability: ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers domain aggregate state transitions, domain events, application service command orchestration, cross-module request query validation, REST API controller endpoints, TypeScript client modules, Vue UI components, and full-stack automated testing:

1. **Backend Domain Layer (`Cakra.Modules.WorkPackage.Domain`)**:
   - Add `WorkPackageReopened` domain event record.
   - Implement `Reopen(string reason, DateTime? newDeadline = null, DateTime? reopenedAtUtc = null)` in `WorkPackage.cs`.
   - Update `WorkPackageStatus.cs` documentation and transition comments.
   - Add domain unit tests in `Cakra.Tests.Unit/WorkPackage/WorkPackageDomainTests.cs`.
2. **Backend Application Layer (`Cakra.Modules.WorkPackage.Services`)**:
   - Add `ReopenWorkPackageCommand` and `ReopenWorkPackageCommandValidator` in `WorkPackageCommands.cs`.
   - Add `ReopenWorkPackageAsync` method in `IWorkPackageService` and `WorkPackageService.cs`.
   - Implement 3 validation gates (Authorization, Unfinished Requests Eligibility via `IRequestQueryService`, and BR-9 Non-Conflict via `IWorkPackageRepository.IsRequestInActiveWorkPackageAsync`).
   - Implement MediatR handler for `ReopenWorkPackageCommand` in `WorkPackageService.cs`.
   - Add service unit tests in `Cakra.Tests.Unit/WorkPackage/WorkPackageServiceTests.cs`.
3. **Backend API Layer (`Cakra.Api`)**:
   - Implement `POST /api/v1/work-packages/{id}/reopen` in `WorkPackagesController.cs`.
   - Add integration tests in `Cakra.Tests.Integration/WorkPackage/WorkPackagesControllerTests.cs`.
4. **Frontend API Client (`Cakra.Web`)**:
   - Add `ReopenWorkPackagePayload` interface and `reopenWorkPackage` function in `src/api/workpackages.ts`.
5. **Frontend Presentation (`Cakra.Web`)**:
   - Add "Re-Open Work Package" action button, eligibility computations, and Re-Open modal dialog in `src/views/WorkPackageView.vue`.
   - Add frontend unit tests in `tests/unit/workpackage-reopen.spec.ts`.
6. **Full-Stack Verification**:
   - Execute complete backend test suite (`dotnet test`).
   - Execute frontend build and typecheck (`npm run build`).

---

# 3. Dependencies

- .NET 8 SDK & ASP.NET Core (`Cakra.Api`, `Cakra.Modules.WorkPackage`)
- MediatR & FluentValidation in `Cakra.Modules.WorkPackage`
- Node.js & npm (Vue 3 / Vite / TypeScript in `src/frontend/Cakra.Web`)
- Existing database tables `[workpackage].[WorkPackages]` and `[workpackage].[WorkPackageRequests]` (zero migrations required)

For slice dependencies:
- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires the referenced slice to have implementation status `IMPLEMENTED`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Core Domain & Event Model | NOT-STARTED | GO | 1/1 |
| P2 - Application Service, Command Pipeline & REST API | NOT-STARTED | GO | 2/2 |
| P3 - Frontend API Client & Work Package Screen | NOT-STARTED | GO | 2/2 |
| P4 - Full-Stack Verification & Non-Regression | NOT-STARTED | GO | 1/1 |

---

# 5. Phases

## P1 - Core Domain & Event Model

Implementation Status: NOT-STARTED
Review Status: GO

### P1-S01

Title: Implement WorkPackage.Reopen Aggregate Method and WorkPackageReopened Domain Event

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Implement the aggregate lifecycle transition from `CLOSED` to `DRAFT` in `WorkPackage.cs`, create the `WorkPackageReopened` domain event, update `WorkPackageStatus.cs`, and add domain unit tests.

Depends On: None

Repository: cakra

Completion Criteria:
- `WorkPackageReopened.cs` created in `Cakra.Modules.WorkPackage.Domain.Events` with `Guid WorkPackageId`, `string Reason`, `string? PreviousClosedReason`, `DateTime? PreviousClosedAt`, `DateTime ReopenedAtUtc`.
- `WorkPackage.cs` updated with `public void Reopen(string reason, DateTime? newDeadline = null, DateTime? reopenedAtUtc = null)`:
  - Throws `InvalidWorkPackageStateTransitionException` if `Status != WorkPackageStatus.Closed`.
  - Throws `ArgumentException` if `reason` is null, empty, or whitespace.
  - Sets `Status = WorkPackageStatus.Draft`.
  - Sets `ClosedReason = null` and `ClosedAt = null`.
  - If `newDeadline.HasValue`, sets `Deadline = NormalizeDeadline(newDeadline)`.
  - Sets `UpdatedAt = timestamp`.
  - Adds domain event `WorkPackageReopened`.
- `WorkPackageStatus.cs` docstrings updated to document `CLOSED` $\rightarrow$ `DRAFT` transition.
- Unit tests in `Cakra.Tests.Unit/WorkPackage/WorkPackageDomainTests.cs` verifying successful transition from `CLOSED` to `DRAFT`, clearing of closure fields, deadline update, event emission, and rejection of calls when in `DRAFT` or `ACTIVE` state.

Notes:
- Adheres strictly to Architecture §4 TD-001 and TD-002.
- Implemented aggregate lifecycle transition from `CLOSED` to `DRAFT` in `WorkPackage.Reopen(reason, newDeadline, reopenedAtUtc)`.
- Implemented domain event `WorkPackageReopened` in `Cakra.Modules.WorkPackage.Domain.Events`.
- Updated `WorkPackageStatus.cs` docstrings documenting the `CLOSED` -> `DRAFT` transition.
- Added comprehensive unit tests in `WorkPackageDomainTests.cs` verifying valid state transitions, clearing of closure fields, deadline normalization and preservation, domain event emissions, and guard clause exceptions for DRAFT/ACTIVE states and empty reasons.
- Changed files:
  - `src/backend/Cakra.Modules.WorkPackage/Domain/Events/WorkPackageReopened.cs`
  - `src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackage.cs`
  - `src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackageStatus.cs`
  - `tests/backend/Cakra.Tests.Unit/WorkPackage/WorkPackageDomainTests.cs`

---

## P2 - Application Service, Command Pipeline & REST API

Implementation Status: NOT-STARTED
Review Status: GO

### P2-S02

Title: Implement ReopenWorkPackageCommand, Validator, and Multi-Gate Service Orchestration

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Implement `ReopenWorkPackageCommand`, FluentValidation validator, MediatR handler, and the 3-gate validation pipeline in `WorkPackageService.cs` ensuring authorization, live unfinished request eligibility, and Business Rule 9 conflict prevention.

Depends On: P1-S01

Repository: cakra

Completion Criteria:
- `ReopenWorkPackageCommand` and `ReopenWorkPackageCommandValidator` defined in `WorkPackageCommands.cs`.
- `IWorkPackageService` updated with `Task<WorkPackageDto> ReopenWorkPackageAsync(Guid workPackageId, string reason, DateTime? newDeadline = null, CancellationToken cancellationToken = default)`.
- `WorkPackageService.cs` implements `IRequestHandler<ReopenWorkPackageCommand, WorkPackageDto>` and `ReopenWorkPackageAsync`:
  - **Gate 1 (Authorization)**: Validates caller is Work Package Owner, Management, or Administrator. Throws `UnauthorizedAccessException` otherwise.
  - **Gate 2 (Eligibility)**: Resolves constituent requests via `_requestQueryService.GetRequestByIdAsync(id)`; verifies $\ge 1$ request has live status not in `['COMPLETED', 'CANCELLED', 'REJECTED']`. Throws `WorkPackageDomainException` if package is empty or all requests are completed/cancelled.
  - **Gate 3 (Integrity / BR-9)**: Queries `_workPackageRepository.IsRequestInActiveWorkPackageAsync(req.Id, workPackage.Id)` for each unfinished request. If claimed by another active/draft package, throws `InvalidOperationException` listing conflicting request titles and IDs.
  - Mutates aggregate via `workPackage.Reopen(reason, newDeadline, now)`.
  - Persists changes via `_workPackageRepository.UpdateAsync(workPackage, cancellationToken)`.
  - Dispatches domain events via `DispatchDomainEventsAsync`.
  - Returns updated `WorkPackageDto`.
- Unit tests in `Cakra.Tests.Unit/WorkPackage/WorkPackageServiceTests.cs` covering all 3 validation gates and happy path reopening.

Notes:
- Adheres strictly to Architecture §4 TD-003.
- Implemented `ReopenWorkPackageCommand` and `ReopenWorkPackageCommandValidator` in `WorkPackageCommands.cs`.
- Added `ReopenWorkPackageAsync` and alias `ReopenWorkPackage` in `IWorkPackageService`.
- Implemented 3-gate validation pipeline and MediatR command handler in `WorkPackageService.cs`:
  - Gate 1 (Authorization): Validates caller is Work Package Owner, Management, or Administrator, throwing `UnauthorizedAccessException` otherwise.
  - Gate 2 (Eligibility): Resolves constituent requests via `_requestQueryService.GetRequestByIdAsync`, verifying $\ge 1$ request has live status not in `['COMPLETED', 'CANCELLED', 'REJECTED']`, throwing `WorkPackageDomainException` if empty or all finished.
  - Gate 3 (Integrity / BR-9): Queries `_workPackageRepository.IsRequestInActiveWorkPackageAsync`, throwing `InvalidOperationException` with conflicting request titles and IDs if claimed by another active/draft package.
  - Mutates aggregate via `workPackage.Reopen(reason, newDeadline, now)`, persists changes, dispatches domain events, and returns `WorkPackageDto`.
- Added comprehensive unit tests in `Cakra.Tests.Unit/WorkPackage/WorkPackageServiceTests.cs` covering all 3 validation gates, command validators, deadline preservation, MediatR handling, and happy path reopening.
- Changed files:
  - `src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageCommands.cs`
  - `src/backend/Cakra.Modules.WorkPackage/Services/IWorkPackageService.cs`
  - `src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageService.cs`
  - `tests/backend/Cakra.Tests.Unit/WorkPackage/RequestRecordedWorkPackageHandlerTests.cs`
  - `tests/backend/Cakra.Tests.Unit/WorkPackage/WorkPackageServiceTests.cs`

---

### P2-S03

Title: Expose REST API Endpoint POST /api/v1/work-packages/{id}/reopen in WorkPackagesController

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Add `POST /api/v1/work-packages/{id}/reopen` endpoint to `WorkPackagesController.cs`, wire request body model, map domain exceptions to standard HTTP error codes, and verify with integration tests.

Depends On: P2-S02

Repository: cakra

Completion Criteria:
- `POST /api/v1/work-packages/{id}/reopen` action added in `WorkPackagesController.cs`:
  - Accepts `ReopenWorkPackageBody(string? Reason, DateTime? Deadline)`.
  - Sends `ReopenWorkPackageCommand`.
  - Returns `200 OK` with enriched `WorkPackageDto`.
  - Returns `400 Bad Request` for invalid state transition, validation errors, eligibility failures, or BR-9 conflicts.
  - Returns `401/403` for unauthorized callers.
  - Returns `404 Not Found` for non-existent packages.
- Controller integration tests added in `Cakra.Tests.Integration/WorkPackage/WorkPackagesControllerTests.cs` validating endpoint behavior and error mappings.

Notes:
- Adheres strictly to Architecture §4 TD-004.
- Implemented `POST /api/v1/work-packages/{id}/reopen` in `WorkPackagesController.cs` accepting `ReopenWorkPackageBody` (alias `ReopenWorkPackageRequest`).
- Wired command dispatching to `ReopenWorkPackageCommand` via MediatR and enriches response via `IWorkPackageQueryService`.
- Mapped domain exceptions to RFC 7807 ProblemDetails: 400 Bad Request for state transitions, validation, eligibility failures, or BR-9 conflicts; 401 Unauthorized for unauthorized callers; and 404 Not Found for non-existent packages.
- Added comprehensive controller integration tests in `Cakra.Tests.Integration/WorkPackage/WorkPackagesControllerTests.cs` verifying happy path reopening, state persistence, input validation, 404 on missing package, draft/active rejection, 0-request and all-finished eligibility failures, BR-9 conflict rejection, and unauthorized caller rejection.
- Changed files:
  - `src/backend/Cakra.Api/Controllers/WorkPackagesController.cs`
  - `tests/backend/Cakra.Tests.Integration/WorkPackage/WorkPackagesControllerTests.cs`

---

## P3 - Frontend API Client & Work Package Screen

Implementation Status: NOT-STARTED
Review Status: GO

### P3-S04

Title: Extend Frontend API Client in workpackages.ts with Reopen Endpoint

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Implement `reopenWorkPackage` API client function and `ReopenWorkPackagePayload` interface in `src/frontend/Cakra.Web/src/api/workpackages.ts`.

Depends On: P2-S03

Repository: cakra

Completion Criteria:
- Export `interface ReopenWorkPackagePayload { reason: string; deadline?: string | null }` in `workpackages.ts`.
- Export `async function reopenWorkPackage(id: string, payload: ReopenWorkPackagePayload): Promise<WorkPackageDto>` issuing `POST /work-packages/${id}/reopen`.
- Export `reopenWorkPackage` in default and named `workPackageService` exports.

Notes:
- Adheres strictly to Architecture §4 TD-005.
- Exported `ReopenWorkPackagePayload` interface in `src/frontend/Cakra.Web/src/api/workpackages.ts`.
- Exported `reopenWorkPackage` function issuing `POST /work-packages/${id}/reopen`.
- Exported `reopenWorkPackage` in both named `workPackageService` and `default` exports.
- Added comprehensive unit tests in `src/frontend/Cakra.Web/tests/unit/workpackages.spec.ts` verifying HTTP payload delivery, path routing, and service exports.
- Verified test suite (`npm test`, 10/10 test files passed, 222 tests passed) and production build (`npm run build`, vue-tsc + vite build cleanly succeeded).
- Changed files:
  - `src/frontend/Cakra.Web/src/api/workpackages.ts`
  - `src/frontend/Cakra.Web/tests/unit/workpackages.spec.ts`

---

### P3-S05

Title: Implement Re-Open Action Button and Modal Dialog on SCR-WP-001 (WorkPackageView.vue)

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Update `WorkPackageView.vue` with computed authorization and eligibility properties, an action strip button in the Detail Panel with explanatory disabled tooltips, and an interactive Re-Open Modal dialog with expired-deadline warning alert.

Depends On: P3-S04

Repository: cakra

Completion Criteria:
- `WorkPackageView.vue` computed properties:
  - `isSelectedClosed`: `selectedStatus.value === 'CLOSED'`.
  - `canReopen`: user has role `Administrator`, `Management`, or is `ownerPersonId`.
  - `hasUnfinishedRequests`: active scope items contain at least one item with status not in `['COMPLETED', 'CANCELLED', 'REJECTED']`.
  - `isReopenEligible`: equals `hasUnfinishedRequests.value`.
  - `reopenDisabledTooltip`: displays *"Cannot re-open: all requests in this package are completed or cancelled."* when ineligible.
- Action strip button:
  - Rendered when `isSelectedClosed && canReopen` with `data-testid="wp-reopen-button"`.
  - Disabled when `!isReopenEligible`, showing tooltip.
  - Clicking opens the Re-Open Modal.
- Re-Open Modal Dialog (`data-testid="wp-reopen-modal"`):
  - Displays count of unfinished requests remaining in scope.
  - If `selectedWorkPackage.deadline && new Date(selectedWorkPackage.deadline) < new Date()`, displays warning alert banner that the target deadline has expired.
  - Date input allowing optional updated deadline.
  - Mandatory reason textarea (`data-testid="wp-reopen-reason-input"`).
  - Confirm button (`data-testid="wp-reopen-confirm-button"`) with loading spinner and disabled state when reason is empty.
  - Error alert displaying backend error messages (including BR-9 conflicts).
  - On success: updates local package state, table row, detail panel, closes modal, and shows success notification banner.
- Unit tests added in `src/frontend/Cakra.Web/tests/unit/workpackage-reopen.spec.ts`.

Notes:
- Adheres strictly to Architecture §4 TD-006.
- Updated `WorkPackageView.vue`:
  - Exported pure helper functions `canUserReopenWorkPackage`, `hasUnfinishedWorkPackageRequests`, `countUnfinishedWorkPackageRequests`, and `isWorkPackageDeadlineExpired`.
  - Implemented computed properties: `isSelectedClosed`, `canReopen`, `hasUnfinishedRequests`, `isReopenEligible`, `reopenDisabledTooltip`, `unfinishedRequestsCount`, and `isDeadlineExpired`.
  - Added Re-Open action button in the Detail Panel lifecycle action strip (`data-testid="wp-reopen-button"`), rendered when `isSelectedClosed && canReopen`, disabled with tooltip when ineligible, with ineligible note (`data-testid="wp-reopen-ineligible-note"`).
  - Implemented interactive Re-Open Modal dialog (`data-testid="wp-reopen-modal"`) featuring:
    - Context card with remaining unfinished requests count (`data-testid="wp-reopen-unfinished-count"`).
    - Expired target deadline warning alert (`data-testid="wp-reopen-deadline-warning"`).
    - Revised target deadline date picker (`data-testid="wp-reopen-deadline-input"`).
    - Mandatory justification reason textarea (`data-testid="wp-reopen-reason-input"`).
    - Error alert (`data-testid="wp-reopen-error-alert"`) handling backend error messages including BR-9 conflicts.
    - Confirm button (`data-testid="wp-reopen-confirm-button"`) with loading spinner and disabled state when empty.
    - On confirmation: invokes `reopenWorkPackage`, updates local state and table rows, synchronizes detail forms, closes modal, and displays success notification banner.
- Added comprehensive unit test suite in `src/frontend/Cakra.Web/tests/unit/workpackage-reopen.spec.ts` (33 tests covering authorization, eligibility, deadline checks, and UI affordance contracts).
- Verified full frontend test suite (`npm test`, 11/11 test files passed, 255 tests passed) and production build (`npm run build`, clean success).
- Changed files:
  - `src/frontend/Cakra.Web/src/views/WorkPackageView.vue`
  - `src/frontend/Cakra.Web/tests/unit/workpackage-reopen.spec.ts`

---

## P4 - Full-Stack Verification & Non-Regression

Implementation Status: NOT-STARTED
Review Status: GO

### P4-S06

Title: Execute End-to-End Build, Test Suites, and Non-Regression Verification

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Run all backend tests, frontend unit tests, and frontend build commands to verify complete full-stack integration and guarantee zero regressions.

Depends On: P3-S05

Repository: cakra

Completion Criteria:
- Backend solution builds cleanly without warnings or errors (`dotnet build Cakra.sln`).
- Full backend test suite passes completely (`dotnet test Cakra.sln`).
- Frontend unit test suite passes (`npm run test:unit`).
- Frontend production build succeeds (`npm run build`).
- Verify complete lifecycle loop: `DRAFT` $\rightarrow$ `ACTIVE` $\rightarrow$ `CLOSED` $\rightarrow$ `DRAFT` $\rightarrow$ `ACTIVE`.
- Verify rejection of packages with 0 requests or all completed requests.
- Verify BR-9 cross-package conflict rejection.
- Zero regressions on Operations Cockpit (`/operations/cockpit`) or existing Work Package cards and tables.

Notes:
- Executed full backend solution build: `dotnet build Cakra.sln` -> Build succeeded with 0 warnings and 0 errors.
- Executed full backend test suite: `dotnet test Cakra.sln` -> Passed! 848 tests executed, 848 passed, 0 failed, 0 skipped:
  - `Cakra.Tests.Unit.dll`: 644/644 passed.
  - `Cakra.Tests.Integration.dll`: 204/204 passed.
- Executed full frontend test suite: `npm test` -> 11/11 test files passed, 255/255 tests passed.
- Executed frontend production build: `npm run build` (`vue-tsc --noEmit && vite build`) -> Built cleanly in 3.86s.
- Verified domain lifecycle transitions (`DRAFT` -> `ACTIVE` -> `CLOSED` -> `DRAFT` -> `ACTIVE`), eligibility guards, and BR-9 cross-package conflict enforcement.
- Confirmed zero regressions across Operations Cockpit and Work Package cards and tables.
- Changed files:
  - None (verification and test execution slice).

---

# 6. Change Log

- 2026-10-16: Initial creation of implementation plan for `CR-035` by Architect (`ica-architect`). Released for execution with `Execution Approval: APPROVED`.
