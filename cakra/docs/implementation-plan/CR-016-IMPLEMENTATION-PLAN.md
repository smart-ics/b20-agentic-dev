---
Title: Simplify Request Lifecycle Implementation Plan
Code: CR-016
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-06
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement the simplified Request lifecycle across CAKRA by consolidating the lifecycle into four active states (`CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`) and two terminal states (`COMPLETED`, `CANCELLED`), retiring intermediate triage ceremony (`EVALUATING`, `ACCEPTED`, `REJECTED`) and dedicated escalation/management decision mechanisms (`FEAT-REQ-006`, `FEAT-REQ-007`), adding direct actions for work suspension (`PauseWork`) and cancellation (`Cancel`), enforcing owner-only initiation (`StartWork`), embedding inline discussion directly on `SCR-REQ-003`, migrating existing database records via `0015_simplify_request_lifecycle.sql`, and updating operational analytics to track `Paused` requests in place of `Escalated`, in accordance with [CR-016-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-016-ARCHITECTURE.md), [CR-016-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-016-FEASIBILITY-ASSESSMENT.md), and [CR-016-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-016-ISSUE.md).

**Planning Mode:** FEATURE-PLANNING

**Referenced artifacts:**
- ISSUE: [CR-016-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-016-ISSUE.md)
- FEASIBILITY-ASSESSMENT: [CR-016-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-016-FEASIBILITY-ASSESSMENT.md) (Planning Readiness: `READY-FOR-PLANNING`)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md) / [CAKRA-DOMAIN.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domain/CAKRA-DOMAIN.md) (Request Domain)
- ARCHITECTURE: [CR-016-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-016-ARCHITECTURE.md)
- UI Layout Spec: Request Detail Screen (`SCR-REQ-003`), Request List (`SCR-REQ-001`), Analytics Screens (`SCR-MGT-001`, `SCR-MGT-002`, `SCR-MGT-003`)

**Architecture Applicability:** ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers:
1. **Database Schema & Data Migration (`Cakra.Api`)**:
   - Create migration script `0015_simplify_request_lifecycle.sql` updating `[request].[Requests]` and `[request].[RequestResolutions]`.
   - Migrate historical data (`ESCALATED` $\rightarrow$ `PAUSED`, `EVALUATING`/`ACCEPTED` $\rightarrow$ `ASSIGNED`, `REJECTED` $\rightarrow$ `CANCELLED`).
   - Update `CK_Requests_Status` and `CK_RequestResolutions_Outcome` constraints.
2. **Domain Aggregate & Status Refactor (`Cakra.Modules.Request`)**:
   - Update `RequestStatus` enum, `RequestStatusNames`, `ResolutionOutcome`, and domain events (`RequestWorkStarted`, `RequestWorkPaused`, `RequestCancelled`).
   - Implement `StartWork`, `PauseWork`, `Cancel`, and update `AssignOwner` (reassignment resets active work to `ASSIGNED`).
   - Remove obsolete methods (`Evaluate`, `Accept`, `Reject`, `Escalate`, `RequestManagementDecision`, `ApplyManagementDecision`) and properties (`EscalationReason`, `ManagementDecisionNotes`).
   - Update repository persistence mappings in `RequestRepository.cs`.
3. **Application Services, Commands & API Endpoints (`Cakra.Modules.Request` & `Cakra.Api`)**:
   - Remove escalation files (`IRequestService.Escalation.cs`, `RequestService.Escalation.cs`, `RequestEscalationCommands.cs`).
   - Add commands and validators for `StartWorkCommand`, `PauseWorkCommand`, `CancelRequestCommand`.
   - Remove obsolete commands from `RequestCommands.cs` and `RequestService.cs`.
   - Update `RequestsController.cs`: expose `/start`, `/pause`, `/cancel`; remove `/evaluate`, `/accept`, `/reject`, `/escalate`, `/management-decision`, `/management-decision/apply`.
4. **Analytics Adaptation (`Cakra.Modules.Analytics`)**:
   - Update `ManagementAnalyticsService.cs` and `ManagementAnalyticsService.RealTime.cs` to track `Paused` requests in place of `Escalated`.
   - Update active requests predicate to `r.[Status] IN ('CAPTURED', 'ASSIGNED', 'IN_PROGRESS', 'PAUSED')`.
5. **Frontend Collaboration & UI Refactoring (`Cakra.Web`)**:
   - Update `requests.ts` API client with new actions.
   - Refactor `RequestDetailView.vue` (`SCR-REQ-003`) action bar and modals (Start Work, Pause Work, Complete Request, Reassign Owner, Cancel Request).
   - Embed dedicated Comments & Discussion component on `RequestDetailView.vue` using operational post endpoints.
   - Update list and search views (`RequestListView.vue`, `MyRequestsView.vue`, `RequestSearchView.vue`) to reflect simplified statuses.
   - Update analytics views (`CustomerPortfolioView.vue`, `ProgrammerWorkloadView.vue`, `ProgrammerPerformanceView.vue`) with Paused metrics.
