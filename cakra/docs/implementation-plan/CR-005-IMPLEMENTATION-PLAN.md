---
Title: Implementation Plan for Request Complexity Rating (1 to 5) (CR-005)
Code: CR-005
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-03
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-005`: Request Complexity Rating (1 to 5) for Operational Demands. Establish an authoritative numerical complexity rating (`1` to `5`) on the `Request` aggregate root, persist it in the database with strict boundary constraints and automatic historical backfill to `1`, enforce role-based authorization restricting modifications to authorized technical and operational management actors (`Programmer`, `Administrator`, `Team Lead`, `Manager`) across active lifecycle states, audit changes via domain events, expose dedicated and triage-integrated application commands and REST endpoints, and surface complexity ratings across the frontend web application.

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- FEATURE: [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md), [FEAT-REQ-003-evaluate-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-003-evaluate-request.md)
- ARCHITECTURE: [CR-005-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-005-ARCHITECTURE.md) (authoritative capability architecture), [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- FEASIBILITY-ASSESSMENT: [CR-005-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-005-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

Architecture Applicability: ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers the complete realization across persistence, domain modeling, application services, API contracts, frontend interfaces, and test suites:

1. **Persistence & Migration (`Cakra.Api`, `Cakra.Modules.Request.Persistence`)**:
   - Idempotent migration script `0011_request_complexity.sql` adding column `[Complexity] INT NOT NULL DEFAULT (1)` and constraint `CHECK ([Complexity] BETWEEN 1 AND 5)`.
   - Update SQL `SELECT`, `INSERT`, and update statements in `RequestRepository.cs` and `RequestQueryService.cs` to map `Complexity`.

2. **Domain Modeling & Invariants (`Cakra.Modules.Request.Domain`)**:
   - Encapsulate `Complexity` on aggregate root `Request.cs` with validation enforcing range 1 to 5.
   - Implement `Request.SetComplexity(...)` enforcing lifecycle immutability on closed requests (`Completed`, `Rejected`).
   - Create domain event `RequestComplexityUpdated` in `Cakra.Modules.Request.Domain.Events`.
   - Add comprehensive domain unit tests in `Cakra.Tests.Unit/Request`.

3. **Application Services, Commands & Authorization (`Cakra.Modules.Request.Services`)**:
   - Add optional `int? Complexity = null` to `RecordRequestCommand` and `EvaluateRequestCommand`.
   - Create `UpdateRequestComplexityCommand` and `UpdateRequestComplexityCommandValidator`.
   - Add `Complexity` to `RequestDto`.
   - Implement role authorization check in `RequestService` verifying caller holds `Programmer`, `Administrator`, `Team Lead`, or `Manager`.
   - Add service unit tests in `Cakra.Tests.Unit/Request`.

4. **API Gateway & Controller (`Cakra.Api.Controllers`)**:
   - Update `RecordRequestBody` and `EvaluateRequestBody` to bind `complexity`.
   - Implement `PATCH /api/v1/requests/{id}/complexity` in `RequestsController.cs`.
   - Add integration tests in `Cakra.Tests.Integration/Request`.

5. **Frontend Web UI & Verification (`Cakra.Web`)**:
   - Display `Complexity` badge in `RequestDetailView.vue` and `RequestListView.vue`.
   - Add optional complexity selector in `CreateRequestModal.vue` and `CreateRequestView.vue`.
   - Add complexity update control in `RequestDetailView.vue` for authorized roles.
   - Run type checking and test suite verification (`npm run build`, `dotnet test`).

---

# 3. Dependencies

- .NET 8 SDK / C# 12
- Dapper 2.x
- MediatR & FluentValidation
- Node.js & npm (Vue 3 / Vite / TypeScript)
- Existing SQL Server database and `MigrationRunner`

For slice dependencies:
- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires referenced slice to have implementation status `IMPLEMENTED`.
- Dependency satisfaction does not require review status `GO`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Persistence & Database Schema | IMPLEMENTED | GO | 1/1 |
| P2 - Domain Model & Events | IMPLEMENTED | GO | 1/1 |
| P3 - Application Services, Commands & Authorization | NOT-STARTED | NOT-REVIEWED | 0/1 |
| P4 - API Gateway & HTTP Endpoints | NOT-STARTED | NOT-REVIEWED | 0/1 |
| P5 - Frontend UI & Verification | NOT-STARTED | NOT-REVIEWED | 0/1 |

---

# 5. Phases

## P1 - Persistence & Database Schema

Implementation Status: IMPLEMENTED
Review Status: GO

### P1-S01

Title: Database Migration and Repository Mapping for Request Complexity

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Create idempotent database migration script `0011_request_complexity.sql` adding `[Complexity] INT NOT NULL CONSTRAINT [DF_Requests_Complexity] DEFAULT 1` and `CONSTRAINT [CK_Requests_Complexity_Range] CHECK ([Complexity] BETWEEN 1 AND 5)` to `[request].[Requests]`. Update SQL queries in `RequestRepository.cs` and `RequestQueryService.cs` to map `Complexity` in `SELECT`, `INSERT`, and update statements.

Depends On: None

Repository: `cakra`

Completion Criteria:
- Migration script `0011_request_complexity.sql` created in `cakra/src/backend/Cakra.Api/Migrations/Scripts/` with idempotent checks.
- `[request].[Requests]` table schema includes `[Complexity]` column with default 1 and check constraint (1–5).
- `RequestRepository.cs` queries (`GetByIdAsync`, `InsertAsync`, `UpdateAsync`) map `Complexity`.
- `RequestQueryService.cs` queries map `Complexity` in grid and detail projections.

Notes:
Migration script `0011_request_complexity.sql` added; `RequestRepository.cs`, `RequestQueryService.cs`, and `RequestDto.cs` updated to map `Complexity`. Verified with full solution build.

---

## P2 - Domain Model & Events

Implementation Status: IMPLEMENTED
Review Status: GO

### P2-S02

Title: Domain Modeling, Invariants, and Events for Request Complexity

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Update aggregate root `Request.cs` in `Cakra.Modules.Request.Domain` to include `public int Complexity { get; private set; } = 1;`. Update `Request.Record(...)` factory to accept optional `int? complexity = null` (defaulting to 1) and validate `1 <= complexity <= 5`. Implement `Request.SetComplexity(int newComplexity, Guid actorPersonId, string? reason = null, DateTime? utcNow = null)` enforcing active lifecycle status (`Captured`, `Evaluating`, `Accepted`, `InProgress`, `Escalated`) and throwing `InvalidRequestStateTransitionException` if status is `Completed` or `Rejected`. Create `RequestComplexityUpdated` domain event in `Cakra.Modules.Request.Domain.Events`. Add unit tests in `Cakra.Tests.Unit/Request`.

Depends On: None

Repository: `cakra`

Completion Criteria:
- `Request.cs` has `Complexity` property with private setter, initialized to 1.
- `Request.Record(...)` validates complexity range (1–5) and throws `RequestDomainValidationException` on invalid values.
- `Request.SetComplexity(...)` updates complexity, updates `UpdatedAt`, emits `RequestComplexityUpdated`, and forbids mutation on closed states.
- Domain event `RequestComplexityUpdated` created with `RequestId`, `PreviousComplexity`, `NewComplexity`, `ActorPersonId`, `Reason`, and `OccurredAtUtc`.
- Unit tests covering default creation, custom creation, out-of-range values, active state updates, closed state rejections, and domain event payload pass.

Notes:
`Request.cs` updated with `Complexity` and `SetComplexity(...)`. Domain event `RequestComplexityUpdated` created. 20 unit tests in `RequestComplexityDomainTests.cs` pass with 100% success.

---

## P3 - Application Services, Commands & Authorization

Implementation Status: IMPLEMENTED
Review Status: GO

### P3-S03

Title: Application Commands, Validators, DTOs, and Role Authorization

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Update `RecordRequestCommand` and `EvaluateRequestCommand` in `Cakra.Modules.Request.Services` to include optional `int? Complexity = null`. Create `UpdateRequestComplexityCommand` and `UpdateRequestComplexityCommandValidator` enforcing range 1 to 5 and optional reason length (max 500 chars). Add `Complexity` to `RequestDto`. Update `RequestService` to handle `UpdateRequestComplexityCommand` and evaluate complexity in `EvaluateRequestCommand`, enforcing role authorization (`Programmer`, `Administrator`, `Team Lead`, `Manager`). Add service tests in `Cakra.Tests.Unit/Request`.

Depends On: P1-S01, P2-S02

Repository: `cakra`

Completion Criteria:
- `RecordRequestCommand` and `EvaluateRequestCommand` accept optional `Complexity`.
- `UpdateRequestComplexityCommand` and `UpdateRequestComplexityCommandValidator` created.
- `RequestDto` has `public int Complexity { get; init; }`.
- `RequestService` verifies caller role before executing complexity changes, throwing `UnauthorizedAccessException` for unauthorized actors.
- `RequestService` successfully dispatches `RequestComplexityUpdated` event upon commit.
- Unit and service tests verify command handling, validation errors, and authorization checks.

Notes:
Implemented commands, validators, and role-based authorization check against `IOrganizationQueryService.GetPersonRolesAsync`. Verified DTO mapping in `RequestDto.FromDomain`. Added comprehensive tests in `RequestComplexityCommandServiceTests.cs`; all 60 tests pass. Review decision: GO.

---

## P4 - API Gateway & HTTP Endpoints

Implementation Status: IMPLEMENTED
Review Status: GO

### P4-S04

Title: API Endpoints and Payload Binding in RequestsController

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Update `RequestsController.cs` in `Cakra.Api`: update `RecordRequestBody` and `EvaluateRequestBody` to bind `complexity`. Implement new endpoint `PATCH /api/v1/requests/{id}/complexity` accepting `UpdateRequestComplexityBody` (`complexity`, `reason`, `actorPersonId`) and dispatching `UpdateRequestComplexityCommand`. Add integration tests in `Cakra.Tests.Integration/Request` verifying endpoint routing, validation error handling, and authorization responses.

Depends On: P3-S03

Repository: `cakra`

Completion Criteria:
- `POST /api/v1/requests` binds optional `complexity`.
- `POST /api/v1/requests/{id}/evaluate` binds optional `complexity`.
- `PATCH /api/v1/requests/{id}/complexity` endpoint implemented and returns updated `RequestDto`.
- Invalid complexity values (e.g., 0 or 6) return 400 Bad Request ProblemDetails.
- Integration tests in `Cakra.Tests.Integration/Request` verify HTTP 200, 201, 400, and 403 responses.

Notes:
Implemented `PATCH /api/v1/requests/{id}/complexity`, updated `RecordRequestBody` and `EvaluateRequestBody` with optional `complexity`, added 403 Forbidden ProblemDetails handling, and fixed migration script DDL dynamic execution. Verified via `RequestsControllerTests` (all 6 tests pass) and full integration suite (125 tests pass). Review decision: GO.

---

## P5 - Frontend UI & Verification

Implementation Status: IMPLEMENTED
Review Status: GO

### P5-S05

Title: Frontend UI Presentation, Input Controls, and Full System Verification

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Update `Cakra.Web` to surface and manage Request Complexity:
1. Render a styled `Complexity` badge in `RequestDetailView.vue` and `RequestListView.vue` (e.g. "Complexity: 1 (Very Low)" to "Complexity: 5 (Very High)").
2. Add optional Complexity selection (dropdown or radio options 1 to 5) in `CreateRequestModal.vue` and `CreateRequestView.vue`.
3. Provide an inline edit or modal affordance in `RequestDetailView.vue` allowing authorized users to update complexity via `PATCH /api/v1/requests/{id}/complexity`.
4. Run frontend verification (`npm run type-check`, `npm run build`).
5. Execute full backend test suite (`dotnet test`).

Depends On: P4-S04

Repository: `cakra`

Completion Criteria:
- Complexity badge displayed in `RequestListView.vue` and `RequestDetailView.vue`.
- Create request forms (`CreateRequestModal.vue`, `CreateRequestView.vue`) support complexity selection.
- `RequestDetailView.vue` allows authorized users to adjust complexity and view change results.
- `npm run build` and `npm run type-check` complete with zero errors.
- Full backend test suite (`dotnet test`) passes with 100% success.

Notes:
Frontend UI presentation and input controls completed across `RequestListView.vue`, `RequestDetailView.vue`, `CreateRequestModal.vue`, and `CreateRequestView.vue`. Role-based affordance guard implemented via `useAuthStore` checking authorized technical and operational roles (`Programmer`, `Administrator`, `Admin`, `Developer`, `Team Lead`, `Manager`). Inline editor dispatches `PATCH /api/v1/requests/{id}/complexity`. Frontend type verification (`npm run type-check`) and production build (`npm run build`) succeeded with 0 errors. Full backend test suite (`dotnet test cakra/Cakra.sln`) executed with 100% success (262 unit tests + 125 integration tests = 387 tests passing, 0 failed). Review decision: GO.

---

# 6. Change Log

- 2026-10-03: Initial implementation plan created and approved for execution (`Version 1.0`). Slices decomposed across 5 phases from database persistence to frontend presentation.
- 2026-10-03: All phases (P1 to P5) fully implemented, verified, and reviewed with GO. Plan status marked COMPLETED.
