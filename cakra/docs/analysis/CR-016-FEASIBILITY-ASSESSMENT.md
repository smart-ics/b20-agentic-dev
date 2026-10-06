---
Title: Feasibility Assessment for Simplify Request Lifecycle
Code: CR-016
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-06
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Feature being assessed: Simplify Request Lifecycle, per change request in [CR-016-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-016-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-016-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-016-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md) / [CAKRA-DOMAIN.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domain/CAKRA-DOMAIN.md) (Request Domain)
- FEATURES:
  - [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md)
  - [FEAT-REQ-002-assign-request-owner.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-002-assign-request-owner.md)
  - [FEAT-REQ-003-evaluate-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-003-evaluate-request.md)
  - [FEAT-REQ-004-accept-request-responsibility.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-004-accept-request-responsibility.md)
  - [FEAT-REQ-005-reject-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-005-reject-request.md)
  - [FEAT-REQ-006-escalate-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-006-escalate-request.md)
  - [FEAT-REQ-007-request-management-decision.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-007-request-management-decision.md)
  - [FEAT-REQ-008-review-request-completion.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-008-review-request-completion.md)
- ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- Screens:
  - `SCR-REQ-001: Request List`
  - `SCR-REQ-003: Request Detail`
  - `SCR-REQ-004: My Requests`
  - `SCR-REQ-005: Request Search`
  - `SCR-MGT-001: Customer Portfolio Analytics`
  - `SCR-MGT-002: Programmer Performance Analytics`
  - `SCR-MGT-003: Programmer Workload Analytics`

## Objective

Assess the technical feasibility, baseline codebase state, interaction gaps, architectural impact, and planning readiness to:

1. Simplify the Request lifecycle to 4 active states (`CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`) and 2 terminal states (`COMPLETED`, `CANCELLED`).
2. Retire intermediate triage ceremony (`EVALUATING`, `ACCEPTED`, `REJECTED`) and dedicated escalation/management decision features (`FEAT-REQ-006`, `FEAT-REQ-007`).
3. Add a direct work suspension action ("Stop / Pause Work") moving a request from `IN_PROGRESS` to `PAUSED` with an optional note, callable by both Owner and Management.
4. Add direct work initiation ("Start Work") from `ASSIGNED` or `PAUSED` to `IN_PROGRESS`, strictly restricted to the assigned owner.
5. Add direct cancellation ("Cancel Request") from any non-terminal state to `CANCELLED` with a mandatory reason, without blocking on unfinished subtasks.
6. Update reassignment rules so reassigning active work (`IN_PROGRESS` or `PAUSED`) resets the status to `ASSIGNED`.
7. Embed a dedicated Comments & Discussion component directly on `SCR-REQ-003: Request Detail` integrated with the operational post and feed.
8. Execute a database migration for existing records (`ESCALATED` $\rightarrow$ `PAUSED`, `EVALUATING`/`ACCEPTED` $\rightarrow$ `ASSIGNED`, `REJECTED` $\rightarrow$ `CANCELLED`) and update `ManagementAnalyticsService` to track `Paused` requests in place of `Escalated`.

---

# 2. Current State

Summarize relevant findings from current artifacts and codebase. This section contains facts only.

## Existing Behavior

1. **Request Domain Aggregate & Status ([`Request.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs) / [`RequestStatus.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/RequestStatus.cs))**:
   - Status enum defines: `Captured = 1`, `Evaluating = 2`, `Accepted = 3`, `Rejected = 4`, `InProgress = 5`, `Escalated = 6`, `Completed = 7`.
   - `AssignOwner` transitions `Captured` $\rightarrow$ `Evaluating` and `Escalated` $\rightarrow$ `Evaluating`, or preserves active status.
   - `Evaluate` records triage notes and keeps state in `Evaluating`.
   - `Accept` / `AcceptResponsibility` transitions `Evaluating` $\rightarrow$ `Accepted`.
   - `Reject` transitions `Evaluating` $\rightarrow$ `Rejected`.
   - `Escalate` transitions `Evaluating` or `InProgress` $\rightarrow$ `Escalated` and sets `EscalationReason`.
   - `StartProgress` transitions `Accepted` $\rightarrow$ `InProgress` or `Escalated` $\rightarrow$ `InProgress`.
   - `Complete` transitions `InProgress` $\rightarrow$ `Completed` and validates that all subtasks are finished.
   - `RequestManagementDecision` and `ApplyManagementDecision` record docket notes and transition `Escalated` $\rightarrow$ `Evaluating` or `InProgress`.