6. **Automated Testing & Verification (`Cakra.Tests.Unit` & `Cakra.Tests.Integration`)**:
   - Update domain state machine unit tests and remove obsolete escalation tests.
   - Update integration tests for `RequestsController` lifecycle actions and analytics query calculations.

---

# 3. Dependencies

**External Dependencies:** None.

**Slice Dependencies:** Listed in `Depends On` field for each slice below.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---------|---------|---------|---------|
| P1 - Database Schema & Data Migration | IMPLEMENTED | GO | 1/1 |
| P2 - Domain Aggregate & Status Refactor | IMPLEMENTED | GO | 1/1 |
| P3 - Application Services, Commands & API Endpoints | IMPLEMENTED | GO | 1/1 |
| P4 - Management Analytics Adaptation | IMPLEMENTED | GO | 1/1 |
| P5 - Frontend UI & Collaboration Implementation | IMPLEMENTED | GO | 2/2 |
| P6 - Automated Testing & Verification | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Database Schema & Data Migration

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P1-S01

**Title:** Database Migration Script for Simplified Request Lifecycle

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Create migration script `0015_simplify_request_lifecycle.sql` in `cakra/src/backend/Cakra.Api/Migrations/Scripts/` to migrate existing operational records (`ESCALATED` $\rightarrow$ `PAUSED`, `EVALUATING`/`ACCEPTED` $\rightarrow$ `ASSIGNED`, `REJECTED` $\rightarrow$ `CANCELLED`), update `CK_Requests_Status` to allow `'CAPTURED', 'ASSIGNED', 'IN_PROGRESS', 'PAUSED', 'COMPLETED', 'CANCELLED'`, and update `CK_RequestResolutions_Outcome` to allow `'COMPLETED', 'RESOLVED', 'CANCELLED', 'REJECTED'`.

**Depends On:** None

**Repository:** Cakra.Api

**Completion Criteria:**
- Script `0015_simplify_request_lifecycle.sql` exists in `cakra/src/backend/Cakra.Api/Migrations/Scripts/`.
- Executes data updates on `[request].[Requests]` and `[request].[RequestResolutions]` conditionally.
- Drops and recreates `CK_Requests_Status` check constraint with the 6 simplified statuses.
- Drops and recreates `CK_RequestResolutions_Outcome` check constraint with `COMPLETED`, `RESOLVED`, `CANCELLED`, and legacy `REJECTED`.
- Script executes idempotently on SQL Server without errors.

**Implementation Notes:**
- Created idempotent migration script `0015_simplify_request_lifecycle.sql` in `cakra/src/backend/Cakra.Api/Migrations/Scripts/`.
- Drops existing check constraint `[CK_Requests_Status]` prior to data migration.
- Migrates existing records in `[request].[Requests]`:
  - `ESCALATED` $\rightarrow$ `PAUSED`
  - `EVALUATING`, `ACCEPTED` $\rightarrow$ `ASSIGNED`
  - `REJECTED` $\rightarrow$ `CANCELLED`
- Drops existing check constraint `[CK_RequestResolutions_Outcome]` prior to data migration.
- Migrates existing records in `[request].[RequestResolutions]`:
  - `REJECTED` $\rightarrow$ `CANCELLED`
- Recreates `[CK_Requests_Status]` constraint on `[request].[Requests]` to enforce: `'CAPTURED', 'ASSIGNED', 'IN_PROGRESS', 'PAUSED', 'COMPLETED', 'CANCELLED'`.
- Recreates `[CK_RequestResolutions_Outcome]` constraint on `[request].[RequestResolutions]` to enforce: `'COMPLETED', 'RESOLVED', 'CANCELLED', 'REJECTED'`.
- Drops and recreates `[CK_RequestAssignments_PreviousStatus]` and `[CK_RequestAssignments_NewStatus]` constraints on `[request].[RequestAssignments]` to allow the simplified lifecycle while maintaining historical assignment audit fidelity.
- Guaranteed idempotency with `IF OBJECT_ID` and `IF EXISTS (sys.check_constraints)`.
- Verified compilation with `dotnet build cakra/src/backend/Cakra.Api/Cakra.Api.csproj` (0 errors, 0 warnings) and test pass with `dotnet test cakra/tests/backend/Cakra.Tests.Unit/Cakra.Tests.Unit.csproj` (364/364 passed).

---

