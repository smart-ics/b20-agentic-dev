---
Title: Feasibility Assessment for Request Complexity Rating (1 to 5) (CR-005)
Code: CR-005
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-03
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Change being assessed: Introduction of a standardized **Complexity** rating (`1` to `5`) for operational requests in the Request module (`Cakra.Modules.Request`), as requested in [CR-005-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-005-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-005-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-005-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md), [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md)
- FEATURES: [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md), [FEAT-REQ-003-evaluate-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-003-evaluate-request.md)

## Objective

Assess the feasibility, domain model impact, persistence requirements, authorization rules, command workflows, and planning readiness to:

1. Add a numerical `Complexity` rating bounded between `1` (lowest/baseline) and `5` (highest) to the authoritative `Request` aggregate root and persistence store.
2. Initialize newly recorded requests with default `Complexity = 1` while supporting optional explicit complexity specification at creation time.
3. Permit authorized roles (`Programmer`, `Administrator`, `Team Lead`, and `Manager`) to adjust complexity across any active lifecycle state (`Captured`, `Evaluating`, `Accepted`, `InProgress`, `Escalated`).
4. Enforce strict immutability of complexity once a request enters a closed state (`Completed` or `Rejected`).
5. Dispatch a dedicated domain event (`RequestComplexityUpdated`) to audit complexity changes with previous and new values, actor identity, and an optional justification note.
6. Provide a dedicated application command (`UpdateRequestComplexityCommand`) and integrate an optional complexity parameter into triage evaluation (`EvaluateRequestCommand`).
7. Expose complexity in data transfer objects (`RequestDto`) and UI screens.
8. Establish a safe database migration strategy that backfills existing historical records with `1` and enforces integrity constraints.

---

# 2. Current State

## Existing Behavior