2. **Backend Commands & Services ([`RequestCommands.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs) / [`RequestService.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs) / [`RequestService.Escalation.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestService.Escalation.cs))**:
   - `RequestService` implements separate handlers for `EvaluateRequestCommand`, `AcceptRequestResponsibilityCommand`, `RejectRequestCommand`, `EscalateRequestCommand`, `RequestManagementDecisionCommand`, and `ApplyManagementDecisionCommand`.
   - `IRequestService.Escalation.cs` and `RequestEscalationCommands.cs` maintain dedicated escalation interfaces and commands.
   - No commands or domain methods exist for `PauseWorkCommand` or `CancelRequestCommand`.
3. **API Controller ([`RequestsController.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/RequestsController.cs))**:
   - Exposes `POST /api/v1/requests/{id}/evaluate`, `POST /api/v1/requests/{id}/accept`, `POST /api/v1/requests/{id}/reject`, `POST /api/v1/requests/{id}/escalate`, `POST /api/v1/requests/{id}/management-decision`, `POST /api/v1/requests/{id}/management-decision/apply`.
   - Lacks endpoints for pause/suspend (`POST /api/v1/requests/{id}/pause`) and cancellation (`POST /api/v1/requests/{id}/cancel`).
4. **Request Detail Frontend ([`RequestDetailView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue))**:
   - Renders discrete action forms for Assign, Evaluate, Accept, Escalate, Complete, Management Decision, and Reassign.
   - Computes state flags `isEvaluating`, `isAccepted`, `isInProgress`, `isEscalated`.
   - Does not have a Pause Work button, Resume Work button, or Cancel Request modal.
   - Does not have a Comments / Discussion section on the page; communication is only accessible via the separate Feed view (`SCR-FEED-001`).
5. **Operational Analytics ([`ManagementAnalyticsService.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Analytics/Services/ManagementAnalyticsService.cs) / [`ManagementAnalyticsService.RealTime.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Analytics/Services/ManagementAnalyticsService.RealTime.cs))**:
   - Hardcodes active requests query: `WHERE r.[Status] IN ('CAPTURED', 'EVALUATING', 'ACCEPTED', 'IN_PROGRESS', 'ESCALATED')`.
   - Queries `EscalatedRequestsCount` / `EscalatedCount` and computes `IsBlocked => Status == 'ESCALATED'`.
   - Snapshot tables (`[analytics].[ProgrammerPerformanceDailySnapshots]`, `[analytics].[CustomerPortfolioDailySnapshots]`) persist `EscalatedRequestsCount`.
6. **Communication & Posts Module ([`Cakra.Modules.Post`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Post))**:
   - Full commenting infrastructure already exists (`Comment.cs`, `PostReferences.cs`, `CommentAdded` event, `POST /api/v1/posts/{id}/comments`, `GET /api/v1/posts/{id}/comments`).
   - Requests have associated posts tracked via `[post].[PostReferences]` (`ReferenceType = 'REQUEST'`, `ReferenceId = <RequestId>`).

## Existing Constraints

1. **Transactional State & Audit Integrity**:
   - Every lifecycle transition must append an immutable audit entry to `[request].[RequestAssignments]`.
2. **Sub-Task Completion Invariant**:
   - `Complete` must continue to enforce that all subtasks are finished (`RequestHasUnfinishedSubTasksException`).
   - `Cancel` must permit closure even if unfinished subtasks remain.
3. **Database Schema & Backward Compatibility**:
   - The database contains existing rows with statuses `EVALUATING`, `ACCEPTED`, `REJECTED`, and `ESCALATED`. A migration script must migrate these rows cleanly without data loss.

---

# 3. Gap Analysis

Identify gaps between the requested changes and the current system.

| ID | Severity | Gap |
|------|------|------|
| GAP-001 | CRITICAL | `RequestStatus` enum and `Request.cs` state machine enforce legacy states (`EVALUATING`, `ACCEPTED`, `REJECTED`, `ESCALATED`) and lack `PAUSED` and `CANCELLED`. |
| GAP-002 | CRITICAL | Missing "Stop / Pause Work" capability in domain aggregate root, service layer, command records, API controller, and frontend. |
| GAP-003 | CRITICAL | Missing "Cancel Request" capability in aggregate root, service layer, command records, API controller, and frontend. |
| GAP-004 | MAJOR | Dedicated Escalation and Management Decision features (`FEAT-REQ-006`, `FEAT-REQ-007`), services, commands, and controller endpoints are obsolete and must be removed. |
| GAP-005 | MAJOR | Reassignment logic in `Request.cs` preserves `InProgress` and requires explicit target status for escalated items, rather than automatically resetting active work to `ASSIGNED`. |
| GAP-006 | MAJOR | `RequestDetailView.vue` (`SCR-REQ-003`) lacks an embedded Comments & Discussion component for direct inline blocker and question handling. |
| GAP-007 | MAJOR | `ManagementAnalyticsService` and analytics UI screens (`CustomerPortfolioView.vue`, `ProgrammerWorkloadView.vue`, `ProgrammerPerformanceView.vue`) track `ESCALATED` instead of `PAUSED`. |
| GAP-008 | MAJOR | Database tables (`[request].[Requests]`, `[request].[RequestAssignments]`, `[request].[RequestResolutions]`) require a data migration script to convert historical rows to the simplified states. |
| GAP-009 | MINOR | List view filters (`RequestListView.vue`, `MyRequestsView.vue`, `RequestSearchView.vue`) filter by legacy statuses rather than the simplified status set. |

---

# 4. Open Questions

All open questions have been evaluated and resolved through the `/grill-me` alignment interview:

| ID | Question | Impact | Resolution |
|------|------|------|------|
| OQ-001 | What happens to intermediate triage states (`EVALUATING`, `ACCEPTED`, `REJECTED`)? | Defines target state machine and workflow actions. | **Resolved**: Retired entirely. Evaluation is part of direct work; acceptance is implicit when owner clicks "Start Work". `Reject` is replaced by `Cancel Request`. |
| OQ-002 | What are the rules and permissions for "Stop / Pause Work"? | Governs domain validation, audit logging, and authorization. | **Resolved**: Transitions `IN_PROGRESS` $\rightarrow$ `PAUSED`. Accepts optional note logged to audit trail. Authorized for assigned Owner and Management. Resumed via "Start Work". |
| OQ-003 | What happens to status when an active request (`IN_PROGRESS` or `PAUSED`) is reassigned? | Prevents false in-progress metrics and clarifies owner agency. | **Resolved**: Reassigning an active request always resets status to `ASSIGNED`, requiring the new owner to explicitly click "Start Work". |
| OQ-004 | From which states can "Cancel Request" be triggered, and what are its validation rules? | Defines cancellation availability and subtask rules. | **Resolved**: Allowed from any non-terminal state (`CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`). Requires a cancellation reason. Unfinished subtasks do not block cancellation. |
| OQ-005 | Who has authority to trigger "Start Work"? | Enforces owner commitment and accountability. | **Resolved**: Strictly restricted to the assigned owner. |
| OQ-006 | Where should request comments live? | Defines UI structure for blocker resolution. | **Resolved**: Embed a dedicated Comments & Discussion component directly on `SCR-REQ-003: Request Detail`, linked to the request's primary operational post. |
| OQ-007 | How should existing database records and analytics adapt? | Governs database migration and reporting metrics. | **Resolved**: DB migration converts `ESCALATED` $\rightarrow$ `PAUSED`, `EVALUATING`/`ACCEPTED` $\rightarrow$ `ASSIGNED`, `REJECTED` $\rightarrow$ `CANCELLED`. Analytics models replace `Escalated` with `Paused`. |

---

# 5. Assumptions

Document assumptions made during the assessment.

| ID | Assumption |
|------|------|
| ASM-001 | Existing `Cakra.Modules.Post` infrastructure (posts, comments, post references) can be queried and updated via API from `RequestDetailView.vue` using the Request's existing Post ID without duplicating comment data. |
| ASM-002 | Request Complexity rating (1 to 5) continues to be adjustable across all active states (`CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`) by authorized roles. |
| ASM-003 | Sub-tasks can be added, completed, reopened, and removed in any active state (`ASSIGNED`, `IN_PROGRESS`, `PAUSED`). |
| ASM-004 | Historical `[request].[RequestAssignments]` audit rows can retain their textual previous/new status values for chronological fidelity, while `[request].[Requests]` active statuses are migrated to canonical new statuses. |

---

# 6. Risks

Document identified risks, impacts, and mitigations.

| ID | Risk | Impact | Mitigation |
|------|------|------|------|
| RISK-001 | Out-of-sync database migration causing runtime errors when loading existing requests with legacy statuses. | High | Include a comprehensive SQL migration script (`0015_simplify_request_lifecycle.sql`) that updates existing rows and status check constraints before deploying code changes. |
| RISK-002 | Regression in analytics views and snapshot generation if legacy status columns are abruptly removed. | Moderate | Update analytics SQL queries and projection models in `Cakra.Modules.Analytics` to map `PAUSED` as suspended work, maintaining backwards compatibility in snapshot schemas. |
| RISK-003 | UI confusion if users previously relied on the separate "Escalate" button to highlight blockers. | Low | Provide clear visual guidance in `RequestDetailView.vue`: comments for blockers/questions, Pause Work for putting requests on hold, and Reassign for management interventions. |

---

# 7. Recommendations

Recommend possible approaches for addressing major gaps.

## Option A: Direct State Machine Streamlining with Inline Post Comments (Recommended)

1. Refactor `RequestStatus` to: `Captured`, `Assigned`, `InProgress`, `Paused`, `Completed`, `Cancelled`.
2. Update `Request.cs`:
   - Replace `Accept`, `Evaluate`, `Reject`, `Escalate`, `RequestManagementDecision`, `ApplyManagementDecision` with `StartWork`, `PauseWork`, and `Cancel`.
   - Update `AssignOwner` to transition `Captured` $\rightarrow$ `Assigned`, and reset `InProgress`/`Paused` $\rightarrow$ `Assigned`.
3. Remove `IRequestService.Escalation.cs`, `RequestService.Escalation.cs`, `RequestEscalationCommands.cs`.
4. Update `RequestsController.cs` to expose `POST /api/v1/requests/{id}/start`, `POST /api/v1/requests/{id}/pause`, and `POST /api/v1/requests/{id}/cancel`.
5. Update `RequestDetailView.vue` (`SCR-REQ-003`):
   - Replace complex workflow cards with streamlined action buttons (Start Work, Pause Work, Reassign, Complete, Cancel).
   - Embed an inline Comments & Discussion component querying `/api/v1/posts/{postId}/comments`.
6. Add database migration `0015_simplify_request_lifecycle.sql` updating existing rows and constraints.
7. Update `ManagementAnalyticsService` to track `Paused` requests in place of `Escalated`.

### Advantages

- Eliminates unnecessary state ceremony and administrative overhead.
- Directly satisfies all user requirements and `/grill-me` alignment decisions.
- Reuses existing robust Post/Comments infrastructure without database duplication.
- Leaves a clean, cohesive, and easily maintainable codebase.

### Disadvantages

- Requires modifying multiple layers across `Cakra.Modules.Request`, `Cakra.Api`, `Cakra.Web`, and `Cakra.Modules.Analytics`.

## Option B: Frontend-Only Virtual Simplification

Keep the backend state machine with `EVALUATING`, `ACCEPTED`, `ESCALATED` intact, but hide them in the UI by auto-calling Evaluate/Accept behind the scenes.

### Advantages

- Fewer backend files modified.

### Disadvantages

- Retains underlying architectural bloat and confusing dual-behavior.
- Fails to cleanly support `PAUSED` and `CANCELLED` in the database and analytics.
- Violates the Knowledge-Centric SDLC principle of model integrity.

---

# 8. Gap Closure

Record resolutions for gaps and open questions based on approved alignment.

## GAP-001 / GAP-002 / GAP-003: State Machine, Pause Work & Cancel Request

### Decision

Update `RequestStatus` to define: `Captured = 1`, `Assigned = 2`, `InProgress = 3`, `Paused = 4`, `Completed = 5`, `Cancelled = 6`. Add `PauseWork(string? note, Guid actorPersonId)` and `Cancel(string reason, Guid actorPersonId)` methods to `Request.cs`. Enforce that `StartWork` is strictly executable by the assigned owner.

### Rationale

Directly implements the streamlined lifecycle agreed upon in `/grill-me`, eliminating administrative triage states while introducing explicit support for work suspension and universal cancellation.

### Impact

Changes to `Request.cs`, `RequestStatus.cs`, `RequestResolution.cs`, `ResolutionOutcome.cs`, `RequestService.cs`, `RequestCommands.cs`, and `RequestsController.cs`.

### Architecture Impact

Requires updating aggregate root state invariants, commands, events (`RequestWorkPaused`, `RequestCancelled`, `RequestWorkStarted`), and API contracts.

### Resolved By

User & SDLC Alignment

### Resolved Date

2026-10-06

---

## GAP-004 / GAP-005: Removal of Escalation & Reassignment Reset

### Decision

Delete `IRequestService.Escalation.cs`, `RequestService.Escalation.cs`, and `RequestEscalationCommands.cs`. Remove `EscalateRequest`, `RequestManagementDecision`, and `ApplyManagementDecision` endpoints from `RequestsController.cs`. Modify `AssignOwner` so that reassigning an active request in `InProgress` or `Paused` resets the status to `Assigned`.

### Rationale

Management decisions and escalations are replaced by direct actions (reassignment, pause, cancellation) and comments, eliminating separate docket records.

### Impact

Simplifies request service and controller interfaces; removes obsolete classes and methods.

### Architecture Impact

Reduces domain surface area; removes escalation events (`RequestEscalated`, `ManagementDecisionRequested`).

### Resolved By

User & SDLC Alignment

### Resolved Date

2026-10-06

---

## GAP-006: Embedded Comments & Discussion on Request Detail

### Decision

Embed a dedicated Comments & Discussion card in `RequestDetailView.vue` (`SCR-REQ-003`), querying and posting comments through the Request's linked operational post (`GET/POST /api/v1/posts/{id}/comments`).

### Rationale

Provides direct inline communication for questions, blockers, and assistance without leaving the Request Detail page, replacing formal escalation dockets with collaborative discussion.

### Impact

Enhancement to `RequestDetailView.vue` and `requests.ts` client API.

### Architecture Impact

Leverages cross-module integration between `Cakra.Web` and `Cakra.Modules.Post` without altering domain boundaries.

### Resolved By

User & SDLC Alignment

### Resolved Date

2026-10-06

---

## GAP-007 / GAP-008 / GAP-009: Analytics, Database Migration & UI Views

### Decision

1. Create migration script `0015_simplify_request_lifecycle.sql` updating `[request].[Requests]` rows (`ESCALATED` $\rightarrow$ `PAUSED`, `EVALUATING`/`ACCEPTED` $\rightarrow$ `ASSIGNED`, `REJECTED` $\rightarrow$ `CANCELLED`).
2. Update `ManagementAnalyticsService` and related DTOs to replace `EscalatedCount` / `IsBlocked` with `PausedCount` / `IsPaused`.
3. Update list views (`RequestListView.vue`, `MyRequestsView.vue`, `RequestSearchView.vue`) and analytics views (`CustomerPortfolioView.vue`, `ProgrammerWorkloadView.vue`, `ProgrammerPerformanceView.vue`) to reflect the simplified status filters.

### Rationale

Ensures complete system-wide consistency across persistence, reporting, and user interfaces without orphaned states.

### Impact

Changes across `Cakra.Api/Migrations`, `Cakra.Modules.Analytics`, and `Cakra.Web` views.

### Architecture Impact

Updates persistence schema constraints and analytical query projections.

### Resolved By

User & SDLC Alignment

### Resolved Date

2026-10-06

---

# 9. Architecture Applicability

Record the Architecture Applicability decision.

## Decision

**ARCHITECTURE-REQUIRED**

## Rationale

This change introduces:
1. Significant domain refactoring (state machine restructuring, removing 4 states, adding 2 states, retiring escalation/decision aggregates).
2. Cross-cutting persistence changes (database migration updating existing operational rows and check constraints).
3. API contract modifications (removal of 5 endpoints, introduction of 3 new endpoints).
4. Analytics query model refactoring (`Escalated` $\rightarrow$ `Paused`).
5. Frontend UI enhancements (embedded comments integration on `SCR-REQ-003`).

Therefore, formal target-state architecture definition is required before implementation planning begins.

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

All feasibility gaps, open questions, and operational decisions for CR-016 have been thoroughly analyzed and resolved through user alignment. The readiness checklist is complete. Architect evaluated architecture applicability (ARCHITECTURE-REQUIRED) and granted the READY-FOR-PLANNING gate for technical realization.

---

# 11. References

Referenced artifacts:

- ISSUE: [`CR-016-ISSUE.md`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-016-ISSUE.md)
- DOMAIN: [`request-domain.md`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md), [`CAKRA-DOMAIN.md`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domain/CAKRA-DOMAIN.md)
- FEATURES: [`FEAT-REQ-001`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md) through [`FEAT-REQ-008`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-008-review-request-completion.md)
- ARCHITECTURE: [`CAKRA-ARCHITECTURE.md`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

Referenced codebase locations:

- Aggregate & Status: [`Request.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs), [`RequestStatus.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/RequestStatus.cs)
- Commands & Services: [`RequestCommands.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs), [`RequestService.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs)
- API Controller: [`RequestsController.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/RequestsController.cs)
- Frontend Views: [`RequestDetailView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue), [`RequestListView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestListView.vue)
- Analytics: [`ManagementAnalyticsService.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Analytics/Services/ManagementAnalyticsService.cs)
- Database Migrations: [`Cakra.Api/Migrations/Scripts`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Migrations/Scripts)