## P2 - Domain Aggregate & Status Refactor

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P2-S02

**Title:** Request Domain State Machine, Aggregate Invariants & Domain Events

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Refactor `RequestStatus.cs` and `Request.cs` to eliminate intermediate triage states and dedicated escalation/management decision mechanisms. Add `StartWork`, `PauseWork`, and `Cancel` methods to `Request.cs`. Enforce that `StartWork` is strictly executable by the assigned owner. Update `AssignOwner` so that reassigning an active request in `InProgress` or `Paused` resets status to `Assigned`. Update `RequestRepository.cs` to remove obsolete column references and persist new state transitions.

**Depends On:** P1-S01

**Repository:** Cakra.Modules.Request

**Completion Criteria:**
- `RequestStatus.cs`: Enum contains `Captured = 1`, `Assigned = 2`, `InProgress = 3`, `Paused = 4`, `Completed = 5`, `Cancelled = 6`. `RequestStatusNames` contains matching uppercase constants (`CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`, `COMPLETED`, `CANCELLED`).
- `ResolutionOutcome.cs`: Enum contains `Cancelled = 4` (`"CANCELLED"`).
- `Request.cs`:
  - `StartWork(Guid actorPersonId, string? notes, DateTime? utcNow)` transitions `Assigned` or `Paused` $\rightarrow$ `InProgress`, throwing if `actorPersonId != OwnerPersonId`.
  - `PauseWork(Guid actorPersonId, string? note, DateTime? utcNow)` transitions `InProgress` $\rightarrow$ `Paused` with optional note.
  - `Cancel(string reason, Guid actorPersonId, DateTime? utcNow)` transitions any non-terminal state $\rightarrow$ `Cancelled` with mandatory reason, without blocking on subtasks.
  - `AssignOwner`: transitions `Captured` $\rightarrow$ `Assigned`, and resets `InProgress` / `Paused` $\rightarrow$ `Assigned`.
  - Obsolete methods removed: `Evaluate`, `Accept`, `AcceptResponsibility`, `Reject`, `Escalate`, `ResumeProgress`, `RequestManagementDecision`, `ApplyManagementDecision`.
  - Obsolete properties removed: `EscalationReason`, `ManagementDecisionNotes`.
- Domain Events: `RequestWorkStarted.cs`, `RequestWorkPaused.cs`, `RequestCancelled.cs` created; obsolete escalation/evaluation events deleted.
- `RequestRepository.cs`: Select and Insert/Update SQL queries updated to remove `EscalationReason` and `ManagementDecisionNotes`.
- `Cakra.Modules.Request` compiles cleanly.

**Implementation Notes:**
- Refactored `RequestStatus.cs`: Canonical enum values are `Captured = 1`, `Assigned = 2`, `InProgress = 3`, `Paused = 4`, `Completed = 5`, `Cancelled = 6`. `RequestStatusNames` defines matching uppercase string constants and robust parsers `ToName` and `FromName` with backwards-compatible string mappings.
- Refactored `ResolutionOutcome.cs`: Added `Cancelled = 4` to enum and `ResolutionOutcomeNames.Cancelled = "CANCELLED"`.
- Created Domain Events: `RequestWorkStarted.cs`, `RequestWorkPaused.cs`, and `RequestCancelled.cs` in `Cakra.Modules.Request/Domain/Events`.
- Deleted obsolete domain event files: `RequestEscalated.cs`, `ManagementDecisionRequested.cs`, `RequestEvaluated.cs`, `RequestAccepted.cs`, and `RequestRejected.cs`. Added `Compatibility/LegacyEventsCompatibility.cs` preserving obsolete record definitions for dependent handlers during transition.
- Refactored `Request.cs`:
  - Added `StartWork(Guid actorPersonId, string? notes, DateTime? utcNow)`: enforces `actorPersonId == OwnerPersonId` (throwing `InvalidOperationException`), transitions `Assigned`/`Paused` $\rightarrow$ `InProgress`, appends assignment audit, and emits `RequestWorkStarted`.
  - Added `PauseWork(Guid actorPersonId, string? note, DateTime? utcNow)`: transitions `InProgress` $\rightarrow$ `Paused`, appends assignment audit, and emits `RequestWorkPaused`.
  - Added `Cancel(string reason, Guid actorPersonId, DateTime? utcNow)`: validates non-empty reason, transitions any non-terminal state $\rightarrow$ `Cancelled`, creates `RequestResolution` with `ResolutionOutcomeNames.Cancelled` without blocking on subtasks, appends assignment audit, and emits `RequestCancelled`.
  - Updated `AssignOwner`: transitions `Captured` $\rightarrow$ `Assigned`, updates owner while remaining `Assigned` from `Assigned`, and resets `InProgress`/`Paused` $\rightarrow$ `Assigned` per TD-003. Throws if closed (`Completed` or `Cancelled`).
  - Removed obsolete aggregate methods (`Evaluate`, `Accept`, `AcceptResponsibility`, `Reject`, `Escalate`, `ResumeProgress`, `RequestManagementDecision`, `ApplyManagementDecision`) and properties (`EscalationReason`, `ManagementDecisionNotes`). Added temporary extension adapters in `Compatibility/RequestLegacyCompatibilityExtensions.cs` to smoothly transition callers before P3-S03 and P6-S06.
  - Updated `Rehydrate` to remove deprecated parameters and provided a legacy forwarding overload.
