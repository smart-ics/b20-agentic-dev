---
Title: Implementation Plan for Target Deadline Date in Work Package Aggregate and Screen (CR-023)
Code: CR-023
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-07
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-023`: Optional target deadline date for the Work Package aggregate root (`WorkPackage.cs`), database schema, Dapper persistence and query services, application commands, REST API endpoints, and screen `SCR-WP-001` (`WorkPackageView.vue`).

Deliver end-to-end realization across:
1. Database schema migration `0017_add_workpackage_deadline.sql` adding `[Deadline] DATETIME2 NULL` and a filtered nonclustered index `[IX_WorkPackages_Deadline]` to `[workpackage].[WorkPackages]`.
2. Property `Deadline` on `WorkPackage` aggregate root with UTC date-only normalization and active lifecycle state gating (`DRAFT` and `ACTIVE` mutable, `CLOSED` strictly immutable).
3. Persistence query mapping and hydration in `WorkPackageRepository.cs` and `WorkPackageQueryService.cs`.
4. Application commands (`CreateWorkPackageCommand`, new `UpdateWorkPackageDeadlineCommand`), handlers, DTO contracts (`WorkPackageDto.Deadline`), and REST endpoint `PUT /api/v1/work-packages/{id}/deadline` on `WorkPackagesController.cs`.
5. Frontend API helpers in `src/frontend/Cakra.Web/src/api/workpackages.ts`.
6. UI enhancements in `src/frontend/Cakra.Web/src/views/WorkPackageView.vue`:
   - Optional deadline input in the "Create Work Package" modal.
   - "Deadline" column in the Work Package list table with formatted date presentation.
   - Target deadline metadata display and inline edit/clear controls in the Detail Panel.
   - Dynamic red "Overdue" badge calculation when `Deadline < Today` for open packages (`DRAFT` or `ACTIVE`), automatically hidden when `CLOSED`.

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- ISSUE: [CR-023-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-023-ISSUE.md)
- FEASIBILITY-ASSESSMENT: [CR-023-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-023-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)
- ARCHITECTURE: [CR-023-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-023-ARCHITECTURE.md) (authoritative target architecture)

Architecture Applicability: ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers all database migrations, domain modeling, persistence updates, application commands, REST endpoints, frontend screens, and build verifications required to realize `CR-023`:

1. **Database Migration (`Cakra.Api`)**:
   - Create idempotent DbUp script `0017_add_workpackage_deadline.sql` adding `[Deadline] DATETIME2 NULL` and filtered index `[IX_WorkPackages_Deadline]` to `[workpackage].[WorkPackages]`.
2. **Domain Aggregate (`Cakra.Modules.WorkPackage`)**:
   - Add property `Deadline` (`DateTime?`, UTC) to `WorkPackage.cs`.
   - Add `NormalizeDeadline` helper ensuring UTC midnight (`00:00:00Z`).
   - Extend `Create(...)` and `Rehydrate(...)` factory methods with optional `DateTime? deadline = null`.
   - Add domain method `UpdateDeadline(DateTime? deadline, DateTime? updatedAtUtc = null)` validating that `Status != WorkPackageStatus.Closed` and updating `UpdatedAt`.
3. **Persistence Layer & Query Service (`Cakra.Modules.WorkPackage`)**:
   - Update `WorkPackageRepository.cs` queries (`SELECT`, `INSERT`, `UPDATE`), `WorkPackageRow`, and hydration logic to map `Deadline`.
   - Update `WorkPackageQueryService.cs` queries (`GetWorkPackageByIdAsync`, `ListWorkPackagesAsync`, `GetRequestWorkPackageAsync`), `WorkPackageQueryRow`, and `ToDto` mapping.
4. **Application Commands & API Contracts (`Cakra.Modules.WorkPackage` & `Cakra.Api`)**:
   - Add `DateTime? Deadline` to `WorkPackageDto`.
   - Extend `CreateWorkPackageCommand` with `DateTime? Deadline = null`.
   - Create `UpdateWorkPackageDeadlineCommand(Guid WorkPackageId, DateTime? Deadline)` and validator.
   - Implement `UpdateDeadlineAsync` and command handlers in `WorkPackageService.cs`.
   - Extend `CreateWorkPackageBody` and add `UpdateWorkPackageDeadlineBody` in `WorkPackagesController.cs`.
   - Expose endpoint `PUT /api/v1/work-packages/{id}/deadline`.
5. **Frontend API Client & Screen (`Cakra.Web`)**:
   - Extend TypeScript interfaces in `src/frontend/Cakra.Web/src/api/workpackages.ts` and add `updateWorkPackageDeadline(...)`.
   - Update `WorkPackageView.vue`:
     - Add deadline input in Create Work Package modal.
     - Add Deadline column in Work Packages table.
     - Add deadline display and inline edit/clear controls in Detail Panel.
     - Add `isWorkPackageOverdue` helper and red "Overdue" badges for open packages past deadline.
6. **Full-Stack Verification**:
   - Run backend test suites (`dotnet test`).
   - Run frontend typecheck and production build (`npm run build`).

---

# 3. Dependencies

- .NET 8 SDK & ASP.NET Core (`Cakra.Api`, `Cakra.Modules.WorkPackage`)
- SQL Server schema migration pipeline (DbUp in `Cakra.Api.Infrastructure.Migrations`)
- Dapper object mapping in `Cakra.Modules.WorkPackage.Persistence`
- Node.js & npm (Vue 3 / Vite / TypeScript in `src/frontend/Cakra.Web`)

For slice dependencies:
- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires the referenced slice to have implementation status `IMPLEMENTED`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Database Schema & Domain Aggregate | NOT-STARTED | NOT-REVIEWED | 0/2 |
| P2 - Persistence & Application Layer | NOT-STARTED | NOT-REVIEWED | 0/2 |
| P3 - Frontend API & Work Package View (SCR-WP-001) | NOT-STARTED | NOT-REVIEWED | 0/2 |
| P4 - Verification & Test Validation | NOT-STARTED | NOT-REVIEWED | 0/1 |

---

# 5. Phases

## P1 - Database Schema & Domain Aggregate

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

### P1-S01

Title: Database Migration Script for Work Package Deadline

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Create an idempotent DbUp SQL migration script `0017_add_workpackage_deadline.sql` in `cakra/src/backend/Cakra.Api/Migrations/Scripts/` that adds `[Deadline] DATETIME2 NULL` and a filtered nonclustered index `[IX_WorkPackages_Deadline]` on `[workpackage].[WorkPackages]`.

Depends On: None

Repository: cakra

Completion Criteria:
- File `cakra/src/backend/Cakra.Api/Migrations/Scripts/0017_add_workpackage_deadline.sql` exists and conforms to DbUp idempotency rules.
- Guarded by `OBJECT_ID`, `COL_LENGTH`, and `sys.indexes` existence checks.
- Compatible with existing `0007_workpackage_tables.sql` and `0014_workpackage_requests_sort_order.sql`.

Notes:
- Matches TD-001 in `CR-023-ARCHITECTURE.md`.
- Implementation: Created idempotent migration script `0017_add_workpackage_deadline.sql` with schema existence, column existence, and index existence guards. Filtered nonclustered index created conditionally where `[Deadline] IS NOT NULL`.
- Changed Files:
  - `cakra/src/backend/Cakra.Api/Migrations/Scripts/0017_add_workpackage_deadline.sql`

---

### P1-S02

Title: Domain Property, Normalization, and Mutation Method on WorkPackage

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
In `cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackage.cs`:
1. Add property `public DateTime? Deadline { get; private set; }`.
2. Add private static helper `NormalizeDeadline(DateTime? deadline)` normalizing non-null dates to UTC midnight (`00:00:00Z`).
3. Update `Create(...)` to accept `DateTime? deadline = null` and initialize `Deadline = NormalizeDeadline(deadline)`.
4. Update `Rehydrate(...)` to accept `DateTime? deadline = null` and assign `Deadline = NormalizeDeadline(deadline)`.
5. Add domain method `UpdateDeadline(DateTime? deadline, DateTime? updatedAtUtc = null)`:
   - Throws `WorkPackageDomainException` if `Status == WorkPackageStatus.Closed`.
   - Normalizes new deadline value to UTC midnight.
   - If changed, sets `Deadline` and updates `UpdatedAt = updatedAtUtc ?? DateTime.UtcNow`.
6. Add unit test coverage in `Cakra.Tests.Unit/WorkPackage/` validating normalization, creation, deadline updates, and closed state guard rules.

Depends On: None

Repository: cakra

Completion Criteria:
- `WorkPackage.Deadline` property exists and enforces UTC midnight normalization.
- `WorkPackage.Create(...)` and `WorkPackage.Rehydrate(...)` accept `DateTime? deadline`.
- `WorkPackage.UpdateDeadline(...)` updates deadline and `UpdatedAt`, but throws `WorkPackageDomainException` on closed work packages.
- Unit tests verify domain behavior.

Notes:
- Matches TD-002 in `CR-023-ARCHITECTURE.md`.
- Implementation:
  - Added property `Deadline` (`DateTime?`, private setter) on `WorkPackage`.
  - Added private static method `NormalizeDeadline(DateTime? deadline)` normalizing to UTC midnight (`00:00:00Z`).
  - Updated factory `Create(...)` to accept optional `DateTime? deadline = null` and initialize `Deadline = NormalizeDeadline(deadline)`.
  - Updated `Rehydrate(...)` to accept optional `DateTime? deadline = null` and assign `Deadline = NormalizeDeadline(deadline)`.
  - Implemented domain method `UpdateDeadline(DateTime? deadline, DateTime? updatedAtUtc = null)` guarding against closed state (`WorkPackageDomainException`), normalizing inputs to UTC midnight, and conditionally updating `Deadline` and `UpdatedAt` when changed.
  - Added comprehensive unit tests in `WorkPackageDeadlineDomainTests.cs` covering creation with/without deadline, normalization to UTC midnight across time zones/kinds, draft and active state mutations, clearing existing deadline to null, no-op update without timestamp drift, closed package guard invariants, and rehydration normalization.
- Changed Files:
  - `cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackage.cs`
  - `cakra/tests/backend/Cakra.Tests.Unit/WorkPackage/WorkPackageDeadlineDomainTests.cs`

---

## P2 - Persistence & Application Layer

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

### P2-S03

Title: Persistence Layer Queries & Dapper Hydration for Work Package Deadline

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
1. In `cakra/src/backend/Cakra.Modules.WorkPackage/Persistence/WorkPackageRepository.cs`:
   - Include `[Deadline]` in `packageSql` and `packagesSql` column projections from `[workpackage].[WorkPackages]`.
   - Include `[Deadline]` in `INSERT INTO [workpackage].[WorkPackages]` column list and parameter mapping in `AddAsync`.
   - Include `[Deadline] = @Deadline` in `UPDATE [workpackage].[WorkPackages]` parameter mapping in `UpdateAsync`.
   - Add `public DateTime? Deadline { get; init; }` to `WorkPackageRow` and pass `Deadline` into `Domain.WorkPackage.Rehydrate(...)`.
2. In `cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageQueryService.cs`:
   - Include `wp.[Deadline]` in `GetWorkPackageByIdAsync`, `ListWorkPackagesAsync`, and `GetRequestWorkPackageAsync` SQL queries.
   - Add `public DateTime? Deadline { get; init; }` to `WorkPackageQueryRow` and map `Deadline = Deadline` in `ToDto(...)`.

Depends On: P1-S01, P1-S02

Repository: cakra

Completion Criteria:
- `WorkPackageRepository.cs` and `WorkPackageQueryService.cs` compile cleanly.
- `WorkPackageRepository` persists and hydrates `Deadline`.
- `WorkPackageQueryService` projects `Deadline` in query methods.

Notes:
- Matches TD-003 in `CR-023-ARCHITECTURE.md`.
- Implementation:
  - Updated `WorkPackageRepository.cs`:
    - Added `[Deadline]` column projection to `packageSql` and `packagesSql`.
    - Added `[Deadline]` to `INSERT INTO [workpackage].[WorkPackages]` column list and parameter mapping in `AddAsync`.
    - Added `[Deadline] = @Deadline` to `UPDATE [workpackage].[WorkPackages]` and parameter mapping in `UpdateAsync`.
    - Added `public DateTime? Deadline { get; init; }` to `WorkPackageRow` and passed `Deadline` to `Domain.WorkPackage.Rehydrate(...)`.
  - Updated `WorkPackageQueryService.cs`:
    - Added `wp.[Deadline]` to `GetWorkPackageByIdAsync`, `ListWorkPackagesAsync`, and `GetRequestWorkPackageAsync` SQL query statements.
    - Added `public DateTime? Deadline { get; init; }` to `WorkPackageQueryRow` and mapped `Deadline = Deadline` in `ToDto(...)`.
  - Updated `WorkPackageDto.cs`:
    - Added `public DateTime? Deadline { get; init; }` to `WorkPackageDto` and mapped `Deadline = workPackage.Deadline` in `FromDomain(...)`.
  - Backend and unit test verification succeeded with 0 errors and 445 passed unit tests.
- Changed Files:
  - `cakra/src/backend/Cakra.Modules.WorkPackage/Persistence/WorkPackageRepository.cs`
  - `cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageQueryService.cs`
  - `cakra/src/backend/Cakra.Modules.WorkPackage/Models/WorkPackageDto.cs`

---

### P2-S04

Title: Application Commands, Handlers, DTOs & REST API Endpoints for Work Package Deadline

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
1. In `cakra/src/backend/Cakra.Modules.WorkPackage/Models/WorkPackageDto.cs`:
   - Add `public DateTime? Deadline { get; init; }` to `WorkPackageDto`.
   - Map `Deadline = workPackage.Deadline` in `FromDomain(Domain.WorkPackage workPackage)`.
2. In `cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageCommands.cs`:
   - Update `CreateWorkPackageCommand` with `DateTime? Deadline = null`.
   - Create `UpdateWorkPackageDeadlineCommand(Guid WorkPackageId, DateTime? Deadline) : IRequest<WorkPackageDto>`.
   - Create `UpdateWorkPackageDeadlineCommandValidator` ensuring `WorkPackageId` is not empty.
3. In `cakra/src/backend/Cakra.Modules.WorkPackage/Services/IWorkPackageService.cs` and `WorkPackageService.cs`:
   - Update `CreateWorkPackageAsync` to accept optional `DateTime? deadline = null`.
   - Add method `UpdateDeadlineAsync(Guid workPackageId, DateTime? deadline, CancellationToken cancellationToken = default)`.
   - Implement `IRequestHandler<UpdateWorkPackageDeadlineCommand, WorkPackageDto>` in `WorkPackageService`.
4. In `cakra/src/backend/Cakra.Api/Controllers/WorkPackagesController.cs`:
   - Update `CreateWorkPackageBody` to include `public DateTime? Deadline { get; set; }`.
   - Add `UpdateWorkPackageDeadlineBody` with `public DateTime? Deadline { get; set; }`.
   - Add endpoint `[HttpPut("{id:guid}/deadline")]` dispatching `UpdateWorkPackageDeadlineCommand`.
5. Add unit/integration tests for the new command and endpoint in `Cakra.Tests.Unit` and/or `Cakra.Tests.Integration`.

Depends On: P1-S02, P2-S03

Repository: cakra

Completion Criteria:
- `CreateWorkPackageCommand` accepts optional `Deadline`.
- `UpdateWorkPackageDeadlineCommand` updates target deadline via `WorkPackageService`.
- `PUT /api/v1/work-packages/{id}/deadline` returns 200 OK with updated `WorkPackageDto`, 400 Bad Request on invalid arguments/closed package, and 404 Not Found if missing.
- Backend builds cleanly (`dotnet build`).

Notes:
- Matches TD-004 in `CR-023-ARCHITECTURE.md`.
- Implementation:
  - Updated `CreateWorkPackageCommand` in `WorkPackageCommands.cs` with optional `DateTime? Deadline = null`.
  - Added `UpdateWorkPackageDeadlineCommand` and `UpdateWorkPackageDeadlineCommandValidator` ensuring non-empty `WorkPackageId`.
  - Updated `IWorkPackageService` and `WorkPackageService`:
    - Added optional `DateTime? deadline = null` to `CreateWorkPackageAsync` / `CreateWorkPackage` and passed it to aggregate root factory.
    - Added `UpdateDeadlineAsync` / `UpdateDeadline` method updating domain deadline, persisting changes, and dispatching domain events.
    - Implemented MediatR `IRequestHandler<UpdateWorkPackageDeadlineCommand, WorkPackageDto>` and updated `CreateWorkPackageCommand` handler to forward deadline.
  - Updated `WorkPackagesController.cs`:
    - Added `Deadline` to `CreateWorkPackageBody` and forwarded it in `CreateWorkPackage` action.
    - Added `UpdateWorkPackageDeadlineBody` with `public DateTime? Deadline { get; set; }`.
    - Added `[HttpPut("{id:guid}/deadline")]` endpoint dispatching `UpdateWorkPackageDeadlineCommand` and handling domain/validation exceptions.
  - Added unit test coverage in `WorkPackageServiceTests.cs` validating deadline creation, updates, clearing, closed aggregate guard exceptions, empty ID validations, and command validator rules.
  - Added integration test coverage in `WorkPackagesControllerTests.cs` verifying 401 Unauthorized protection, 201 Created with deadline, 200 OK on GET/PUT deadline, 200 OK clearing deadline to null, 404 Not Found on missing work packages, and 400 Bad Request on closed work packages.
- Changed Files:
  - `cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageCommands.cs`
  - `cakra/src/backend/Cakra.Modules.WorkPackage/Services/IWorkPackageService.cs`
  - `cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageService.cs`
  - `cakra/src/backend/Cakra.Api/Controllers/WorkPackagesController.cs`
  - `cakra/tests/backend/Cakra.Tests.Unit/WorkPackage/WorkPackageServiceTests.cs`
  - `cakra/tests/backend/Cakra.Tests.Integration/WorkPackage/WorkPackagesControllerTests.cs`

---

## P3 - Frontend API & Work Package View (SCR-WP-001)

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

### P3-S05

Title: Frontend API Client Support for Work Package Deadline

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
In `cakra/src/frontend/Cakra.Web/src/api/workpackages.ts`:
1. Add `deadline?: string | null` to `WorkPackageDto`.
2. Add `deadline?: string | null` to `CreateWorkPackagePayload`.
3. Add interface `UpdateWorkPackageDeadlinePayload { deadline: string | null }`.
4. Add and export function `updateWorkPackageDeadline(id: string, payload: UpdateWorkPackageDeadlinePayload): Promise<WorkPackageDto>`.
5. Add `updateWorkPackageDeadline` to exported `workPackageService` object.

Depends On: P2-S04

Repository: cakra

Completion Criteria:
- `workpackages.ts` provides strongly typed support for reading, creating, and updating work package deadlines.
- TypeScript compiles cleanly without errors.

Notes:
- Matches TD-005 in `CR-023-ARCHITECTURE.md`.
- Implementation:
  - Added optional `deadline?: string | null` property to `WorkPackageDto`.
  - Added optional `deadline?: string | null` property to `CreateWorkPackagePayload`.
  - Added `UpdateWorkPackageDeadlinePayload` interface with `deadline: string | null`.
  - Implemented and exported `updateWorkPackageDeadline(id: string, payload: UpdateWorkPackageDeadlinePayload): Promise<WorkPackageDto>` dispatching `PUT /work-packages/${id}/deadline`.
  - Added `updateWorkPackageDeadline` to exported `workPackageService` object.
  - Verified compilation via `vue-tsc --noEmit && vite build` (`npm run build`).
- Changed Files:
  - `cakra/src/frontend/Cakra.Web/src/api/workpackages.ts`

---

### P3-S06

Title: Work Package Screen (SCR-WP-001) UI Integration & Overdue Badging

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
In `cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue`:
1. Update `WorkPackageItem` interface with `deadline?: string | null`.
2. Update `createForm` state to include `deadline: ''`. In `openCreateModal` reset `createForm.deadline = ''`.
3. In Create Work Package modal: add an optional date picker input `<input type="date" v-model="createForm.deadline" class="form-control form-control-sm">` with `data-testid="create-deadline-input"`.
4. In `handleCreateWorkPackage`: pass `deadline: createForm.deadline ? createForm.deadline : null` in the `POST /work-packages` payload.
5. In Work Packages list table:
   - Add a `<th scope="col" style="width: 130px">Deadline</th>` column header before Status.
   - Render formatted deadline (`YYYY-MM-DD` or date display).
   - If `isWorkPackageOverdue(wp)` is true, display a red `Overdue` badge (`data-testid="wp-overdue-badge"`).
   - Update `colspan="7"` on empty/loading rows to accommodate the new column.
6. In Detail Panel metadata:
   - Display target deadline with formatted date and overdue badge when applicable.
7. In Detail Panel actions:
   - Add an inline deadline editing form (`deadlineForm.deadline`) with Save and Clear buttons, visible and active when `canModifyPackage` is true.
   - Wire form submission to `PUT /api/v1/work-packages/${id}/deadline`.
8. Implement `isWorkPackageOverdue(wp)` helper: returns true if `wp.deadline` is present, `wp.status !== 'CLOSED'`, and date parsed is strictly before today (local midnight).
9. Add helper `formatDeadline(dateString)` for user-friendly date rendering.

Depends On: P3-S05

Repository: cakra

Completion Criteria:
- Users can input a deadline when creating a work package.
- Work Package table displays the Deadline column with overdue indicators.
- Detail Panel displays the deadline and allows editing or clearing it while in `DRAFT` or `ACTIVE`.
- Closed work packages do not allow modifying the deadline and do not display overdue warnings.
- Vue typecheck and build pass cleanly (`npm run build`).

Notes:
- Matches TD-005 in `CR-023-ARCHITECTURE.md`.
- Implementation:
  - Updated `WorkPackageItem` interface in `WorkPackageView.vue` with `deadline?: string | null`.
  - Added `deadline: ''` to `createForm` state and reset in `openCreateModal`, `closeCreateModal`, and after successful creation.
  - Added date picker `<input type="date" v-model="createForm.deadline" class="form-control form-control-sm">` (`data-testid="create-deadline-input"`) to the Create Work Package modal.
  - Passed `deadline: createForm.deadline ? createForm.deadline : null` in `handleCreateWorkPackage` POST payload.
  - Added `<th scope="col" style="width: 130px">Deadline</th>` column header before Status, formatted deadline date cell (`YYYY-MM-DD`), and red `Overdue` badge (`data-testid="wp-overdue-badge"`).
  - Updated `colspan="7"` on empty and loading table rows.
  - Added target deadline display with formatted date and overdue badge to the Detail Panel metadata grid.
  - Added inline deadline editing form with `deadlineForm.deadline`, Save button (`data-testid="save-deadline-button"`), and Clear button (`data-testid="clear-deadline-button"`), active and visible when `canModifyPackage` is true.
  - Implemented `handleUpdateDeadline` and `handleClearDeadline` using `updateWorkPackageDeadline` API client helper.
  - Implemented `isWorkPackageOverdue(wp)` checking presence of deadline, non-closed status, and comparison strictly before today's local midnight.
  - Implemented `formatDeadline(value)` helper for consistent `YYYY-MM-DD` date presentation.
  - Verified compilation via `npm run build` (`vue-tsc --noEmit && vite build`) with zero errors.
- Changed Files:
  - `cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue`

---

## P4 - Verification & Test Validation

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

### P4-S07

Title: Full-Stack Test Suite Execution & Acceptance Verification

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
1. Run backend unit and integration test suites:
   `dotnet test cakra/tests/backend/Cakra.Tests.Unit`
   `dotnet test cakra/tests/backend/Cakra.Tests.Integration`
2. Run frontend typechecking and production build:
   `npm run build` in `cakra/src/frontend/Cakra.Web`
3. Verify that all 10 acceptance conditions in `CR-023-ARCHITECTURE.md §11` are completely satisfied with zero regressions.

Depends On: P1-S01, P1-S02, P2-S03, P2-S04, P3-S05, P3-S06

Repository: cakra

Completion Criteria:
- All automated backend unit and integration tests execute successfully with 100% pass rate.
- Frontend production bundle builds with zero errors.
- All acceptance conditions verified.

Notes:
- Full-stack test execution and acceptance verification results:
  - Backend Unit Tests: `dotnet test cakra/tests/backend/Cakra.Tests.Unit` completed with 452 passed, 0 failed, 0 skipped (100% pass rate).
  - Backend Integration Tests: `dotnet test cakra/tests/backend/Cakra.Tests.Integration` completed with 178 passed, 0 failed, 0 skipped (100% pass rate).
  - Frontend Build & Typechecking: `npm run build` (`vue-tsc --noEmit && vite build`) in `cakra/src/frontend/Cakra.Web` completed with 0 errors and generated production bundle.
  - Acceptance Conditions Verification: All 10 conditions defined in `CR-023-ARCHITECTURE.md §11` verified and satisfied:
    1. Migration script `0017_add_workpackage_deadline.sql` executed cleanly and idempotently, recorded in `SchemaVersions`.
    2. `WorkPackage.Create(...)` correctly accepts and stores optional target deadline normalized to UTC midnight.
    3. `WorkPackage.UpdateDeadline(...)` updates/clears deadline when `DRAFT`/`ACTIVE`, and throws `WorkPackageDomainException` when `CLOSED`.
    4. `POST /api/v1/work-packages` accepts `deadline` in request body.
    5. `PUT /api/v1/work-packages/{id}/deadline` updates/clears deadline and returns updated `WorkPackageDto`.
    6. Queries (`GetWorkPackageById`, `ListWorkPackages`, `GetRequestWorkPackage`) project and return `Deadline`.
    7. Create Work Package modal in `SCR-WP-001` provides deadline input.
    8. Work Packages table in `SCR-WP-001` renders Deadline column and red Overdue badge for overdue open packages.
    9. Detail Panel in `SCR-WP-001` displays deadline and provides inline edit/clear actions for active packages.
    10. 100% pass rate across all test suites with zero regressions.
- Changed Files:
  - `cakra/docs/implementation-plan/CR-023-IMPLEMENTATION-PLAN.md`
