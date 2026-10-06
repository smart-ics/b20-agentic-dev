---
Title: Feasibility Assessment for Optional Target Deadline for Request Aggregate and Screens (CR-020)
Code: CR-020
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-06
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Change being assessed: Implementation of an optional target deadline attribute for operational requests, including aggregate lifecycle rules, database persistence, automated timeline audit trail logging, frontend creation/edit forms, and overdue visualization, as formally captured in [CR-020-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-020-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-020-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-020-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- UI LAYOUT: [15-scr-req-003.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/15-scr-req-003.md)
- FEATURES: [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md)

## Objective

Assess the feasibility, domain boundaries, schema changes, audit trail mechanics, UI/UX interaction patterns, and planning readiness to:

1. Add an optional `Deadline` property (`DateTime?`, UTC) to the `Request` aggregate root and database schema `[request].[Requests]`.
2. Allow setting the deadline optionally during initial request recording (`Request.Record(...)`, `POST /api/v1/requests`).
3. Allow updating or clearing (`null`) the deadline on active requests via `Request.UpdateCoreAttributes(...)` and `PUT /api/v1/requests/{id}/core-attributes`.
4. Enforce lifecycle invariants locking deadline changes once a request is closed (`COMPLETED`, `CANCELLED`).
5. Automatically record an audit trail entry on `RequestAssignment` whenever the deadline is set, modified, or cleared, capturing a clear timeline note (e.g., `Deadline set to YYYY-MM-DD`, `Deadline changed from YYYY-MM-DD to YYYY-MM-DD`, or `Deadline cleared`).
6. Expose `Deadline` in `RequestDto` and `RequestDetailDto`.
7. Add date input controls in `CreateRequestModal.vue`, `CreateRequestView.vue`, and the Edit Request modal in `RequestDetailView.vue`.
8. Display the deadline in the Request Information card on `RequestDetailView.vue` and present a prominent "Overdue" badge when `Deadline < Today` for open requests.

---

# 2. Current State

## Existing Behavior

1. **Domain Model (`Cakra.Modules.Request`, `Request.cs`)**:
   - `Request` contains `Title`, `Description`, `RequestType`, `Status`, `Priority`, `Complexity`, and linkages (`CustomerId`, `ProductId`, `WorkPackageId`, `OwnerPersonId`).
   - `Request` has no `Deadline` property.
   - `Record(...)` accepts core fields without deadline.
   - `UpdateCoreAttributes(...)` mutates `Title`, `Description`, `Priority`, and `RequestType`, emitting `RequestCoreAttributesUpdated`, but does not support deadline mutation.
   - `RequestAssignment` tracks ownership and lifecycle state changes; it is currently created during initial record, assignment/reassignment, start work, pause work, complete, and cancel.

2. **Persistence Layer (`Cakra.Modules.Request.Persistence`)**:
   - `0006_request_tables.sql` created `[request].[Requests]` without a `Deadline` column.
   - `RequestRepository.cs` maps columns in SQL queries (`SELECT`, `INSERT INTO [request].[Requests]`, `UPDATE [request].[Requests]`). None of these queries reference `Deadline`.

3. **API & Service Layer (`Cakra.Api`, `Cakra.Modules.Request.Services`)**:
   - `RecordRequestCommand` and `UpdateRequestCoreAttributesCommand` do not accept a deadline parameter.
   - `RequestDto` does not expose a deadline field.
   - `RequestsController.cs` has `POST /api/v1/requests` and `PUT /api/v1/requests/{id}/core-attributes` (and `PUT /api/v1/requests/{id}`). Neither accepts or returns a deadline.

4. **Frontend Applications (`Cakra.Web`)**:
   - `CreateRequestModal.vue` and `CreateRequestView.vue` provide form inputs for title, description, customer, product, request type, priority, and complexity. No date picker for deadline exists.
   - `RequestDetailView.vue` renders an information card with Status, Priority, Complexity, Customer, Product, Owner, CreatedAt, and UpdatedAt. No deadline is rendered.
   - The Edit Request modal in `RequestDetailView.vue` only captures title, description, type, and priority.
   - There is no overdue calculation or visual badge anywhere in the frontend.

## Existing Constraints

1. **Lifecycle Invariants**: Requests in terminal closed states (`COMPLETED`, `CANCELLED`) must reject attribute updates and state mutations.
2. **Persistence Schema Migrations**: All database schema changes must be idempotent, sequentially numbered, and implemented via DbUp migration scripts in `Cakra.Api/Migrations/Scripts`. The next sequential script is `0016_add_request_deadline.sql`.
3. **Date & Timezone Normalization**: Deadlines represent date-only targets. They must be stored in UTC (`DATETIME2 NULL`) normalized to midnight UTC, avoiding timezone distortion when saved and displayed.
4. **Audit Trail Integrity**: State and attribute changes must preserve chronological audit history. When a deadline is mutated, an audit entry must record the actor, timestamp, and a human-readable note without corrupting lifecycle status.
5. **No Negative Validation on Target Date**: Per user decision, target deadlines are informational and have no restriction preventing past dates.

---

# 3. Gap Analysis

| ID | Severity | Gap |
|---|---|---|
| GAP-001 | CRITICAL | Database table `[request].[Requests]` lacks column `[Deadline] DATETIME2 NULL`. No migration script exists. |
| GAP-002 | CRITICAL | `Request` aggregate root has no `Deadline` property, cannot accept deadline in `Record(...)`, and cannot update or clear deadline in `UpdateCoreAttributes(...)`. |
| GAP-003 | CRITICAL | `IRequestRepository` and `RequestRepository.cs` do not persist, hydrate, or update the `Deadline` column. |
| GAP-004 | MAJOR | `RecordRequestCommand`, `UpdateRequestCoreAttributesCommand`, `RequestRecorded`, `RequestCoreAttributesUpdated`, and `RequestDto` lack `Deadline`. |
| GAP-005 | MAJOR | `RequestsController.cs` request payload models and endpoints do not accept or return `Deadline`. |
| GAP-006 | MAJOR | `CreateRequestModal.vue` and `CreateRequestView.vue` have no deadline date input in their form schemas. |
| GAP-007 | MAJOR | `RequestDetailView.vue` has no deadline display in the info card, no deadline date picker in the Edit modal, and no overdue indicator. |
| GAP-008 | MINOR | When a deadline changes, no automated `RequestAssignment` audit trail entry is generated to explain the change in the timeline. |

---

# 4. Open Questions

| ID | Question | Impact | Status |
|---|---|---|---|
| OQ-001 | What is the format, granularity, and validation of the deadline? | Domain modeling, DB data type, and validation rules. | CLOSED |
| OQ-002 | When and under which lifecycle states can the deadline be set or edited? | Aggregate invariant gating and API commands. | CLOSED |
| OQ-003 | Can an existing deadline be cleared back to null? | Domain logic, API contract, and UI form reset. | CLOSED |
| OQ-004 | How should overdue deadlines be visually presented in the UI? | Frontend styling, badge logic, and component templates. | CLOSED |
| OQ-005 | How should deadline changes be recorded in the audit history / timeline? | Domain audit logging on `RequestAssignment`. | CLOSED |
| OQ-006 | Does backend API require specialized filtering/sorting by deadline? | Backend query complexity vs frontend data manipulation. | CLOSED |

---

# 5. Assumptions

| ID | Assumption |
|---|---|
| ASM-001 | The database migration script is sequentially numbered as `0016_add_request_deadline.sql` and uses idempotent `IF NOT EXISTS` guards. |
| ASM-002 | The deadline date is stored normalized to UTC (`00:00:00Z`) so date-only semantics are preserved across all client timezones. |
| ASM-003 | Overdue status is evaluated purely on the client: a request is overdue if `Deadline != null`, `Deadline < Today` (in local date), and `Status` is neither `COMPLETED` nor `CANCELLED`. |
| ASM-004 | If `UpdateCoreAttributes` is invoked without altering the deadline, no redundant `RequestAssignment` audit trail item is appended. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| RISK-001 | Timezone skew causes a deadline to display as the previous or next day on client browsers. | User confusion over actual target date. | Transmit ISO-8601 string or date part (`YYYY-MM-DD`) and parse as UTC date-only components. |
| RISK-002 | Modifying core attributes without changing deadline generates spam in the assignment audit trail. | Cluttered audit timeline. | In `UpdateCoreAttributes(...)`, check `if (newDeadline != Deadline)` before creating and appending the `RequestAssignment` audit record. |
| RISK-003 | Closed requests get edited or have deadlines added posthumously. | Violates historical immutability. | Re-enforce existing `Status.IsClosed()` guard in `UpdateCoreAttributes(...)`, throwing `InvalidRequestStateTransitionException`. |

---

# 7. Recommendations

## Option A: Integrated Core Attributes & Assignment Audit Trail (Recommended)

1. **Database & Persistence**:
   - Add migration `0016_add_request_deadline.sql` with `ALTER TABLE [request].[Requests] ADD [Deadline] DATETIME2 NULL;`.
   - Update `RequestRepository.cs` `SELECT`, `INSERT`, and `UPDATE` statements to include `[Deadline]`.
2. **Domain & Events**:
   - Add `public DateTime? Deadline { get; private set; }` to `Request.cs`.
   - Update `Record(...)` to accept `DateTime? deadline = null`.
   - Update `UpdateCoreAttributes(...)` to accept `DateTime? deadline = null`. If `deadline != Deadline`, append a `RequestAssignment` with notes describing the transition:
     - Old null, new date -> `$"Deadline set to {newDate:yyyy-MM-dd}"`
     - Old date, new date -> `$"Deadline changed from {oldDate:yyyy-MM-dd} to {newDate:yyyy-MM-dd}"`
     - Old date, new null -> `"Deadline cleared"`
   - Include `DateTime? Deadline` in `RequestRecorded` and `RequestCoreAttributesUpdated`.
3. **API & Contracts**:
   - Update `RecordRequestCommand` and `UpdateRequestCoreAttributesCommand` to accept optional `DateTime? Deadline`.
   - Expose `DateTime? Deadline` on `RequestDto`.
4. **Frontend UI**:
   - Add HTML `<input type="date">` in `CreateRequestModal.vue` and `CreateRequestView.vue`.
   - In `RequestDetailView.vue`, render Deadline in the Request Information card. If overdue, display a red `Overdue` badge.
   - Include date input in the Edit Request Details modal with a clear button to remove the deadline.

### Advantages
- Seamlessly aligns with existing architecture and commands.
- No new specialized API endpoints needed.
- Provides immediate transparency in the audit history timeline without modifying other aggregates.
- Directly satisfies all agreed requirements from the intake interview.

### Disadvantages
- Adds a column and minor parameter updates across the Request command stack.

## Option B: Separate Dedicated Endpoint for Deadline Only

- Create `PUT /api/v1/requests/{id}/deadline` and separate domain method `SetDeadline(...)`.

### Advantages
- Isolates deadline mutations from core attribute editing.

### Disadvantages
- Forces users to interact with multiple separate modals or buttons to edit request details.
- More complex frontend UX. Rejected during collaborative interview.

---

# 8. Gap Closure

## GAP-001 (Database Migration)
### Decision
Create `0016_add_request_deadline.sql` adding nullable column `[Deadline] DATETIME2 NULL` to `[request].[Requests]`.
### Rationale
Provides reliable, non-breaking schema persistence for target deadlines while supporting existing historical requests.
### Impact
`Cakra.Api/Migrations/Scripts/0016_add_request_deadline.sql`.
### Architecture Impact
Persistence schema update in `[request]` schema.
### Resolved By
User & Analyst
### Resolved Date
2026-10-06

---

## GAP-002 & GAP-008 (Domain Aggregate & Audit Trail Logging)
### Decision
Add `Deadline` (`DateTime?`) property to `Request`. Update `Record(...)` to initialize `Deadline`. Update `UpdateCoreAttributes(...)` to accept `DateTime? deadline`. When `deadline != Deadline`, append an audit `RequestAssignment` with status preserved and formatted note (`Deadline set to ...`, `Deadline changed from ... to ...`, or `Deadline cleared`).
### Rationale
Maintains domain aggregate encapsulation and ensures full audit trail transparency on the state history timeline.
### Impact
`Cakra.Modules.Request/Domain/Request.cs`.
### Architecture Impact
Aggregate root property, invariants, and audit logging mechanics.
### Resolved By
User & Analyst
### Resolved Date
2026-10-06

---

## GAP-003, GAP-004 & GAP-005 (Repository, Commands, and REST API)
### Decision
Include `Deadline` in `RequestRepository.cs` queries and hydration. Add `DateTime? Deadline` to `RecordRequestCommand`, `UpdateRequestCoreAttributesCommand`, `RequestRecorded`, `RequestCoreAttributesUpdated`, `RequestDto`, and `RequestsController.cs` endpoints.
### Rationale
Ensures end-to-end data transmission across persistence, application services, and REST API layers.
### Impact
`Cakra.Modules.Request` (`Persistence/RequestRepository.cs`, `Services/RequestCommands.cs`, `Models/RequestDto.cs`), `Cakra.Api/Controllers/RequestsController.cs`.
### Architecture Impact
API contract and query read model expansion.
### Resolved By
User & Analyst
### Resolved Date
2026-10-06

---

## GAP-006 & GAP-007 (Frontend Creation, Detail, and Overdue Badge)
### Decision
Update `CreateRequestModal.vue` and `CreateRequestView.vue` with optional date input `form.deadline`. In `RequestDetailView.vue`, display formatted deadline in the info card, display a red `Overdue` badge when open and past deadline, and add an editable date input (with clear button) in the Edit Request modal.
### Rationale
Delivers an intuitive, user-friendly UI matching the agreed specifications.
### Impact
`src/frontend/Cakra.Web/src/components/CreateRequestModal.vue`, `src/frontend/Cakra.Web/src/views/CreateRequestView.vue`, `src/frontend/Cakra.Web/src/views/RequestDetailView.vue`, `src/frontend/Cakra.Web/src/api/requests.ts`.
### Architecture Impact
Screen component enhancements for `SCR-REQ-002` and `SCR-REQ-003`.
### Resolved By
User & Analyst
### Resolved Date
2026-10-06

---

## OQ-001 through OQ-006 (Closed Decisions from Intake Interview)
### Decision
All open questions are closed as agreed:
- OQ-001: Date-only UTC, purely informational, no past-date restriction.
- OQ-002: Optional on creation; mutable during active states (`CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`); locked on closed states (`COMPLETED`, `CANCELLED`).
- OQ-003: Can be cleared back to null at any time during active states.
- OQ-004: Prominent red "Overdue" badge when `Deadline < Today` for open requests.
- OQ-005: Automated `RequestAssignment` audit entry logged on deadline change with descriptive note.
- OQ-006: Return `Deadline` in DTO; handle filtering/sorting on client without extra backend query parameters.
### Rationale
Directly aligned with user specifications from `/grill-me`.
### Impact
Fully clarifies all functional requirements and implementation constraints.
### Architecture Impact
Provides unambiguous baseline for target architecture.
### Resolved By
User & Analyst
### Resolved Date
2026-10-06

---

# 9. Architecture Applicability

## Decision
ARCHITECTURE-REQUIRED

## Rationale
Architecture definition is required because this change introduces:
1. Database schema migration (`0016_add_request_deadline.sql`) in `[request].[Requests]`.
2. Core domain aggregate state modifications and automated audit entry generation on `RequestAssignment`.
3. Extended REST API contracts on `POST /api/v1/requests` and `PUT /api/v1/requests/{id}/core-attributes`.
4. Cross-layer UI enhancements for request creation, edit modal, state history timeline, and overdue badge calculation.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved
- [x] All required decisions recorded
- [x] All blocking open questions resolved
- [x] Architecture can be finalized or updated (Gate granted by Architect)

## Status

READY-FOR-PLANNING

## Notes

All feasibility analysis and gap closure decisions are completed and approved. The Architect has evaluated the artifact and granted the READY-FOR-PLANNING gate. Target architecture definition may proceed.

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-020-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-020-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- UI LAYOUT: [15-scr-req-003.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/15-scr-req-003.md)

Referenced codebase locations:

- [0006_request_tables.sql](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Migrations/Scripts/0006_request_tables.sql)
- [Request.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs)
- [RequestAssignment.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/RequestAssignment.cs)
- [RequestRepository.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Persistence/RequestRepository.cs)
- [RequestsController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/RequestsController.cs)
- [CreateRequestModal.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/CreateRequestModal.vue)
- [CreateRequestView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/CreateRequestView.vue)
- [RequestDetailView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue)
- [requests.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/requests.ts)