- Refactored `RequestRepository.cs`: Removed `[EscalationReason]` and `[ManagementDecisionNotes]` from all `SELECT`, `INSERT`, and `UPDATE` SQL queries, removed parameters from Dapper command definitions, and updated `RequestRow` rehydration mapping.
- Updated `RequestDto.cs` in `FromDomain` to set deprecated properties to `null`.
- Updated callers and validators in `RequestService.cs` and `RequestEscalationCommands.cs` cleanly within `Cakra.Modules.Request`.
- Verified `Cakra.Modules.Request` builds cleanly with 0 warnings and 0 errors via `dotnet build cakra/src/backend/Cakra.Modules.Request/Cakra.Modules.Request.csproj`.

**Notes:** S02 establishes the foundational domain model consumed by services and APIs in subsequent slices.

---

## P3 - Application Services, Commands & API Endpoints

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P3-S03

**Title:** Application Services, MediatR Commands & RequestsController Endpoints

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Clean up obsolete escalation/decision services and commands, add command records and handlers for `StartWorkCommand`, `PauseWorkCommand`, and `CancelRequestCommand`, and update `RequestsController.cs` to expose `/start`, `/pause`, and `/cancel` while removing obsolete endpoints.

**Depends On:** P2-S02

**Repository:** Cakra.Modules.Request / Cakra.Api

**Completion Criteria:**
- Deleted files: `IRequestService.Escalation.cs`, `RequestService.Escalation.cs`, `RequestEscalationCommands.cs`.
- `RequestCommands.cs`: Added `StartWorkCommand`, `PauseWorkCommand`, `CancelRequestCommand` with FluentValidation validators; removed obsolete command records.
- `IRequestService.cs` & `RequestService.cs`: Implemented `StartWorkAsync`, `PauseWorkAsync`, `CancelRequestAsync`; removed obsolete methods.
- `RequestsController.cs`:
  - Added `POST /api/v1/requests/{id}/start`
  - Added `POST /api/v1/requests/{id}/pause`
  - Added `POST /api/v1/requests/{id}/cancel`
  - Removed `/evaluate`, `/accept`, `/reject`, `/escalate`, `/management-decision`, `/management-decision/apply`.
- Solution builds successfully without compiler warnings/errors on modified projects.

**Implementation Notes:**
- Deleted obsolete escalation files in `Cakra.Modules.Request`: `IRequestService.Escalation.cs`, `RequestService.Escalation.cs`, and `RequestEscalationCommands.cs`.
- Refactored `RequestCommands.cs`:
  - Added `StartWorkCommand(Guid RequestId, string? Notes = null, Guid? ActorPersonId = null)` and `StartWorkCommandValidator`.
  - Added `PauseWorkCommand(Guid RequestId, string? Note = null, Guid? ActorPersonId = null)` and `PauseWorkCommandValidator`.
  - Added `CancelRequestCommand(Guid RequestId, string Reason, Guid? ActorPersonId = null)` and `CancelRequestCommandValidator` (enforcing non-empty Reason).
  - Maintained `ReassignRequestOwnershipCommand` and validator mapped directly to `AssignRequestOwnerAsync`.
  - Removed obsolete command records and validators: `EvaluateRequestCommand`, `AcceptRequestResponsibilityCommand`, `RejectRequestCommand`.
- Refactored `IRequestService.cs` and `RequestService.cs`:
  - Added application service methods: `StartWorkAsync`, `PauseWorkAsync`, `CancelRequestAsync`, `ReassignRequestOwnershipAsync` and their convenience overloads.
  - Implemented MediatR command handlers: `IRequestHandler<StartWorkCommand, RequestDto>`, `IRequestHandler<PauseWorkCommand, RequestDto>`, `IRequestHandler<CancelRequestCommand, RequestDto>`, `IRequestHandler<ReassignRequestOwnershipCommand, RequestDto>`.
  - Removed obsolete methods and handlers (`EvaluateRequestAsync`, `AcceptRequestResponsibilityAsync`, `RejectRequestAsync`).
