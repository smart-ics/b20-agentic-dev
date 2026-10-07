---
Title: Feasibility Assessment for Target Deadline Date in Work Package Aggregate and Screen (CR-023)
Code: CR-023
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-07
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Change being assessed: Implementation of an optional target deadline attribute for the Work Package aggregate root (`WorkPackage.cs`) and screen (`SCR-WP-001` / `WorkPackageView.vue`), including aggregate lifecycle rules, database schema migration, query and command services, dedicated update endpoint, and overdue visualization, as formally captured in [CR-023-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-023-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-023-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-023-ISSUE.md)
- ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- UI SPECIFICATION: [Work Package Screen (`SCR-WP-001`)](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue)

## Objective

Assess the feasibility, domain boundaries, schema changes, API contracts, UI/UX interaction patterns, and planning readiness to:

1. Add an optional `Deadline` property (`DateTime?`, UTC midnight) to the `WorkPackage` aggregate root and database schema `[workpackage].[WorkPackages]`.
2. Allow setting the deadline optionally during initial Work Package creation (`WorkPackage.Create(...)`, `CreateWorkPackageCommand`, `POST /api/v1/work-packages`).
3. Allow updating or clearing (`null`) the deadline on active Work Packages via a dedicated endpoint `PUT /api/v1/work-packages/{id}/deadline` and aggregate method `WorkPackage.UpdateDeadline(...)`.
4. Enforce lifecycle invariants locking deadline changes once a Work Package is closed (`CLOSED`).
5. Update `UpdatedAt` timestamp upon deadline change without emitting separate domain events (per confirmed alignment).
6. Expose `Deadline` in `WorkPackageDto` and query projections.
7. Add date input controls in the Create Work Package modal and the Work Package Detail Panel on `SCR-WP-001` (`WorkPackageView.vue`).
8. Add a "Deadline" column to the Work Package table on `SCR-WP-001` and display a prominent "Overdue" badge when `Deadline < Today` for open (`DRAFT` or `ACTIVE`) packages, clearing the indicator once `CLOSED`.

---

# 2. Current State

## Existing Behavior

1. **Domain Model (`Cakra.Modules.WorkPackage`, `WorkPackage.cs`)**:
   - `WorkPackage` aggregate root contains `Name`, `Objective`, `Status` (`DRAFT`, `ACTIVE`, `CLOSED`), `OwnerPersonId`, `CustomerId`, `ProductId`, `ClosedReason`, `ClosedAt`, `CreatedAt`, `UpdatedAt`, and constituent `Requests`.
   - `WorkPackage` has no `Deadline` attribute.
   - `Create(...)` accepts `name`, `objective`, `ownerPersonId`, `customerId`, and `productId`, but has no parameter for `deadline`.
   - Aggregate mutations include `Activate(...)`, `Close(...)`, `UpdateObjective(...)`, `AssignOwner(...)`, `AddRequest(...)`, `RemoveRequest(...)`, and `ReorderRequests(...)`. No deadline mutation exists.

2. **Persistence Layer (`Cakra.Modules.WorkPackage.Persistence`)**:
   - `0007_workpackage_tables.sql` created `[workpackage].[WorkPackages]` without a `Deadline` column.
   - `WorkPackageRepository.cs` maps columns in SQL statements (`SELECT`, `INSERT INTO [workpackage].[WorkPackages]`, `UPDATE [workpackage].[WorkPackages]`). None of these reference `Deadline`.
   - The private helper class `WorkPackageRow` does not include `Deadline`.

3. **API & Service Layer (`Cakra.Api`, `Cakra.Modules.WorkPackage.Services`)**:
   - `CreateWorkPackageCommand` does not accept a deadline parameter.
   - `WorkPackageDto` does not expose a deadline field.
   - `WorkPackageQueryService.cs` maps `SELECT` queries across `GetWorkPackageByIdAsync`, `ListWorkPackagesAsync`, and `GetRequestWorkPackageAsync`, which do not project `Deadline`.
   - `WorkPackagesController.cs` has `POST /api/v1/work-packages` and `PUT /api/v1/work-packages/{id}/objective`, but no `PUT /api/v1/work-packages/{id}/deadline`.

4. **Frontend Applications (`Cakra.Web`, `WorkPackageView.vue`)**:
   - `WorkPackageItem` interface in `WorkPackageView.vue` and `workpackages.ts` lacks `deadline`.
   - Create Work Package modal only accepts `name`, `objective`, `ownerPersonId`, `customerId`, and `productId`.
   - The Work Package table renders columns: `ID`, `Objective`, `Owner`, `Customer`, `Product`, and `Status`. There is no `Deadline` column.
   - The Work Package Detail Panel renders Objective, Owner, Created, Customer, Product, and Closed Reason, but no deadline display or edit form.
   - There is no overdue calculation or badge for Work Packages.