1. **Domain Model (`Cakra.Modules.Request.Domain.Request`)**:
   - The [`Request`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs) aggregate root manages operational properties including `Title`, `Description`, `RequestType`, `Status`, and `Priority` (`LOW`, `NORMAL`, `HIGH`, `URGENT`), but has no concept or attribute for effort sizing or technical complexity.
   - Initial state construction via [`Request.Record`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs#L70-L135) sets `Priority` to `"NORMAL"` when unspecified, but has no parameter for complexity.
   - Triage evaluation via [`Request.Evaluate`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs#L224-L249) records text in `EvaluationNotes` and emits `RequestEvaluated`, but does not alter or establish complexity.
   - There are no methods on `Request` to mutate complexity or validate that complexity falls within an allowed numeric range.

2. **Application Commands & Handlers (`RequestCommands.cs`, `RequestService.cs`)**:
   - [`RecordRequestCommand`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs#L9-L18) accepts title, description, customer ID, product ID, request type, priority, actor ID, and work package ID.
   - [`EvaluateRequestCommand`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs#L91-L95) accepts request ID, evaluation notes, and actor ID.
   - No dedicated command exists to update complexity during active execution (e.g. while in `Accepted` or `InProgress` status).

3. **Persistence & Data Schema (`RequestRepository.cs`, `database-schema-conventions.md`)**:
   - Table `[request].[Requests]` stores request aggregate data. It lacks a `[Complexity]` column.
   - All `SELECT`, `INSERT`, and update queries in [`RequestRepository.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Persistence/RequestRepository.cs) and [`RequestQueryService.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestQueryService.cs) do not reference or map a complexity column.

4. **Authorization**:
   - Application service operations validate the existence of the calling actor via [`IOrganizationQueryService`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization).
   - Role-based checking for technical or administrative authority is available via [`IAuthorizationService`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Services/IAuthorizationService.cs) (`ResolveRolesAsync`), but is not currently invoked for request state mutations.

5. **Read Projections & Frontend**:
   - [`RequestDto`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Models/RequestDto.cs) lacks a `Complexity` property.
   - Frontend views (`RequestDetailView.vue`, `RequestListView.vue`, `MyRequestsView.vue`, `CreateRequestModal.vue`) do not display or collect complexity.

## Existing Constraints

1. **State Machine Invariants**: Closed requests in status `Completed` or `Rejected` cannot undergo operational or lifecycle modifications.
2. **Numeric Boundaries**: Complexity must be strictly bounded between integer `1` and `5`.
3. **Audit Trail Integrity**: Any alteration to complexity must maintain traceability (who changed it, when, from what, to what, and why).
4. **Backward Compatibility**: Existing database records and legacy API consumers must not fail; historical records require clean backfilling to `1`.
5. **No Cross-Schema Direct Writes**: All changes to `[request].[Requests]` must remain internal to the Request module bounded context.

---

# 3. Gap Analysis

| ID | Severity | Gap |
|---|---|---|
| GAP-001 | CRITICAL | `Request` aggregate root lacks `Complexity` attribute, numeric validation invariants (1–5), and business method `SetComplexity(...)`. |
| GAP-002 | CRITICAL | Database schema `[request].[Requests]` lacks `[Complexity]` column, default constraint (`DEFAULT 1`), check constraint (`BETWEEN 1 AND 5`), and repository CRUD mapping. |
| GAP-003 | MAJOR | Application layer lacks dedicated `UpdateRequestComplexityCommand` and does not accept optional `Complexity` in `RecordRequestCommand` or `EvaluateRequestCommand`. |
| GAP-004 | MAJOR | Domain event `RequestComplexityUpdated` does not exist to record and dispatch complexity state changes. |
| GAP-005 | MAJOR | `RequestService` does not check role authorization (`Programmer`, `Administrator`, `Team Lead`, `Manager`) for complexity modifications. |
| GAP-006 | MINOR | `RequestDto` and UI views (`RequestDetailView.vue`, `RequestListView.vue`, `CreateRequestModal.vue`) do not expose or render complexity. |
| GAP-007 | MAJOR | Domain documentation (`request-domain.md`) and Feature specifications (`FEAT-REQ-001`, `FEAT-REQ-003`) lack complexity definitions and rules. |

---

# 4. Open Questions

| ID | Question | Impact |
|---|---|---|
| OQ-001 | Can closed requests (`Completed` or `Rejected`) ever have their complexity adjusted for retrospective/reporting purposes? | Domain aggregate invariants and state machine rules. |
| OQ-002 | How should existing historical requests in the database be handled during migration? | Migration script design, backward compatibility, and data integrity. |
| OQ-003 | Is a justification/reason note required when updating complexity, or is it optional? | Command validation rules, UI input requirements, and user experience. |
| OQ-004 | Should request grid filtering and sorting by complexity be included in this change? | Query service scope, grid index performance, and UI layout changes. |

---

# 5. Assumptions

| ID | Assumption |
|---|---|
| ASM-001 | Complexity is a pure integer score between 1 and 5 governed by team convention, without system-enforced time durations or hourly estimates. |
| ASM-002 | Defaulting complexity to 1 upon creation satisfies operational requirements for requests recorded without upfront technical triage. |
| ASM-003 | Role validation can be resolved using `IAuthorizationService.ResolveRolesAsync` or organization role queries for the acting `PersonId`. |
| ASM-004 | Frontend changes to display complexity in `RequestDetailView.vue` and `RequestListView.vue` will be non-intrusive metadata badges. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| RISK-001 | Applying a `NOT NULL` constraint without backfill could fail database migration on existing environments. | High | Migration DDL must add column with `DEFAULT 1` and update existing rows before creating strict constraints. |
| RISK-002 | Unauthorized actors attempting to modify complexity could compromise project planning accuracy. | Medium | Enforce role check in `RequestService` verifying caller holds `Programmer`, `Administrator`, `Team Lead`, or `Manager` before executing update. |
| RISK-003 | Complexity modification on completed work could distort historical metrics and completion audit trails. | Medium | Domain model strictly throws `InvalidRequestStateTransitionException` if status is `Completed` or `Rejected`. |

---

# 7. Recommendations

## Option A: Comprehensive Domain Integration (Recommended)

Incorporate `Complexity` as a first-class domain attribute on `Request`:
- Add `Complexity` (1–5) to `Request.cs`, defaulting to `1`.
- Provide `SetComplexity(...)` method emitting `RequestComplexityUpdated`.
- Support optional complexity parameter in `RecordRequestCommand` and `EvaluateRequestCommand`.
- Provide dedicated `UpdateRequestComplexityCommand` for updates during any active state.
- Enforce role check in application service (`Programmer`, `Administrator`, `Team Lead`, `Manager`).
- Add SQL column with `DEFAULT 1` and `CHECK (Complexity BETWEEN 1 AND 5)`.
- Expose in `RequestDto` and show on UI views as an operational badge.

### Advantages

- Clean domain modeling aligned with DDD principles and Cakra architecture standards.
- Full auditability via domain events without losing historical context.
- High operational flexibility: can be set at intake, during triage evaluation, or during in-progress execution.
- Prevents invalid values at compile, domain validation, and database constraint levels.

### Disadvantages

- Requires coordination across domain, application, persistence, and UI layers.

## Option B: Evaluation-Only Metadata Property

Treat complexity as an optional evaluation-time metadata field, settable only via `EvaluateRequestCommand` without dedicated commands or creation-time defaults.

### Advantages

- Slightly fewer command contracts.

### Disadvantages

- Does not satisfy the requirement to have complexity on *each* request.
- Cannot adjust complexity when discovery happens during `InProgress` or `Accepted` states.
- Requesters who know complexity upfront cannot submit it during recording.

---

# 8. Gap Closure

Record resolutions for gaps and open questions.

## GAP-001

### Status

CLOSED

### Decision

Implement Option A: Add `public int Complexity { get; private set; } = 1;` to `Request.cs` with validation enforcing `1 <= complexity <= 5`, and a `SetComplexity` method.

### Rationale

Establishes authoritative domain ownership and guarantees that every Request aggregate holds a valid complexity score.

### Impact

`Request.cs` constructor, `Record(...)` factory, and state mutation methods will incorporate `Complexity`.

### Architecture Impact

Requires updating `Request.cs` in `Cakra.Modules.Request.Domain`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## GAP-002

### Status

CLOSED

### Decision

Add `[Complexity] INT NOT NULL CONSTRAINT [DF_Requests_Complexity] DEFAULT 1` to `[request].[Requests]` with constraint `CHECK ([Complexity] BETWEEN 1 AND 5)`. Update `RequestRepository.cs` and `RequestQueryService.cs` SQL statements.

### Rationale

Ensures relational integrity and matches domain invariants with zero data loss or null values.

### Impact

Database schema migration and Dapper query mappings updated.

### Architecture Impact

Requires DDL migration script and persistence mapping updates in `Cakra.Modules.Request.Persistence`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## GAP-003

### Status

CLOSED

### Decision

Create `UpdateRequestComplexityCommand` and update `RecordRequestCommand` and `EvaluateRequestCommand` to include an optional `int? Complexity = null` parameter.

### Rationale

Provides flexible operational entry points while maintaining clean separation of concerns.

### Impact

Application service command interfaces and validators updated in `Cakra.Modules.Request.Services`.

### Architecture Impact

Requires new command class, validation rules, and service handler methods.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## GAP-004

### Status

CLOSED

### Decision

Create domain event `RequestComplexityUpdated(Guid RequestId, int PreviousComplexity, int NewComplexity, Guid ActorPersonId, string? Reason, DateTime OccurredAtUtc)`.

### Rationale

Preserves full auditability and enables reactive notifications or analytics projections.

### Impact

Emitted whenever `SetComplexity(...)` alters the complexity value.

### Architecture Impact

Requires new event record in `Cakra.Modules.Request.Domain.Events`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## GAP-005

### Status

CLOSED

### Decision

Enforce role authorization checks in `RequestService`: verify the actor has `Programmer`, `Administrator`, `Team Lead`, or `Manager` role before executing complexity modifications.

### Rationale

Protects operational sizing integrity by restricting adjustments to qualified technical and operational personnel.

### Impact

Commands rejected with authorization errors when invoked by non-technical/non-management actors.

### Architecture Impact

Integrates role verification into `RequestService` command handlers.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## GAP-006

### Status

CLOSED

### Decision

Include `public int Complexity { get; init; }` in `RequestDto`. Update frontend detail and list components to render a complexity badge.

### Rationale

Provides visibility of complexity to operators, developers, and managers across screens.

### Impact

Read models and UI views reflect complexity score.

### Architecture Impact

Updates `RequestDto.cs` and Vue frontend components.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## GAP-007

### Status

CLOSED

### Decision

Update `request-domain.md`, `FEAT-REQ-001`, and `FEAT-REQ-003` to formally define `Complexity`, its default behavior, role permissions, and lifecycle rules.

### Rationale

Maintains authoritative working and permanent business knowledge synchronization per SDLC manifesto.

### Impact

Documentation updated to reflect new business capability.

### Architecture Impact

Requires updating domain and feature artifacts in `cakra/docs/`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## OQ-001

### Status

CLOSED

### Decision

Strictly reject complexity updates on closed requests (`Completed` or `Rejected`) by throwing `InvalidRequestStateTransitionException`.

### Rationale

Aligns with core domain state machine rules: closed requests are immutable operational historical records.

### Impact

Ensures historical completion records cannot be retroactively altered.

### Architecture Impact

Enforced within `Request.SetComplexity` domain method.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## OQ-002

### Status

CLOSED

### Decision

Backfill all existing database rows with default value `1` as part of the schema migration.

### Rationale

Satisfies the requirement that *each* request has a complexity value between 1 and 5 without introducing nullable fields or migration errors.

### Impact

All existing records receive an initial baseline score of 1.

### Architecture Impact

Included in migration DDL.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## OQ-003

### Status

CLOSED

### Decision

Make the justification/reason note optional when modifying complexity.

### Rationale

Minimizes administrative friction for minor adjustments while allowing developers or managers to document context when shifting estimates.

### Impact

`Reason` parameter defaults to `null` and is bounded to max 500 characters when provided.

### Architecture Impact

Validator allows null/empty reason while enforcing max length when present.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## OQ-004

### Status

CLOSED

### Decision

Include complexity in `RequestDto` for immediate rendering in details and grid views, but defer dedicated grid column filtering/range queries to a subsequent enhancement.

### Rationale

Keeps the initial change focused and minimizes blast radius on query index design while delivering immediate visibility.

### Impact

No index changes or new grid query parameters needed in this release.

### Architecture Impact

Read model projections map the column directly.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

# 9. Architecture Applicability

Record the Architecture Applicability decision.

## Decision

ARCHITECTURE-REQUIRED

## Rationale

Introducing Request Complexity alters the authoritative aggregate root (`Request`), persistence schema (`[request].[Requests]`), DDL constraints, application service command contracts (`UpdateRequestComplexityCommand`, `RecordRequestCommand`, `EvaluateRequestCommand`), domain events (`RequestComplexityUpdated`), authorization rules, and frontend views. A formal architecture update is necessary to define the technical realization, schema migration, and implementation plan slices before execution begins.

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

All feasibility analysis, gap definitions, open questions, and closure decisions have been completed, verified, and accepted. Gate `READY-FOR-PLANNING` is granted by `ica-architect`. Technical realization will be defined in target architecture artifact `CR-005-ARCHITECTURE.md`.

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-005-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-005-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- DOMAIN: [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md)
- FEATURE: [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md)
- FEATURE: [FEAT-REQ-003-evaluate-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-003-evaluate-request.md)

Referenced codebase locations:

- [Request.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs)
- [RequestStatus.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/RequestStatus.cs)
- [RequestCommands.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs)
- [RequestService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs)
- [RequestDto.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Models/RequestDto.cs)
- [RequestRepository.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Persistence/RequestRepository.cs)
- [RequestQueryService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestQueryService.cs)
- [IAuthorizationService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Services/IAuthorizationService.cs)