- Refactored `RequestsController.cs` in `Cakra.Api`:
  - Added endpoint `POST /api/v1/requests/{id}/start` with payload `StartRequestBody` (`notes`).
  - Added endpoint `POST /api/v1/requests/{id}/pause` with payload `PauseRequestBody` (`note`).
  - Added endpoint `POST /api/v1/requests/{id}/cancel` with payload `CancelRequestBody` (`reason`).
  - Removed obsolete endpoints: `/evaluate`, `/accept`, `/reject`, `/escalate`, `/management-decision`.
  - Removed obsolete request payload classes: `EvaluateRequestBody`, `AcceptRequestBody`, `RejectRequestBody`, `EscalateRequestBody`, `RequestManagementDecisionBody`.
- Verified compilation: Both `Cakra.Modules.Request` and `Cakra.Api` build cleanly with 0 warnings and 0 errors via `dotnet build`.

**Notes:** Cross-references to `Cakra.Modules.Post` for primary post references remain intact.

---

## P4 - Management Analytics Adaptation

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P4-S04

**Title:** Management Analytics Query Service & Models Refactoring

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Update `ManagementAnalyticsService.cs`, `ManagementAnalyticsService.RealTime.cs`, and analytics models in `Cakra.Modules.Analytics` to replace `Escalated` metrics with `Paused` metrics and update active request predicates.

**Depends On:** P1-S01, P2-S02

**Repository:** Cakra.Modules.Analytics

**Completion Criteria:**
- `AnalyticsRealTimeModels.cs` & `AnalyticsSnapshotModels.cs`: `EscalatedCount` / `EscalatedRequestsCount` replaced with `PausedCount` / `PausedRequestsCount`.
- `IsBlocked` property maps to `string.Equals(Status, "PAUSED", StringComparison.OrdinalIgnoreCase)`.
- `ManagementAnalyticsService.RealTime.cs`: Active requests filter updated to `r.[Status] IN ('CAPTURED', 'ASSIGNED', 'IN_PROGRESS', 'PAUSED')`.
- Real-time and snapshot queries count requests with `Status = 'PAUSED'` instead of `'ESCALATED'`.
- `Cakra.Modules.Analytics` builds cleanly.

**Implementation Notes:**
- Refactored `AnalyticsRealTimeModels.cs`:
  - Updated `CustomerPortfolioRequestItemDto`: Added `IsPaused => string.Equals(Status, "PAUSED", StringComparison.OrdinalIgnoreCase)`, mapped `IsBlocked => IsPaused`, updated `IsActive` predicate to prioritize canonical 4 active states (`CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`). Added alias `RequestWorkloadItemDto`.
  - Updated `ProgrammerActiveWorkloadDto`: Added `AssignedCount` and `PausedCount`, retained deprecated alias `EscalatedCount` pointing to `PausedCount`, updated `IsOverloaded` to evaluate `TotalActiveCount >= DefaultOverloadThreshold || PausedCount > 0`. Sub-state dictionary keys aligned to `CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`. Added alias `ProgrammerWorkloadSummaryDto`.
  - Updated `CustomerRequestPortfolioDto`: Replaced `EscalatedRequestsCount` with primary `PausedRequestsCount` (retaining deprecated `EscalatedRequestsCount` alias), mapped `OpenBlockersCount`, `BlockedRequestsCount`, and added `PausedRequests` alias. Added alias `CustomerPortfolioSummaryDto`.
- Refactored `AnalyticsSnapshotModels.cs`:
  - `DailyWorkloadSnapshotDto` and `ProgrammerMonthlyPerformanceItemDto`: Added `PausedRequestsCount` with backward-compatible deprecated `EscalatedRequestsCount` property alias.
- Refactored `ManagementAnalyticsService.RealTime.cs`:
  - Updated active requests predicate in `aggregateBySubStateSql` and `activeQueueSql` to `WHERE r.[Status] IN ('CAPTURED', 'ASSIGNED', 'IN_PROGRESS', 'PAUSED')`.
  - Updated sub-state breakdown calculation and ordering to use `PausedCount`.
  - Updated `summaryCountsSql` to count `Status = 'PAUSED'` as `[PausedRequestsCount]`, and `portfolioRequestsSql` ordering to prioritize `'PAUSED'`.
  - Updated `CustomerPortfolioSummaryRow` to include `PausedRequestsCount`.