## Existing Constraints

1. **Lifecycle Invariants**: Work Packages in terminal closed state (`CLOSED`) must reject attribute updates, including deadline changes.
2. **Persistence Schema Migrations**: All database schema changes must be idempotent, sequentially numbered, and implemented via DbUp migration scripts in `Cakra.Api/Migrations/Scripts`. The next sequential script is `0017_add_workpackage_deadline.sql`.
3. **Date & Timezone Normalization**: Deadlines represent date-only targets. They must be stored in UTC (`DATETIME2 NULL`) normalized to midnight UTC (`00:00:00Z`), avoiding timezone drift between frontend clients and the database.
4. **Informational Target Date**: Per confirmed requirement, target deadlines are informational; no negative validation rule prevents selecting past dates upon entry.

---

# 3. Gap Analysis

| ID | Severity | Gap |
|---|---|---|
| GAP-001 | CRITICAL | Database table `[workpackage].[WorkPackages]` lacks column `[Deadline] DATETIME2 NULL`. Next migration script `0017_add_workpackage_deadline.sql` does not exist. |
| GAP-002 | CRITICAL | `WorkPackage` aggregate root has no `Deadline` property, cannot accept deadline in `Create(...)`, and cannot update or clear deadline via domain method. |
| GAP-003 | CRITICAL | `WorkPackageRepository.cs` and `WorkPackageQueryService.cs` do not persist, hydrate, or project the `Deadline` column. |
| GAP-004 | MAJOR | `CreateWorkPackageCommand`, `WorkPackageDto`, and service layer lack `Deadline`. |
| GAP-005 | MAJOR | No dedicated endpoint `PUT /api/v1/work-packages/{id}/deadline` exists on `WorkPackagesController.cs`. |
| GAP-006 | MAJOR | Create Work Package modal in `WorkPackageView.vue` does not have a deadline date picker. |
| GAP-007 | MAJOR | Work Package list table on `SCR-WP-001` lacks a "Deadline" column. |
| GAP-008 | MAJOR | Detail Panel on `SCR-WP-001` does not display or allow updating/clearing the Work Package deadline. |
| GAP-009 | MAJOR | No overdue evaluation logic or "Overdue" badge exists in `WorkPackageView.vue`. |

---

# 4. Open Questions

| ID | Question | Impact | Status |
|---|---|---|---|
| OQ-001 | What entity owns the Deadline in SCR-WP-001? | Scope of change: Work Package container vs Request items. | CLOSED |
| OQ-002 | What are the lifecycle and mutability rules for the Work Package Deadline? | Aggregate invariant gating and validation logic. | CLOSED |
| OQ-003 | What is the format, granularity, and entry validation for the deadline? | Domain normalization and database data type. | CLOSED |
| OQ-004 | Should overdue deadlines have a visual indicator in SCR-WP-001? | UX styling, badge presentation, and conditional rendering. | CLOSED |
| OQ-005 | How should the backend API expose updating and clearing the deadline? | REST API contract, MediatR command, and controller routing. | CLOSED |
| OQ-006 | Should updating the deadline emit a domain event? | Event dispatcher and domain event records. | CLOSED |

---

# 5. Assumptions

| ID | Assumption |
|---|---|
| ASM-001 | The database migration script is sequentially numbered as `0017_add_workpackage_deadline.sql` and uses idempotent `IF NOT EXISTS` / `COL_LENGTH` guards. |
| ASM-002 | The deadline date is stored normalized to UTC midnight (`00:00:00Z`), preserving date-only semantics across all client timezones. |
| ASM-003 | Overdue status is evaluated client-side: a Work Package is overdue if `Deadline != null`, `Deadline < Today` (in local date), and `Status` is not `CLOSED`. |
| ASM-004 | Updating the Work Package deadline modifies `UpdatedAt = UtcNow`, but does not emit a domain event, as aligned in the intake interview. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| RISK-001 | Timezone skew causes a deadline to display as the previous or next day on client browsers. | User confusion over actual target date. | Transmit date part (`YYYY-MM-DD`) or ISO-8601 string and parse/normalize to UTC midnight on both client and server. |
| RISK-002 | A closed Work Package has its deadline modified via direct API call. | Violates historical immutability. | In `WorkPackage.UpdateDeadline(...)`, enforce guard `if (Status == WorkPackageStatus.Closed) throw new WorkPackageDomainException(...)`. |
| RISK-003 | Dense table layout on `SCR-WP-001` experiences column crowding with the addition of the Deadline column. | UI wrapping or clipping on smaller screens. | Ensure concise date formatting (`YYYY-MM-DD` or `MMM D, YYYY`) and place the Overdue badge inline or compactly within the cell. |

