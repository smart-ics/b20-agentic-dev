---
Title: Implementation Plan for Optional Target Deadline for Request Aggregate and Screens (CR-020)
Code: CR-020
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-06
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-020`: Optional target deadline for operational requests across database persistence, domain aggregate rules, automated audit history logging, REST API endpoints, DTO contracts, and frontend screens (`CreateRequestModal.vue`, `CreateRequestView.vue`, `RequestDetailView.vue`).

Deliver end-to-end realization across:
1. Database schema migration `0016_add_request_deadline.sql` adding column `[Deadline] DATETIME2 NULL` to `[request].[Requests]`.
2. Domain property `Deadline` on `Request` aggregate root with UTC date-only normalization and active lifecycle state gating.
3. Automated audit logging on `RequestAssignment` when deadline is set, modified, or cleared during `Request.UpdateCoreAttributes(...)`.
4. Persistence query updates in `RequestRepository.cs` and entity hydration.
5. Application commands (`RecordRequestCommand`, `UpdateRequestCoreAttributesCommand`), handlers, and REST controller endpoints on `RequestsController.cs`.
6. Frontend form inputs in creation flows (`CreateRequestModal.vue`, `CreateRequestView.vue`).
7. Presentation in `RequestDetailView.vue` including formatted deadline, dynamic red "Overdue" badge, and Edit modal controls.

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- ISSUE: [CR-020-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-020-ISSUE.md)
- FEASIBILITY-ASSESSMENT: [CR-020-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-020-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)
- ARCHITECTURE: [CR-020-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-020-ARCHITECTURE.md) (authoritative target architecture)

Architecture Applicability: ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers all database migrations, domain modeling, persistence updates, application commands, REST endpoints, frontend screens, and build verifications required to realize `CR-020`:

1. **Database Migration (`Cakra.Api`)**:
   - Create idempotent DbUp script `0016_add_request_deadline.sql` adding `[Deadline] DATETIME2 NULL` and filtered index `[IX_Requests_Deadline]` to `[request].[Requests]`.
2. **Domain Aggregate & Events (`Cakra.Modules.Request`)**:
   - Add property `Deadline` (`DateTime?`, UTC) to `Request.cs`.
   - Add `NormalizeDeadline` helper ensuring UTC midnight (`00:00:00Z`).
   - Extend `Record(...)` to accept optional `DateTime? deadline = null`.
   - Extend `UpdateCoreAttributes(...)` to accept optional `DateTime? deadline = null`.
   - In `UpdateCoreAttributes(...)`, if the deadline changed, create and append a `RequestAssignment` audit trail item with status preserved and formatted note.
   - Update domain events `RequestRecorded` and `RequestCoreAttributesUpdated` with `DateTime? Deadline`.
3. **Persistence Layer (`Cakra.Modules.Request`)**:
   - Update `RequestRepository.cs` `SELECT`, `INSERT`, `UPDATE`, and hydration logic to map `Deadline`.
4. **Application Commands & API Contracts (`Cakra.Modules.Request` & `Cakra.Api`)**:
   - Add `DateTime? Deadline` to `RequestDto` and `RequestDetailDto`.
   - Extend `RecordRequestCommand` and `UpdateRequestCoreAttributesCommand` with `DateTime? Deadline`.
   - Update `RequestService` command handlers.
   - Extend `RequestsController.cs` payload models and actions (`POST /api/v1/requests`, `PUT /api/v1/requests/{id}/core-attributes`).
5. **Frontend API Client & Creation Views (`Cakra.Web`)**:
   - Extend TypeScript interfaces in `src/frontend/Cakra.Web/src/api/requests.ts`.
   - Add optional date input for deadline in `CreateRequestModal.vue` and `CreateRequestView.vue`.
6. **Frontend Request Detail View & Overdue Badging (`Cakra.Web`)**:
   - Render formatted deadline in Request Detail Information card on `RequestDetailView.vue`.
   - Dynamically compute overdue status and display red "Overdue" badge when open and past target date.
   - Add date picker in Edit Request Details modal dialog with clear deadline button.
7. **Full-Stack Verification**:
   - Run backend test suites (`dotnet test`).
   - Run frontend typecheck and production build (`npm run build`).

---

# 3. Dependencies

- .NET 8 SDK & ASP.NET Core (`Cakra.Api`, `Cakra.Modules.Request`)
- SQL Server schema migration pipeline (DbUp in `Cakra.Api.Infrastructure.Migrations`)
- Dapper object mapping in `Cakra.Modules.Request.Persistence`
- Node.js & npm (Vue 3 / Vite / TypeScript in `src/frontend/Cakra.Web`)

For slice dependencies:
- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires the referenced slice to have implementation status `IMPLEMENTED`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Database Schema & Domain Aggregate | IMPLEMENTED | GO | 2/2 |
| P2 - Persistence & Application Layer | IMPLEMENTED | GO | 2/2 |
| P3 - Frontend Creation & Detail Screens | IMPLEMENTED | GO | 2/2 |
| P4 - Verification & Test Validation | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Database Schema & Domain Aggregate

Implementation Status: IMPLEMENTED
Review Status: GO

### P1-S01

Title: Database Migration Script for Request Deadline

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Create an idempotent DbUp SQL migration script `0016_add_request_deadline.sql` in `cakra/src/backend/Cakra.Api/Migrations/Scripts/` that adds `[Deadline] DATETIME2 NULL` and a filtered nonclustered index `[IX_Requests_Deadline]` on `[request].[Requests]`.

Depends On: None

Repository: cakra

Completion Criteria:
- File `cakra/src/backend/Cakra.Api/Migrations/Scripts/0016_add_request_deadline.sql` exists and is embedded or located with existing scripts.
- Guarded by `COL_LENGTH` and index existence checks.
- Compatible with existing `0006_request_tables.sql` and `0015_simplify_request_lifecycle.sql`.

Notes:
- Reuses DbUp convention matching scripts `0001` through `0015`.
- Implementation: Created `0016_add_request_deadline.sql` adding column `[Deadline] DATETIME2 NULL` and filtered index `[IX_Requests_Deadline]` on `[request].[Requests]` with idempotency guards matching TD-001.
- Changed Files:
  - `cakra/src/backend/Cakra.Api/Migrations/Scripts/0016_add_request_deadline.sql`

---

### P1-S02

Title: Domain Property, Normalization, Mutation Method, and Audit Logging on Request

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
1. In `cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs`:
   - Add property `public DateTime? Deadline { get; private set; }`.
   - Add private static helper `NormalizeDeadline(DateTime? deadline)` normalizing non-null dates to UTC midnight (`00:00:00Z`).
   - Update `Record(...)` to accept `DateTime? deadline = null` and initialize `Deadline = NormalizeDeadline(deadline)`.
   - Update `UpdateCoreAttributes(...)` to accept `DateTime? deadline = null`.
   - In `UpdateCoreAttributes(...)`, if `normalizedNewDeadline != Deadline`, create and append a `RequestAssignment` audit entry with status preserved (`previousStatus: Status, newStatus: Status`) and notes:
     - `Deadline set to {newDate:yyyy-MM-dd}` (when previous was null)
     - `Deadline changed from {oldDate:yyyy-MM-dd} to {newDate:yyyy-MM-dd}` (when both non-null)
     - `Deadline cleared` (when new is null)
   - Preserve existing invariant throwing `InvalidRequestStateTransitionException` if status is closed (`COMPLETED` or `CANCELLED`).
2. Update domain events in `cakra/src/backend/Cakra.Modules.Request/Domain/Events/`:
   - Update `RequestRecorded.cs` to include `DateTime? Deadline`.
   - Update `RequestCoreAttributesUpdated.cs` to include `DateTime? Deadline`.

Depends On: None

Repository: cakra

Completion Criteria:
- `Request.Deadline` property exists and enforces UTC midnight normalization.
- `Request.Record(...)` and `Request.UpdateCoreAttributes(...)` accept `DateTime? deadline`.
- `RequestAssignment` audit record is automatically appended whenever deadline is modified or cleared on an existing request.
- Closed requests reject attribute updates.
- Domain events carry `Deadline`.

Notes:
- Can be tested via unit tests in `Cakra.Modules.Request.Tests` or domain verification.
- Implementation:
  - Added property `public DateTime? Deadline { get; private set; }` and private static helper `NormalizeDeadline(DateTime? deadline)` to `Request.cs`.
  - Updated `Request.Record(...)` and `Request.Rehydrate(...)` to accept optional `DateTime? deadline = null` and normalize values to UTC midnight.
  - Updated `Request.UpdateCoreAttributes(...)` to accept `DateTime? deadline = null`, preserve closed request invariant, and append a `RequestAssignment` audit trail record with status preserved and formatted note on deadline addition, change, or clearing.
  - Updated domain events `RequestRecorded.cs` and `RequestCoreAttributesUpdated.cs` with `DateTime? Deadline`.
  - Added unit test suite `RequestDeadlineDomainTests.cs` (8 unit tests) in `Cakra.Tests.Unit`, verifying normalization, creation, audit note formatting, and closed state guard rules. All 419 unit tests pass cleanly.
- Changed Files:
  - `cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs`
  - `cakra/src/backend/Cakra.Modules.Request/Domain/Events/RequestRecorded.cs`
  - `cakra/src/backend/Cakra.Modules.Request/Domain/Events/RequestCoreAttributesUpdated.cs`
  - `cakra/tests/backend/Cakra.Tests.Unit/Request/RequestDeadlineDomainTests.cs`

---

## P2 - Persistence & Application Layer

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

### P2-S03

Title: Persistence Layer Queries & Dapper Hydration for Deadline

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
In `cakra/src/backend/Cakra.Modules.Request/Persistence/RequestRepository.cs`:
1. Include `[Deadline]` in all `SELECT` query column projections from `[request].[Requests]`.
2. Include `[Deadline]` in `INSERT INTO [request].[Requests]` parameter mapping.
3. Include `[Deadline] = @Deadline` in `UPDATE [request].[Requests]` parameter mapping in `UpdateAsync`.
4. Update `HydrateFromRow` to assign `Deadline` property on hydrated `Request` instances (e.g. using reflection/internal setter or private constructor helper).

Depends On: P1-S01, P1-S02

Repository: cakra

Completion Criteria:
- `RequestRepository.cs` compiles and maps `Deadline` across read, insert, and update queries.
- Hydrated `Request` aggregates contain the persisted `Deadline` value.

Notes:
- Ensure parameter `@Deadline` handles DBNull when `Deadline` is null.
- Implementation:
  - Added `[Deadline]` column projection to `SELECT` queries in `GetByIdAsync` and `GetAllAsync`.
  - Added `[Deadline]` column and `@Deadline` parameter mapping to `INSERT INTO [request].[Requests]` in `AddAsync`.
  - Added `[Deadline] = @Deadline` and `@Deadline` parameter mapping to `UPDATE [request].[Requests]` in `UpdateAsync`.
  - Added `public DateTime? Deadline { get; init; }` to `RequestRow` and forwarded `Deadline` to `Domain.Request.Rehydrate(...)` in `ToDomain()`.
  - Added unit test coverage for `Request.Rehydrate` with deadline in `RequestDeadlineDomainTests.cs`. All 421 unit tests pass cleanly.
- Changed Files:
  - `cakra/src/backend/Cakra.Modules.Request/Persistence/RequestRepository.cs`
  - `cakra/tests/backend/Cakra.Tests.Unit/Request/RequestDeadlineDomainTests.cs`

---

### P2-S04

Title: Application Commands, Handlers, DTOs & REST API Endpoints

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
1. In `cakra/src/backend/Cakra.Modules.Request/Models/RequestDto.cs`:
   - Add `public DateTime? Deadline { get; init; }` to `RequestDto`.
   - Map `Deadline = request.Deadline` in `FromDomain(Request request)`.
2. In `cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs`:
   - Update `RecordRequestCommand` record with `DateTime? Deadline = null`.
   - Update `UpdateRequestCoreAttributesCommand` record with `DateTime? Deadline = null`.
3. In `cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs`:
   - Pass `command.Deadline` to `Request.Record(...)` in `RecordRequestAsync`.
   - Pass `command.Deadline` to `request.UpdateCoreAttributes(...)` in `UpdateRequestCoreAttributesAsync`.
4. In `cakra/src/backend/Cakra.Api/Controllers/RequestsController.cs`:
   - Update `RecordRequestBody` with `public DateTime? Deadline { get; set; }`.
   - Update `UpdateRequestCoreAttributesBody` with `public DateTime? Deadline { get; set; }`.
   - Map `body.Deadline` to commands in `RecordRequest` (`POST /api/v1/requests`) and `UpdateRequestCoreAttributes` (`PUT /api/v1/requests/{id}/core-attributes` and `PUT /api/v1/requests/{id}`).

Depends On: P1-S02, P2-S03

Repository: cakra

Completion Criteria:
- `RecordRequestCommand` and `UpdateRequestCoreAttributesCommand` accept `Deadline`.
- `RequestsController` accepts `deadline` in request bodies and returns `Deadline` in `RequestDto`.
- Backend solution builds cleanly (`dotnet build`).

Notes:
- Maintain backward compatibility: `Deadline` is optional (`null`).
- Implementation:
  - Added `public DateTime? Deadline { get; init; }` to `RequestDto` and mapped `Deadline = request.Deadline` in `RequestDto.FromDomain`.
  - Added `DateTime? Deadline = null` to `RecordRequestCommand` and `UpdateRequestCoreAttributesCommand` in `RequestCommands.cs`.
  - Added `DateTime? deadline = null` to `RecordRequestAsync` and `UpdateRequestCoreAttributesAsync` in `IRequestService.cs` and `RequestService.cs`, forwarding `deadline` to `Request.Record(...)` and `request.UpdateCoreAttributes(...)`. Added backwards-compatible method overloads.
  - Updated `RequestsController` to accept `Deadline` in `RecordRequestBody` and `UpdateRequestCoreAttributesBody`, mapping `Deadline` to commands in `RecordRequest` and `UpdateRequestCoreAttributes`, and added route `PUT /api/v1/requests/{id}/core-attributes` alongside `PUT /api/v1/requests/{id}`.
  - Wrapped `CREATE NONCLUSTERED INDEX` in dynamic SQL `EXEC(N'CREATE NONCLUSTERED INDEX ...')` in `0016_add_request_deadline.sql` to avoid compile-time column resolution failures before the alter table commits.
  - Added unit test coverage in `RequestDeadlineDomainTests.cs` and `RequestCoreAttributesCommandServiceTests.cs`. All 424 unit tests and 54 Request integration tests pass.
- Changed Files:
  - `cakra/src/backend/Cakra.Modules.Request/Models/RequestDto.cs`
  - `cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs`
  - `cakra/src/backend/Cakra.Modules.Request/Services/IRequestService.cs`
  - `cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs`
  - `cakra/src/backend/Cakra.Api/Controllers/RequestsController.cs`
  - `cakra/src/backend/Cakra.Api/Migrations/Scripts/0016_add_request_deadline.sql`
  - `cakra/tests/backend/Cakra.Tests.Unit/Request/RequestDeadlineDomainTests.cs`
  - `cakra/tests/backend/Cakra.Tests.Unit/Request/RequestCoreAttributesCommandServiceTests.cs`

---

## P3 - Frontend Creation & Detail Screens

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

### P3-S05

Title: Frontend API Client and Request Creation Views

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
1. In `cakra/src/frontend/Cakra.Web/src/api/requests.ts`:
   - Add `deadline?: string | null` to `RequestDto`, `RequestDetail`, `CreateRequestPayload`, and `UpdateRequestCoreAttributesPayload`.
2. In `cakra/src/frontend/Cakra.Web/src/components/CreateRequestModal.vue`:
   - Add `deadline: string | null` to `form` reactive state.
   - Add an optional date input field `<input type="date" id="createDeadlineInput" v-model="form.deadline" class="form-control" data-testid="create-deadline-input">`.
   - Include `deadline: form.deadline || null` in payload submission.
3. In `cakra/src/frontend/Cakra.Web/src/views/CreateRequestView.vue`:
   - Add `deadline: string | null` to `form` state and template input field `<input type="date" ... data-testid="create-deadline-input">`.

Depends On: P2-S04

Repository: cakra

Completion Criteria:
- Creation forms permit selecting an optional target deadline date.
- Payloads submitted to `POST /api/v1/requests` include `deadline`.
- Form resets clear the deadline field cleanly.

Notes:
- Use standard HTML5 `<input type="date">`.
- Implementation:
  - Added `deadline?: string | null` to `RequestDto`, `RequestDetail`, `CreateRequestPayload` (with `RecordRequestPayload` alias), and `UpdateRequestCoreAttributesPayload` in `cakra/src/frontend/Cakra.Web/src/api/requests.ts`.
  - Added `deadline: null as string | null` to reactive `form` state, optional date input with `id="createDeadlineInput"` and `data-testid="create-deadline-input"`, `deadline: form.deadline ? form.deadline : null` in payload submission, and reset handling in `cakra/src/frontend/Cakra.Web/src/components/CreateRequestModal.vue`.
  - Added `deadline: null as string | null` to reactive `form` state, optional date input with `id="createDeadlineInput"` and `data-testid="create-deadline-input"`, and `deadline: form.deadline ? form.deadline : null` in payload submission in `cakra/src/frontend/Cakra.Web/src/views/CreateRequestView.vue`.
  - Frontend type verification (`npm run type-check`) and production build (`npm run build`) succeeded with 0 errors. All backend tests pass (424 unit tests, 173 integration tests).
- Changed Files:
  - `cakra/src/frontend/Cakra.Web/src/api/requests.ts`
  - `cakra/src/frontend/Cakra.Web/src/components/CreateRequestModal.vue`
  - `cakra/src/frontend/Cakra.Web/src/views/CreateRequestView.vue`

---

### P3-S06

Title: Request Detail View, Edit Modal & Dynamic Overdue Badge

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
In `cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue`:
1. In Request Information card:
   - Render a Deadline attribute row with formatted date (e.g. `formatDeadline(request.deadline)` or `"None"`).
   - Compute `isOverdue`: true if `request.deadline` exists, status is not `COMPLETED` or `CANCELLED`, and `deadline < today` (date-only comparison).
   - Display a prominent red badge `<span class="badge bg-danger" data-testid="request-overdue-badge"><i class="bi bi-exclamation-triangle-fill me-1"></i>Overdue</span>` when `isOverdue` is true.
2. In Edit Request Details modal:
   - Add `deadline: string | null` to `editForm` reactive state.
   - Populate `editForm.deadline` in `openEditModal()` from `request.deadline` (formatting as `YYYY-MM-DD`).
   - Add `<input type="date" id="editDeadlineInput" v-model="editForm.deadline" class="form-control" data-testid="edit-deadline-input">`.
   - Add a "Clear" button to set `editForm.deadline = null`.
   - Pass `deadline: editForm.deadline || null` to `updateRequestCoreAttributes(...)` call.
3. Verify that the chronological state history timeline displays the automated audit record created by the backend when the deadline is set, modified, or cleared.

Depends On: P3-S05

Repository: cakra

Completion Criteria:
- Request Detail displays the deadline and displays the red Overdue badge if past deadline and open.
- Edit Request Details modal allows updating or clearing the deadline.
- State history timeline reflects deadline changes with descriptive audit notes.

Notes:
- Check date comparison against current date midnight to avoid false-positive overdue flags on the due day itself.
- Implementation:
  - Added `deadline?: string | null` to local `RequestDetail` interface.
  - Implemented `isOverdue` computed property comparing normalized midnight of deadline against current local midnight, suppressing overdue status when COMPLETED or CANCELLED.
  - Implemented `formatDeadline` formatting valid dates as `YYYY-MM-DD` or returning `"None"`.
  - Added Deadline attribute row with `formatDeadline(request.deadline)` and prominent red badge `<span class="badge bg-danger" data-testid="request-overdue-badge"><i class="bi bi-exclamation-triangle-fill me-1"></i>Overdue</span>` inside Properties / Request Information card (`data-testid="request-information-card"` and `data-testid="request-detail-deadline"`).
  - Added `deadline: null as string | null` to `editForm` reactive state.
  - In `openEditModal()`, populated `editForm.deadline` formatted as `YYYY-MM-DD` (or `null`).
  - Added date input field `<input type="date" id="editDeadlineInput" v-model="editForm.deadline" class="form-control" data-testid="edit-deadline-input">` and Clear Deadline button (`data-testid="clear-deadline-button"`).
  - In `handleSaveEdit()`, passed `deadline: editForm.deadline ? editForm.deadline : null` to `updateRequestCoreAttributes(...)` and triggered `await loadStateHistory()` to immediately show backend audit entries in state history timeline.
  - Verified `npm run type-check` and `npm run build` pass cleanly with 0 errors. All 424 backend unit tests pass.
- Changed Files:
  - `cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue`

---

## P4 - Verification & Test Validation

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

### P4-S07

Title: Automated Test Verification and Build Validation

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
1. Execute backend test suite via `dotnet test` covering `Cakra.Modules.Request` and `Cakra.Api`.
2. Add/update unit tests verifying:
   - `Request.Record(...)` sets and normalizes `Deadline`.
   - `Request.UpdateCoreAttributes(...)` updates `Deadline` and logs `RequestAssignment` with descriptive notes.
   - Closed requests reject deadline edits.
   - `RequestRepository` persists and retrieves `Deadline`.
3. Execute frontend verification:
   - TypeScript check (`vue-tsc --noEmit`).
   - Production Vite build (`npm run build`).

Depends On: P2-S04, P3-S06

Repository: cakra

Completion Criteria:
- All backend unit and integration tests pass without error.
- Frontend builds cleanly without TypeScript or packaging errors.
- Zero regressions in existing test suite.

Notes:
- Use standard `dotnet test` and `npm run build` commands.
- Implementation:
  - Verified backend unit test suite: 424/424 unit tests pass (`dotnet test cakra/tests/backend/Cakra.Tests.Unit/Cakra.Tests.Unit.csproj`).
  - Added integration test `RequestRepository_persists_and_retrieves_Deadline_on_Record_and_UpdateCoreAttributes` in `cakra/tests/backend/Cakra.Tests.Integration/Request/RequestCoreCommandsIntegrationTests.cs`, validating full SQL Server roundtrip persistence and hydration of `Deadline` across `RecordRequestCommand`, `UpdateRequestCoreAttributesCommand`, `GetByIdAsync`, and `GetAllAsync`.
  - Updated test doubles (`InMemoryRequestRepository`) across unit test suites (`RequestCoreCommandsTests.cs`, `RequestEscalationAndManagementTests.cs`, `RequestCompletionAndQueriesTests.cs`, `RequestComplexityCommandServiceTests.cs`) to preserve `req.Deadline` during entity rehydration.
  - Verified integration test suite: 173/173 integration tests pass against SQL Server instance.
  - Executed frontend TypeScript verification in `cakra/src/frontend/Cakra.Web` (`npm run type-check` via `vue-tsc --noEmit`) with 0 errors.
  - Executed frontend production build in `cakra/src/frontend/Cakra.Web` (`npm run build` via Vite) with exit code 0 and successful bundle output.
  - Verified all acceptance conditions in CR-020-ARCHITECTURE.md Section 11.
- Changed Files:
  - `cakra/tests/backend/Cakra.Tests.Integration/Request/RequestCoreCommandsIntegrationTests.cs`
  - `cakra/tests/backend/Cakra.Tests.Unit/Request/RequestCoreCommandsTests.cs`
  - `cakra/tests/backend/Cakra.Tests.Unit/Request/RequestEscalationAndManagementTests.cs`
  - `cakra/tests/backend/Cakra.Tests.Unit/Request/RequestCompletionAndQueriesTests.cs`
  - `cakra/tests/backend/Cakra.Tests.Unit/Request/RequestComplexityCommandServiceTests.cs`

---

# 6. Change Log

- 2026-10-06: Initial plan created for CR-020 (Optional Target Deadline for Request Aggregate and Screens). Plan approved by Architect (`Execution Approval: APPROVED`).