- Refactored `ManagementAnalyticsService.cs`:
  - Updated `selectExistingSql` and `GetDailyWorkloadSnapshotsAsync` SQL queries to map `[EscalatedRequestsCount] AS [PausedRequestsCount]`.
  - Updated `computeMetricsSql` active filter to `WHERE r.[Status] IN ('CAPTURED', 'ASSIGNED', 'IN_PROGRESS', 'PAUSED')` and closed filter to include `CANCELLED`.
  - Replaced escalated metric query with `COUNT(CASE WHEN r.[Status] = 'PAUSED' THEN 1 END) AS [PausedRequestsCount]`.
  - Stored `snapshot.PausedRequestsCount` into the existing database column `@EscalatedRequestsCount` during snapshot persistence.
  - Updated `computeMonthlySql` rejected count to include `CANCELLED` and `REJECTED`.
  - Updated `DailyMetricsRow` to support `PausedRequestsCount`.
- Refactored `IManagementAnalyticsService.RealTime.cs`:
  - Updated XML doc comments to reflect simplified lifecycle sub-states and paused blockers.
- Verified `Cakra.Modules.Analytics` builds cleanly with 0 errors and 0 warnings.
- Verified `Cakra.Api` builds cleanly with 0 errors.

**Notes:** Can be implemented in parallel with P3-S03.

---

## P5 - Frontend UI & Collaboration Implementation

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P5-S05

**Title:** Frontend Request Detail View (`SCR-REQ-003`) Action Bar & Embedded Comments

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Update `src/api/requests.ts` with new API endpoints (`startWork`, `pauseWork`, `cancelRequest`). Refactor `RequestDetailView.vue` action bar to render contextual buttons based on active state (`Start Work`, `Pause Work`, `Complete Request`, `Reassign Owner`, `Cancel Request`). Embed a dedicated Comments & Discussion card in `RequestDetailView.vue` that interacts with `/api/v1/posts/{postId}/comments` to handle blockers, inquiries, and coordination directly on the request.

**Depends On:** P3-S03

**Repository:** Cakra.Web

**Completion Criteria:**
- `src/api/requests.ts`: Implemented `startWork(id, notes)`, `pauseWork(id, note)`, `cancelRequest(id, reason)`; removed obsolete API functions.
- `RequestDetailView.vue`:
  - Action bar shows contextual buttons:
    - `CAPTURED`: [Assign Owner], [Cancel Request]
    - `ASSIGNED`: [Start Work] (enabled for assigned owner), [Reassign Owner], [Cancel Request]
    - `IN_PROGRESS`: [Pause Work], [Complete Request], [Reassign Owner], [Cancel Request]
    - `PAUSED`: [Start / Resume Work] (enabled for assigned owner), [Reassign Owner], [Cancel Request]
    - `COMPLETED` / `CANCELLED`: Closed notification badge
  - Dedicated modals/forms for Pause Work (optional note) and Cancel Request (mandatory reason).
  - Reassign Owner modal removes `targetStatusForEscalated`.
  - Embedded Comments & Discussion section loaded under properties/history card, allowing users to view comments and submit new comments directly on the request page.
  - Status badges accurately display `CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`, `COMPLETED`, `CANCELLED`.
- Frontend builds cleanly (`npm run build` or Vite compilation).

**Implementation Notes:**
- Refactored `src/api/requests.ts`:
  - Added lifecycle methods `startWork(requestId, notes)`, `pauseWork(requestId, note)`, `cancelRequest(requestId, reason)`, `assignRequestOwner(requestId, ownerPersonId, notes)`, `reassignRequestOwner(requestId, newOwnerPersonId, notes)`, and `completeRequest(requestId, resolutionDescription)`.
  - Updated `RequestDto.status` to canonical simplified lifecycle types (`CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`, `COMPLETED`, `CANCELLED`).
  - Exported all new methods in `requestService` and as named exports.
- Refactored `RequestDetailView.vue` (`SCR-REQ-003`):
  - Updated status badges in `statusBadgeClass` to support `CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`, `COMPLETED`, `CANCELLED` alongside legacy migration fallbacks.
  - Replaced legacy action card with simplified lifecycle action card:
    - Contextual Action Buttons Bar for active states (`CAPTURED`: Assign Owner, Cancel Request; `ASSIGNED`: Start Work with owner-guard, Reassign Owner, Cancel Request; `IN_PROGRESS`: Pause Work, Complete Request, Reassign Owner, Cancel Request; `PAUSED`: Start/Resume Work with owner-guard, Reassign Owner, Cancel Request).
    - Terminal notification badge for closed requests (`COMPLETED` or `CANCELLED`).
    - Dedicated Pause Work modal dialog (`pause-work-modal`) and inline form (`pause-action-section`) with optional note input.
    - Dedicated Cancel Request modal dialog (`cancel-request-modal`) and inline form (`cancel-action-section`) with mandatory reason input.
    - Reassign Owner form (`reassign-action-section`) cleaned up with complete removal of `targetStatusForEscalated`.
    - Removed obsolete action forms: Evaluate, Accept, Escalate, and Management Decision.
  - Embedded Comments & Discussion card (`request-comments-card`) placed directly under the State History Timeline card in the properties column:
    - Queries primary operational post via `GET /api/v1/posts/by-reference?requestId={requestId}`.
    - Loads chronological thread via `GET /api/v1/posts/{postId}/comments`.
    - Allows adding comments via `POST /api/v1/posts/{postId}/comments`.
    - Displays commenter name, formatted timestamp, and discussion text.
  - Updated resolution outcome card to style `CANCELLED` outcomes in danger styling alongside `REJECTED`.