---

# 7. Recommendations

## Recommended Approach: Native Work Package Attribute with Dedicated Update Endpoint

1. **Database & Migration**:
   - Create `0017_add_workpackage_deadline.sql` adding `[Deadline] DATETIME2 NULL` to `[workpackage].[WorkPackages]`, plus a filtered nonclustered index on `([Deadline]) WHERE [Deadline] IS NOT NULL`.
2. **Domain Aggregate (`WorkPackage.cs`)**:
   - Add `public DateTime? Deadline { get; private set; }`.
   - Add private helper `NormalizeDeadline(DateTime? deadline)` normalizing non-null dates to UTC midnight.
   - Update `Create(...)` and `Rehydrate(...)` to accept optional `DateTime? deadline = null`.
   - Add domain method `UpdateDeadline(DateTime? deadline, DateTime? updatedAtUtc = null)` validating that `Status != WorkPackageStatus.Closed` and updating `UpdatedAt`.
3. **Persistence & Queries**:
   - Update `WorkPackageRepository.cs` queries (`GetByIdAsync`, `GetAllAsync`, `AddAsync`, `UpdateAsync`) and `WorkPackageRow` to include `[Deadline]`.
   - Update `WorkPackageQueryService.cs` queries (`GetWorkPackageByIdAsync`, `ListWorkPackagesAsync`, `GetRequestWorkPackageAsync`) and `WorkPackageQueryRow` to project `[Deadline]`.
4. **Application Services & API**:
   - Update `CreateWorkPackageCommand` to accept optional `DateTime? Deadline`.
   - Add `UpdateWorkPackageDeadlineCommand(Guid WorkPackageId, DateTime? Deadline)` and validator.
   - Expose `DateTime? Deadline` in `WorkPackageDto`.
   - Add `PUT /api/v1/work-packages/{id}/deadline` in `WorkPackagesController.cs`.
5. **Frontend UI (`SCR-WP-001`)**:
   - Update `workpackages.ts` with `deadline` in `WorkPackageDto`, `CreateWorkPackagePayload`, and add `updateWorkPackageDeadline(id, { deadline })`.
   - Update `WorkPackageView.vue`:
     - Modal: Add optional date input for deadline.
     - Table: Add "Deadline" column with formatted date and red "Overdue" badge if `Deadline < Today` and not `CLOSED`.
     - Detail Panel: Render target deadline in metadata section; provide edit/clear controls when in `DRAFT` or `ACTIVE`.

---

# 8. Gap Closure

## GAP-001 (Database Migration)
### Decision
Create `0017_add_workpackage_deadline.sql` adding nullable column `[Deadline] DATETIME2 NULL` and filtered index `[IX_WorkPackages_Deadline]` on `[workpackage].[WorkPackages]`.
### Rationale
Provides reliable, non-breaking schema persistence for Work Package target completion dates.
### Impact
`Cakra.Api/Migrations/Scripts/0017_add_workpackage_deadline.sql`.
### Architecture Impact
Persistence schema update in `[workpackage]` schema.
### Resolved By
User & Analyst
### Resolved Date
2026-10-07

---

## GAP-002 (Domain Aggregate Model & Invariants)
### Decision
Add `Deadline` (`DateTime?`, normalized to UTC midnight) property to `WorkPackage`. Update `Create(...)` and `Rehydrate(...)`. Add `UpdateDeadline(DateTime? deadline, DateTime? updatedAtUtc = null)` enforcing that closed Work Packages cannot be modified.
### Rationale
Enforces domain encapsulation, timezone normalization, and lifecycle invariants.
### Impact
`Cakra.Modules.WorkPackage/Domain/WorkPackage.cs`.
### Architecture Impact
Aggregate root properties and methods.
### Resolved By
User & Analyst
### Resolved Date
2026-10-07

---

