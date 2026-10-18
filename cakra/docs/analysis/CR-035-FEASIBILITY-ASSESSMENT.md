---
Title: Feasibility Assessment for Work Package Re-Open Capability (CR-035)
Code: CR-035
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-16
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Change being assessed: Controlled capability to re-open a previously closed Work Package back into `DRAFT` state when constituent operational requests remain unfinished, enforcing integrity invariants, audit trails, and UI affordances, as formally captured in [CR-035-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-035-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-035-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-035-ISSUE.md)
- DOMAIN: [work-package-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/work-package-domain.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- ARCHITECTURE (prior): [CR-025-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-025-ARCHITECTURE.md)
- ARCHITECTURE (system): [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- UI SPECIFICATION: [Work Package Screen (`SCR-WP-001`)](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue)

## Objective

Assess technical feasibility, domain integrity constraints, cross-module boundaries, and architectural impact to:

1. Introduce an aggregate lifecycle transition from `CLOSED` back to `DRAFT` (`CLOSED` $\rightarrow$ `DRAFT`) on `WorkPackage.cs`.
2. Enforce strict eligibility gating: re-opening is allowed **only** if the Work Package contains at least one active constituent request in an unfinished state (`Status NOT IN ('COMPLETED', 'CANCELLED', 'REJECTED')`). Fully completed, cancelled, or empty packages remain permanently closed.
3. Enforce Business Rule 9 (*"A Request may belong to zero or one Work Package"*): block re-opening if any unfinished constituent request was linked to another active or draft Work Package while this package was closed.
4. Require a mandatory justification reason for re-opening, reset `ClosedReason` and `ClosedAt` to `null` on the aggregate root, and emit a `WorkPackageReopened` domain event capturing the reopen reason and prior closure history.
5. Provide optional deadline update capability during re-opening, alerting the user if the prior target deadline is already in the past.
6. Restrict re-opening authority to the assigned Work Package Owner, Management, or Administrator.
7. Expose a dedicated command and REST endpoint `POST /api/v1/work-packages/{id}/reopen`.
8. Implement frontend action and modal dialog on `SCR-WP-001` ([`WorkPackageView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue)) with clear eligibility feedback and tooltips.

---

# 2. Current State

## Existing Behavior

1. **Domain Aggregate Lifecycle ([`WorkPackage.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackage.cs))**:
   - `WorkPackage` aggregate manages lifecycle via `Status` (`WorkPackageStatus.Draft`, `Active`, `Closed`).
   - The state machine is strictly unidirectional: `DRAFT` $\rightarrow$ `ACTIVE` $\rightarrow$ `CLOSED` or `DRAFT` $\rightarrow$ `CLOSED`.
   - `Activate()` explicitly forbids activation from `Closed`, throwing `InvalidWorkPackageStateTransitionException` (*"Work Package '{Id}' is CLOSED and cannot be activated."*).
   - No method exists on `WorkPackage` to transition out of `CLOSED`.
   - `Close(string reason, DateTime? closedAtUtc)` records `ClosedReason`, `ClosedAt`, updates `Status = WorkPackageStatus.Closed`, and emits `WorkPackageClosed`. It does not remove or detach constituent requests (`RemovedAt` remains `null`).

2. **Constituent Requests & Business Rule 9 ([`WorkPackageRequest.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackageRequest.cs), [`WorkPackageRepository.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Persistence/WorkPackageRepository.cs))**:
   - Table `[workpackage].[WorkPackageRequests]` links requests to work packages.
   - `WorkPackageRepository.IsRequestInActiveWorkPackageAsync` checks:
     ```sql
     SELECT 1 FROM [workpackage].[WorkPackageRequests] wpr
     INNER JOIN [workpackage].[WorkPackages] wp ON wp.[Id] = wpr.[WorkPackageId]
     WHERE wpr.[RequestId] = @RequestId
       AND wpr.[RemovedAt] IS NULL
       AND wp.[Status] IN ('DRAFT', 'ACTIVE')
       AND (@ExcludingWorkPackageId IS NULL OR wpr.[WorkPackageId] <> @ExcludingWorkPackageId)
     ```
   - When a package is `CLOSED`, its linked requests are excluded from this query filter. Consequently, other active/draft packages can legally claim those requests while this package is closed.

3. **Command & Service Layer ([`WorkPackageService.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageService.cs))**:
   - `WorkPackageService` provides `CreateWorkPackageAsync`, `UpdateObjectiveAsync`, `UpdateDeadlineAsync`, `UpdateContextAsync`, `AssignOwnerAsync`, `ActivateWorkPackageAsync`, `CloseWorkPackageAsync`, `AddRequestToWorkPackageAsync`, `RemoveRequestFromWorkPackageAsync`, and `ReorderRequestsAsync`.
   - There is no command record or service method for re-opening a closed package.

4. **API Layer ([`WorkPackagesController.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/WorkPackagesController.cs))**:
   - Exposes `POST /api/v1/work-packages/{id}/activate` and `POST /api/v1/work-packages/{id}/close`.
   - No `POST /api/v1/work-packages/{id}/reopen` endpoint exists.

5. **Frontend Application ([`workpackages.ts`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/workpackages.ts), [`WorkPackageView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue))**:
   - `WorkPackageView.vue` restricts lifecycle actions via `canActivate = computed(() => isSelectedDraft.value)` and `canClose = computed(() => isSelectedDraft.value || isSelectedActive.value)`.
   - When `isSelectedClosed` is true, all modification forms, inputs, and lifecycle action buttons are disabled or hidden.
   - There is no button, form, or dialog to re-open a closed package.

## Existing Constraints

1. **Business Rule 9 Invariant**: A Request may belong to zero or one active/draft Work Package at any given time. Re-opening a Work Package must not produce duplicate active memberships for any request.
2. **Business Rule 13**: Closing a Work Package does not imply that every Request within it is closed; constituent request lifecycles are autonomous and owned by the Request domain.
3. **Database Schema Constraints**:
   - Table `[workpackage].[WorkPackages]` schema:
     - `[Status] VARCHAR(20) NOT NULL` (stores `'DRAFT'`, `'ACTIVE'`, `'CLOSED'`)
     - `[ClosedReason] NVARCHAR(MAX) NULL`
     - `[ClosedAt] DATETIME2 NULL`
     - `[Deadline] DATETIME2 NULL`
     - `[UpdatedAt] DATETIME2 NOT NULL`
   - All target columns already exist and accept nulls; **zero database schema migrations** are required.
4. **Operations Cockpit Telemetry Boundary (`CR-025`)**:
   - `GetOperationsCockpitQueryHandler` and `WorkPackageTelemetryCalculator` strictly filter `wp.[Status] = 'ACTIVE'`.
   - Packages in `DRAFT` are excluded from demand density, scope pressure, capacity share, and health invariants.

---

# 3. Gap Analysis

| ID | Severity | Gap |
| :--- | :--- | :--- |
| **GAP-001** | CRITICAL | **Aggregate State Transition (`CLOSED` $\rightarrow$ `DRAFT`)**: [`WorkPackage.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackage.cs) lacks a `Reopen(...)` method and rejects any state change out of `CLOSED`. |
| **GAP-002** | CRITICAL | **Domain Event**: No `WorkPackageReopened` domain event exists to record the re-opening event, reason, previous closure details, and timestamp. |
| **GAP-003** | CRITICAL | **Application Command & Multi-Gate Validation**: [`WorkPackageService.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageService.cs) lacks `ReopenWorkPackageCommand`, validator, and validation pipeline (unfinished request check, cross-package conflict check, actor role authorization). |
| **GAP-004** | MAJOR | **API Endpoint**: [`WorkPackagesController.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/WorkPackagesController.cs) lacks `POST /api/v1/work-packages/{id}/reopen` accepting `{ reason, deadline }`. |
| **GAP-005** | MAJOR | **Frontend API Client**: [`workpackages.ts`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/workpackages.ts) lacks `reopenWorkPackage` function and `ReopenWorkPackagePayload` interface. |
| **GAP-006** | MAJOR | **UI Controls & Modal Dialog**: [`WorkPackageView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue) lacks the "Re-Open Work Package" action button, eligibility evaluation logic, and interactive modal dialog with expired-deadline warning. |
| **GAP-007** | MINOR | **Domain Specification Alignment**: [`work-package-domain.md`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/work-package-domain.md) Section 9 documents only unidirectional `DRAFT` $\rightarrow$ `ACTIVE` $\rightarrow$ `CLOSED` and must be updated to include `CLOSED` $\rightarrow$ `DRAFT`. |

---

# 4. Open Questions

| ID | Question | Impact |
| :--- | :--- | :--- |
| **OQ-001** | What target lifecycle state should the package transition into upon re-opening? | Determines whether the package resumes active telemetry immediately or enters preparatory review. |
| **OQ-002** | How strictly should the "unfinished requests" gate be enforced for empty packages or packages where all requests are completed? | Determines whether empty or completed packages can ever be re-opened. |
| **OQ-003** | How should the system handle a situation where an unfinished request was claimed by another active Work Package while this package was closed? | Enforces Business Rule 9 integrity and prevents duplicate active request memberships. |
| **OQ-004** | How should previous closure fields (`ClosedReason`, `ClosedAt`) be handled on the aggregate root upon re-opening? | Affects aggregate state cleanliness and audit trail preservation. |
| **OQ-005** | Which roles have the authority to re-open a closed Work Package? | Defines security and authorization boundaries for the command. |
| **OQ-006** | How should an expired target deadline be handled during re-opening? | Prevents accidental immediate deadline breach upon subsequent reactivation. |

---

# 5. Assumptions

| ID | Assumption |
| :--- | :--- |
| **ASM-001** | An operational request is considered "finished" if and only if its status is `COMPLETED`, `CANCELLED`, or `REJECTED`. All other statuses (`DRAFT`, `SUBMITTED`, `EVALUATED`, `ACCEPTED`, `IN_PROGRESS`, `PAUSED`) are considered unfinished. |
| **ASM-002** | Zero database schema migrations (`.sql` scripts) are required because `[workpackage].[WorkPackages]` already contains `[Status]`, `[ClosedReason]`, `[ClosedAt]`, `[Deadline]`, and `[UpdatedAt]`. |
| **ASM-003** | Re-opening a package into `DRAFT` does not immediately impact the Operations Cockpit or telemetry calculations, as telemetry explicitly consumes only `ACTIVE` packages. |
| **ASM-004** | Live request statuses must be queried synchronously via `IRequestQueryService` during command execution to avoid stale read-model race conditions. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
| :--- | :--- | :--- | :--- |
| **RISK-001** | **Concurrent Request Completion Race Condition**: The last open request is completed in the Request domain immediately before or during Re-Open submission. | Package could be re-opened even though no unfinished work remains. | Query live request statuses from `IRequestQueryService` synchronously inside the transactional command handler before mutating status. |
| **RISK-002** | **Concurrent Cross-Package Request Assignment (BR-9 Race)**: Another user assigns an unfinished request to another active package concurrently with Re-Open. | Request belongs to two active/draft packages, violating Business Rule 9. | Execute `_workPackageRepository.IsRequestInActiveWorkPackageAsync` within the database transaction prior to updating package status. |
| **RISK-003** | **Expired Deadline Telemetry Shock**: Reopening a package with an old deadline and activating it immediately triggers `DEADLINE_BREACHED` and `CRITICAL/IMPOSSIBLE` pressure tiers in the Cockpit. | Distorts executive telemetry and alarms leadership. | In-modal alert highlighting expired deadline with an optional date picker to set an updated deadline during re-opening, plus UI warnings in DRAFT state. |

---

# 7. Recommendations

## Option A: Controlled Re-Open into DRAFT with Multi-Gate Integrity Verification (Recommended)

Transition `CLOSED` $\rightarrow$ `DRAFT`. Enforce a 3-tier validation gate in `WorkPackageService`:
1. **Authorization**: Caller is Work Package Owner, Management, or Administrator.
2. **Eligibility**: Package contains $\ge 1$ constituent request with status not in `['COMPLETED', 'CANCELLED', 'REJECTED']`.
3. **Integrity (BR-9)**: None of the constituent unfinished requests are currently active in another Work Package.
Require a mandatory reason, clear `ClosedReason`/`ClosedAt` on the aggregate root, emit `WorkPackageReopened`, and provide optional deadline override.

### Advantages
- Safe and reviewable: the owner can inspect scope, re-negotiate deadlines, and resolve blockers in `DRAFT` before committing to active telemetry.
- Zero telemetry pollution: `DRAFT` packages are ignored by the Cockpit.
- Strictly protects Business Rule 9 and historical integrity.
- Fully auditable via domain event.

### Disadvantages
- Requires a two-step flow (Re-Open to Draft, then Activate) if the user wants to resume operational execution immediately.

---

## Option B: Direct Re-Activation into ACTIVE

Transition `CLOSED` $\rightarrow$ `ACTIVE` directly, resuming execution immediately.

### Advantages
- Single-click action for the user.

### Disadvantages
- High risk of immediate `DEADLINE_BREACHED` telemetry alarms if the original deadline expired while closed.
- Bypasses the preparatory review opportunity in `DRAFT`.
- If request conflicts exist, fails catastrophically or risks violating Business Rule 9.

---

# 8. Gap Closure

## GAP-001

Status: CLOSED

### Decision

Add a domain method `public void Reopen(string reason, DateTime? deadline = null, DateTime? reopenedAtUtc = null)` to `WorkPackage.cs` that transitions status from `Closed` to `Draft`. Update `WorkPackageStatus` domain validation to recognize `CLOSED` $\rightarrow$ `DRAFT` as a valid state transition.

### Rationale

Empowers the aggregate root as the authoritative guardian of lifecycle invariants while enabling controlled re-entry into the preparatory lifecycle stage.

### Impact

Modifies `WorkPackage.cs` domain entity and `WorkPackageStatus.cs`.

### Architecture Impact

Requires architectural specification of aggregate method signature, invariant checks, and state mutations.

### Resolved By

User & Analyst (Grill-Me Alignment)

### Resolved Date

2026-10-16

---

## GAP-002

Status: CLOSED

### Decision

Create domain event `WorkPackageReopened(Guid WorkPackageId, string Reason, string? PreviousClosedReason, DateTime? PreviousClosedAt, DateTime ReopenedAt)` in `Cakra.Modules.WorkPackage.Domain.Events`.

### Rationale

Maintains complete domain auditability for lifecycle reversibility, preserving prior closure history in domain events without retaining stale closure values on the active aggregate.

### Impact

New event class in `Domain/Events`.

### Architecture Impact

Requires event dispatching contract in application service and event logging.

### Resolved By

User & Analyst (Grill-Me Alignment)

### Resolved Date

2026-10-16

---

## GAP-003

Status: CLOSED

### Decision

Implement `ReopenWorkPackageCommand(Guid WorkPackageId, string Reason, DateTime? NewDeadline = null)` and `ReopenWorkPackageCommandValidator` (validating `WorkPackageId` not empty and `Reason` not empty). In `WorkPackageService.cs`:
1. Verify caller is Work Package Owner, Management, or Administrator.
2. Query constituent requests and verify through `IRequestQueryService` that at least one request is unfinished (not `COMPLETED`, `CANCELLED`, or `REJECTED`).
3. Query `_workPackageRepository.IsRequestInActiveWorkPackageAsync` for each unfinished request. If any conflict exists, throw `InvalidOperationException` with details of conflicting request(s).
4. Invoke `workPackage.Reopen(reason, newDeadline, now)`, persist via repository, and dispatch domain events.

### Rationale

Centralizes multi-gate validation and transactional integrity in the application service layer, preventing invalid or conflicting re-open attempts.

### Impact

New command record, validator, and service implementation in `Cakra.Modules.WorkPackage.Services`.

### Architecture Impact

Requires service interface contract update and MediatR handler wiring.

### Resolved By

User & Analyst (Grill-Me Alignment)

### Resolved Date

2026-10-16

---

## GAP-004

Status: CLOSED

### Decision

Expose endpoint `POST /api/v1/work-packages/{id}/reopen` in `WorkPackagesController.cs` accepting `ReopenWorkPackageRequest(string Reason, DateTime? Deadline)`.

### Rationale

Provides standard RESTful HTTP interface consistent with `/activate` and `/close` endpoints.

### Impact

New controller action returning `WorkPackageDto`.

### Architecture Impact

API route and request/response model definition.

### Resolved By

User & Analyst (Grill-Me Alignment)

### Resolved Date

2026-10-16

---

## GAP-005

Status: CLOSED

### Decision

Add `ReopenWorkPackagePayload` interface and `reopenWorkPackage(id: string, payload: ReopenWorkPackagePayload): Promise<WorkPackageDto>` in `src/api/workpackages.ts`.

### Rationale

Standard typed API client integration for frontend consumers.

### Impact

API client functions in `workpackages.ts`.

### Architecture Impact

Frontend API client contract.

### Resolved By

User & Analyst (Grill-Me Alignment)

### Resolved Date

2026-10-16

---

## GAP-006

Status: CLOSED

### Decision

In `WorkPackageView.vue`:
1. Compute `canReopen`: user has authorized role (`Owner`, `Management`, `Administrator`) and package `status === 'CLOSED'`.
2. Compute `reopenEligibility`: evaluate whether active scope items contain at least one unfinished request.
3. Render "Re-Open Work Package" button in the Detail Panel action strip. If ineligible, disable the button with tooltip: *"Cannot re-open: all requests in this package are completed or cancelled."*
4. Render Re-Open Modal dialog displaying:
   - Summary of unfinished requests.
   - Alert banner if current deadline is in the past.
   - Optional updated deadline input.
   - Mandatory justification reason textarea.
   - Confirm and Cancel buttons with loading spinner.

### Rationale

Delivers clear visual feedback, prevents user confusion regarding disabled actions, and guides the user through safe deadline updates and reason capture.

### Impact

Template, reactive state, and handler additions in `WorkPackageView.vue`.

### Architecture Impact

UI component and modal design specification in Architecture TD.

### Resolved By

User & Analyst (Grill-Me Alignment)

### Resolved Date

2026-10-16

---

## GAP-007

Status: CLOSED

### Decision

Update `work-package-domain.md` Section 9 to document the `CLOSED` $\rightarrow$ `DRAFT` transition and update Section 8 with business rules governing re-open eligibility, conflict prevention, and justification requirements.

### Rationale

Preserves domain documentation integrity as the single source of truth for business knowledge.

### Impact

Updates `cakra/docs/domains/work-package-domain.md`.

### Architecture Impact

Authoritative domain documentation input for architecture and planning.

### Resolved By

User & Analyst (Grill-Me Alignment)

### Resolved Date

2026-10-16

---

## OQ-001

Status: CLOSED

### Decision

Re-open into `DRAFT` state (`CLOSED` $\rightarrow$ `DRAFT`).

### Rationale

Enables preparatory review and prevents premature telemetry ingestion until the team deliberately activates the package.

### Impact

Domain state transition destination is strictly `Draft`.

### Architecture Impact

Defined in TD-001.

### Resolved By

User & Analyst (Grill-Me Alignment)

### Resolved Date

2026-10-16

---

## OQ-002

Status: CLOSED

### Decision

Strictly block re-opening if all requests are completed or cancelled, or if the package has zero requests. Closure is permanent in these cases; any new work requires creating a new Work Package.

### Rationale

Protects historical metrics and audit boundaries. Completed packages represent finalized historical commitments.

### Impact

Eligibility check blocks packages without unfinished requests.

### Architecture Impact

Defined in TD-002.

### Resolved By

User & Analyst (Grill-Me Alignment)

### Resolved Date

2026-10-16

---

## OQ-003

Status: CLOSED

### Decision

Block re-opening with a descriptive validation error listing conflicting request(s) if any unfinished request is currently linked to another active or draft Work Package.

### Rationale

Preserves Business Rule 9 invariant without silent scope detachment or destructive reassignment.

### Impact

Transactional integrity check in `WorkPackageService.cs`.

### Architecture Impact

Defined in TD-002.

### Resolved By

User & Analyst (Grill-Me Alignment)

### Resolved Date

2026-10-16

---

## OQ-004

Status: CLOSED

### Decision

Reset `ClosedReason` and `ClosedAt` to `null` on the aggregate root upon re-opening, while capturing both prior closure values in the `WorkPackageReopened` domain event.

### Rationale

Maintains clean state representation on the active entity while preserving full historical traceability in event streams and audit logs.

### Impact

`WorkPackage.cs` resets properties; event payload carries audit history.

### Architecture Impact

Defined in TD-001.

### Resolved By

User & Analyst (Grill-Me Alignment)

### Resolved Date

2026-10-16

---

## OQ-005

Status: CLOSED

### Decision

Restrict re-opening authority to the assigned Work Package Owner, Management, or Administrator.

### Rationale

Balances operational flexibility for the directly responsible owner with organizational governance oversight by leadership.

### Impact

Role and owner validation in command handler and frontend UI computed property.

### Architecture Impact

Defined in TD-002.

### Resolved By

User & Analyst (Grill-Me Alignment)

### Resolved Date

2026-10-16

---

## OQ-006

Status: CLOSED

### Decision

Preserve the existing deadline by default, while offering an optional new deadline date picker in the Re-Open modal dialog accompanied by an alert banner if the existing deadline is in the past.

### Rationale

Eliminates accidental deadline breaches upon reactivation without forcing re-entry of still-valid deadlines.

### Impact

Modal dialog UI inputs and optional command parameter.

### Architecture Impact

Defined in TD-003.

### Resolved By

User & Analyst (Grill-Me Alignment)

### Resolved Date

2026-10-16

---

# 9. Architecture Applicability

## Decision

ARCHITECTURE-REQUIRED

## Rationale

This change requires formal target-state architecture definition because it introduces:

1. A new aggregate lifecycle state transition (`CLOSED` $\rightarrow$ `DRAFT`) modifying the fundamental state machine of `WorkPackage`.
2. A new domain event (`WorkPackageReopened`) and aggregate mutation method.
3. Cross-module synchronous query orchestration between `Cakra.Modules.WorkPackage` and `Cakra.Modules.Request` (`IRequestQueryService`) to validate live request statuses.
4. Transactional conflict validation enforcing Business Rule 9 across `WorkPackageRequests`.
5. A new REST API endpoint and payload contracts.
6. New interactive UI components (Re-Open button, eligibility evaluation, and modal dialog) on `SCR-WP-001`.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved
- [x] All required decisions recorded
- [x] All blocking open questions resolved
- [x] Architecture can be finalized or updated

## Status

READY-FOR-PLANNING

## Notes

All critical gaps (`GAP-001` through `GAP-007`) and open questions (`OQ-001` through `OQ-006`) have been thoroughly evaluated, resolved, and documented with explicit decisions and architectural impacts based on the user-aligned grill-me session.

Gate READY-FOR-PLANNING is formally granted by the Architect on 2026-10-16. Architecture Applicability is confirmed as ARCHITECTURE-REQUIRED. Target architecture is formally specified in [CR-035-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-035-ARCHITECTURE.md).

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-035-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-035-ISSUE.md)
- DOMAIN: [work-package-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/work-package-domain.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- ARCHITECTURE (prior): [CR-025-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-025-ARCHITECTURE.md)

Referenced codebase locations:

- [`WorkPackage.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackage.cs) — Aggregate root lifecycle methods
- [`WorkPackageStatus.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackageStatus.cs) — Status enum and naming helpers
- [`WorkPackageRepository.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Persistence/WorkPackageRepository.cs) — Dapper persistence and active package membership check
- [`WorkPackageService.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageService.cs) — Command execution and validation
- [`WorkPackagesController.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/WorkPackagesController.cs) — REST API endpoints
- [`workpackages.ts`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/workpackages.ts) — Frontend API client
- [`WorkPackageView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue) — Work Package screen (`SCR-WP-001`)