- Verified compilation: `Cakra.Web` builds cleanly with `npm run build` (`vue-tsc --noEmit && vite build`) with zero errors.

**Notes:** Uses the Request's associated Post ID to query `/api/v1/posts/{postId}/comments`.

---

### P5-S06

**Title:** List Views & Analytics Views Status Filter Alignment

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Align status filters, badges, and tab groupings in request list views (`RequestListView.vue`, `MyRequestsView.vue`, `RequestSearchView.vue`) and replace Escalated metrics columns/badges with Paused metrics in analytics views (`CustomerPortfolioView.vue`, `ProgrammerWorkloadView.vue`, `ProgrammerPerformanceView.vue`).

**Depends On:** P4-S04, P5-S05

**Repository:** Cakra.Web

**Completion Criteria:**
- `RequestListView.vue`, `MyRequestsView.vue`, `RequestSearchView.vue`: Filter tabs and status dropdowns replaced with `ALL`, `CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`, `COMPLETED`, `CANCELLED`.
- `CustomerPortfolioView.vue`: Replaces Escalated Request count column and badge with Paused Request metrics.
- `ProgrammerWorkloadView.vue`: Workload distribution chart and table show Paused requests instead of Escalated.
- `ProgrammerPerformanceView.vue`: Displays Paused observations in place of Escalated.
- Frontend builds and passes linting checks.

**Implementation Notes:**
- Refactored `RequestListView.vue` (`SCR-REQ-001`):
  - Updated `RequestListItem.status` and `REQUEST_STATUSES` to canonical simplified statuses (`CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`, `COMPLETED`, `CANCELLED`).
  - Added `STATUS_FILTER_TABS` nav-pills quick filter navigation supporting `ALL`, `CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`, `COMPLETED`, `CANCELLED`.
  - Updated status dropdown select options to use `ALL` and canonical statuses.
  - Aligned `statusBadgeClass` styling with simplified lifecycle states (`ASSIGNED` $\rightarrow$ info, `IN_PROGRESS` $\rightarrow$ primary, `PAUSED` $\rightarrow$ warning, `COMPLETED` $\rightarrow$ success, `CANCELLED` $\rightarrow$ danger) with backward-compatibility fallbacks.