## GAP-003 & GAP-004 (Repository, Queries, DTO, and Commands)
### Decision
Include `Deadline` in `WorkPackageRepository.cs`, `WorkPackageQueryService.cs`, `WorkPackageDto`, `CreateWorkPackageCommand`, and create `UpdateWorkPackageDeadlineCommand`.
### Rationale
Ensures consistent round-trip data flow across data access, query caching, and command processing layers.
### Impact
`Cakra.Modules.WorkPackage` (`Persistence/WorkPackageRepository.cs`, `Services/WorkPackageQueryService.cs`, `Services/WorkPackageCommands.cs`, `Services/WorkPackageService.cs`, `Models/WorkPackageDto.cs`).
### Architecture Impact
Module contract and query read model expansion.
### Resolved By
User & Analyst
### Resolved Date
2026-10-07

---

## GAP-005 (REST API Controller Endpoint)
### Decision
Add `PUT /api/v1/work-packages/{id}/deadline` in `WorkPackagesController.cs` accepting `{ "deadline": "YYYY-MM-DD" | null }`.
### Rationale
Provides a clean, dedicated REST API endpoint to update or clear the deadline independently.
### Impact
`Cakra.Api/Controllers/WorkPackagesController.cs`.
### Architecture Impact
REST API surface for Work Package module.
### Resolved By
User & Analyst
### Resolved Date
2026-10-07

---

## GAP-006, GAP-007, GAP-008 & GAP-009 (Frontend UI on SCR-WP-001)
### Decision
Update `WorkPackageView.vue` and `workpackages.ts`:
1. Include deadline in the Create Work Package modal.
2. Add a "Deadline" table column displaying formatted date and red "Overdue" badge when `Deadline < Today` while open (`DRAFT` or `ACTIVE`).
3. Include target deadline in the Detail Panel with inline edit/clear controls when in active lifecycle states.
### Rationale
Directly delivers the operational oversight and visual urgency indicators requested by users.
### Impact
`src/frontend/Cakra.Web/src/views/WorkPackageView.vue`, `src/frontend/Cakra.Web/src/api/workpackages.ts`.
### Architecture Impact
Screen presentation and interaction enhancement for `SCR-WP-001`.
### Resolved By
User & Analyst
### Resolved Date
2026-10-07

---

## OQ-001 through OQ-006 (Intake Alignment Decisions)
### Decision
All open questions are closed as agreed:
- OQ-001: Belongs to the Work Package aggregate itself (`WorkPackages` table).
- OQ-002: Optional on creation; mutable during `DRAFT` and `ACTIVE`; immutable when `CLOSED`.
- OQ-003: Date-only UTC midnight, purely informational target date with no past-date entry restriction.
- OQ-004: Prominent "Overdue" badge displayed when `Deadline < Today` while in `DRAFT` or `ACTIVE`; hidden when `CLOSED`.
- OQ-005: Dedicated endpoint `PUT /api/v1/work-packages/{id}/deadline` accepting optional date or `null`.
- OQ-006: No domain event required; updating property and `UpdatedAt` timestamp is sufficient.
### Rationale
Directly aligned with user specifications from `/grill-me`.
### Impact
Fully clarifies all functional requirements and implementation boundaries.
### Architecture Impact
Provides unambiguous baseline for target architecture.
### Resolved By
User & Analyst
### Resolved Date
2026-10-07

---

# 9. Architecture Applicability

## Decision
ARCHITECTURE-REQUIRED

## Rationale
Architecture definition is required because this change introduces:
1. Database schema migration (`0017_add_workpackage_deadline.sql`) in `[workpackage].[WorkPackages]`.
2. Work Package domain aggregate state modifications and invariant enforcement.
3. New REST API contract on `PUT /api/v1/work-packages/{id}/deadline` and updated `POST /api/v1/work-packages`.
4. Cross-layer UI enhancements for creation, table presentation, detail editing, and overdue status evaluation.

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

All feasibility analysis and gap closure decisions are completed and approved. The Architect has evaluated the artifact, verified that all blocking gaps and open questions are closed, and granted the READY-FOR-PLANNING gate. Target architecture definition may proceed.

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-023-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-023-ISSUE.md)
- ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- UI SPECIFICATION: [Work Package Screen (`SCR-WP-001`)](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue)

Referenced codebase locations:

- [0007_workpackage_tables.sql](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Migrations/Scripts/0007_workpackage_tables.sql)
- [WorkPackage.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackage.cs)
- [WorkPackageDto.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Models/WorkPackageDto.cs)
- [WorkPackageRepository.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Persistence/WorkPackageRepository.cs)
- [WorkPackageCommands.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageCommands.cs)
- [WorkPackageService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageService.cs)
- [WorkPackageQueryService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageQueryService.cs)
- [WorkPackagesController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/WorkPackagesController.cs)
- [WorkPackageView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue)
- [workpackages.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/workpackages.ts)