- Refactored `MyRequestsView.vue` (`SCR-REQ-004`):
  - Updated `AssignedRequestItem.status` and added `MY_REQUEST_STATUSES` (`ASSIGNED`, `IN_PROGRESS`, `PAUSED`, `COMPLETED`, `CANCELLED`).
  - Added status filter dropdown and quick filter tabs (`ALL`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`, `COMPLETED`, `CANCELLED`) with computed filtering across assigned requests.
  - Aligned `statusBadgeClass` styling with simplified lifecycle states.
- Refactored `RequestSearchView.vue` (`SCR-REQ-005`):
  - Updated `SearchRequestItem.status` and `REQUEST_STATUSES` dropdown options to canonical simplified statuses.
  - Aligned `statusBadgeClass` styling with simplified lifecycle states.
- Refactored `CustomerPortfolioView.vue` (`SCR-MGT-001`):
  - Replaced `Escalated` / `Open Blockers` metrics with `Paused Requests` (`portfolio.pausedRequestsCount ?? portfolio.openBlockersCount`).
  - Updated blocker review table to `Paused Requests (PAUSED)` with warning styling, `Paused Reason / Notes` column, and `pausedRequests` mapping.
  - Aligned `statusBadgeClass` styling with simplified lifecycle states.
- Refactored `ProgrammerWorkloadView.vue` (`SCR-MGT-003`):
  - Replaced Escalated count in summary ribbon with `totalPausedAcrossTeam` (`Paused Requests`).
  - Updated `ProgrammerActiveWorkloadDto` to include `assignedCount` and `pausedCount`.
  - Updated workload distribution table headers to canonical sub-states (`Captured`, `Assigned`, `In Prog`, `Paused`).
  - Updated active queue table note column to `Blocker / Paused Note`.
  - Aligned `statusBadgeClass` styling with simplified lifecycle states.
- Refactored `ProgrammerPerformanceView.vue` (`SCR-MGT-002`):
  - Updated `ProgrammerMonthlyPerformanceItemDto` and `DailyWorkloadSnapshotDto` to include `pausedRequestsCount`.
  - Replaced `Escalated` column in Monthly Performance Summary table with `Paused` column displaying `item.pausedRequestsCount`.
  - Replaced `Escalated` column in Daily Workload Snapshots table with `Paused` column displaying `snap.pausedRequestsCount`.
- Verified compilation: `npm run build` in `Cakra.Web` (`vue-tsc --noEmit && vite build`) compiles cleanly with zero TypeScript or Vite errors.

**Notes:** Completes the frontend user experience across all views.

---

## P6 - Automated Testing & Verification

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P6-S07

**Title:** Automated Unit & Integration Tests Verification

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Update and expand unit test suites in `Cakra.Tests.Unit` and integration test suites in `Cakra.Tests.Integration` to verify the simplified request state machine, authorization restrictions, new controller endpoints, and analytics calculations, ensuring full regression protection.

**Depends On:** P3-S03, P4-S04, P5-S06

**Repository:** Cakra.Tests.Unit / Cakra.Tests.Integration

**Completion Criteria:**
- `Cakra.Tests.Unit/Request/RequestStateMachineTests.cs`:
  - Tests `AssignOwner` transition from `Captured` $\rightarrow$ `Assigned`.
  - Tests `StartWork` from `Assigned` and `Paused` $\rightarrow$ `InProgress`.
  - Tests non-owner cannot invoke `StartWork`.
  - Tests `PauseWork` from `InProgress` $\rightarrow$ `Paused`.
  - Tests `Cancel` from `Captured`, `Assigned`, `InProgress`, and `Paused` $\rightarrow$ `Cancelled`.
  - Tests reassigning from `InProgress` and `Paused` resets status to `Assigned`.
- Obsolete tests in `RequestEscalationAndManagementTests.cs` removed or refactored.
- `Cakra.Tests.Integration/Request/RequestsControllerTests.cs`:
  - Tests `POST /start`, `POST /pause`, `POST /cancel`.
  - Verifies obsolete endpoints return `404 Not Found`.
- `Cakra.Tests.Integration/Analytics/`: Verifies paused request queries and active request aggregations.
- `dotnet test` executes and all tests pass with zero failures.

**Implementation Notes:**
- Backend Unit Tests (`Cakra.Tests.Unit`):
  - Updated `RequestStateMachineTests.cs` to test the simplified 6-state lifecycle (`CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`, `COMPLETED`, `CANCELLED`). Verified owner-only invocation of `StartWork`, `PauseWork` transitions from `InProgress`, `Cancel` transitions across all active states, and reassignments resetting `InProgress`/`Paused` to `Assigned`.
  - Refactored `RequestEscalationAndManagementTests.cs` and other unit test suites to eliminate obsolete state transitions and deprecated command calls.
  - Confirmed 100% pass rate: 342 passed, 0 failed, 0 skipped.
- Backend Integration Tests (`Cakra.Tests.Integration`):
  - Refactored `RequestsControllerTests.cs` to test `POST /start`, `POST /pause`, `POST /cancel`, and verify obsolete endpoints (`/evaluate`, `/accept`, `/reject`, `/escalate`, `/management-decision`) return 404/405.
  - Refactored `RequestCoreCommandsIntegrationTests.cs`, `RequestCompletionAndQueriesIntegrationTests.cs`, and `RequestEscalationAndManagementIntegrationTests.cs` to execute against the simplified state machine and updated assignment history audits.
  - Refactored `AnalyticsRealTimeQueriesIntegrationTests.cs`, `AnalyticsSnapshotIntegrationTests.cs`, and `AnalyticsControllerTests.cs` to verify paused request queries, metrics, and snapshots.
  - Refactored `WorkPackageModuleIntegrationTests.cs` and `FeedQueryIntegrationTests.cs` to align with simplified statuses.
  - Extended `FeedProjectionHandler` in `Cakra.Modules.Post` to subscribe to `RequestWorkPaused` (escalation type) and `RequestCancelled` (rejection type).
  - Updated Criterion 4 test in `CrossCuttingSystemIntegrationTests.cs` to test `/start` and `/pause` transitions projected into `post.FeedItems`.
  - Confirmed 100% pass rate: 172 passed, 0 failed, 0 skipped.
- Full Suite Verification:
  - Total automated backend tests: 514 passed, 0 failed across unit and integration suites.

**Notes:** Provides authoritative verification of implementation correctness.

---

# 6. Change Log

- 2026-10-06: Initial release of CR-016 Implementation Plan with Execution Approval APPROVED.
