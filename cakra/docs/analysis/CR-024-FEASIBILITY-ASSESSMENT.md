---
Title: Feasibility Assessment for Updating Customer and Product in Work Package Aggregate and Screen (CR-024)
Code: CR-024
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-07
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Change being assessed: Implementation of the capability to update, reassign, or clear (`null`) the Customer and Product associations for an existing Work Package aggregate root (`WorkPackage.cs`) and screen (`SCR-WP-001` / `WorkPackageView.vue`), including lifecycle mutability rules, cross-module active entity validation, unified domain event publication, REST API endpoint, and detail drawer UI controls, as formally captured in [CR-024-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-024-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-024-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-024-ISSUE.md)
- DOMAIN: [work-package-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/work-package-domain.md)
- ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- UI SPECIFICATION: [Work Package Screen (`SCR-WP-001`)](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue)

## Objective

Assess the feasibility, domain boundaries, schema impact, API contracts, UI/UX interaction patterns, and planning readiness to:

1. Add a domain method `UpdateContext(Guid? customerId, Guid? productId, DateTime? updatedAtUtc = null)` to the `WorkPackage` aggregate root.
2. Enforce lifecycle invariants locking context modifications when a Work Package is in `CLOSED` state.
3. Emit a unified domain event `WorkPackageContextChanged` when Customer or Product associations change.
4. Support independent clearing (setting to `null` / unassigned) of Customer, Product, or both.
5. Preserve aggregate boundaries such that changing Work Package context never cascades to or alters constituent Requests.
6. Provide cross-module validation ensuring referenced non-null Customer and Product IDs exist and are active.
7. Expose a unified REST API endpoint `PUT /api/v1/work-packages/{id}/context` in `WorkPackagesController.cs`.
8. Provide interactive Customer and Product selection controls in the Detail Drawer of `SCR-WP-001` (`WorkPackageView.vue`) with save and clear capabilities.

---

# 2. Current State

## Existing Behavior

1. **Domain Model (`Cakra.Modules.WorkPackage`, `WorkPackage.cs`)**:
   - `WorkPackage` aggregate root declares `public Guid? CustomerId { get; private set; }` and `public Guid? ProductId { get; private set; }`.
   - `CustomerId` and `ProductId` are initialized during initial creation via `WorkPackage.Create(...)` and rehydrated via `WorkPackage.Rehydrate(...)`.
   - There is currently no domain method on `WorkPackage` to mutate or clear `CustomerId` or `ProductId`.
   - Aggregate mutations exist for `UpdateObjective(...)`, `UpdateDeadline(...)`, `AssignOwner(...)`, `Activate(...)`, `Close(...)`, `AddRequest(...)`, `RemoveRequest(...)`, and `ReorderRequests(...)`.
2. **Persistence Layer (`Cakra.Modules.WorkPackage.Persistence`)**:
   - The database table `[workpackage].[WorkPackages]` already possesses nullable columns `[CustomerId] UNIQUEIDENTIFIER NULL` and `[ProductId] UNIQUEIDENTIFIER NULL`.
   - `WorkPackageRepository.cs` already projects `[CustomerId]` and `[ProductId]` in `SELECT` queries, inserts them in `AddAsync(...)`, and already includes `[CustomerId] = @CustomerId` and `[ProductId] = @ProductId` in the parameterized `UPDATE` statement in `UpdateAsync(...)`.
   - No SQL schema migration is required.
3. **Application Services & API Layer (`Cakra.Modules.WorkPackage.Services`, `Cakra.Api`)**:
   - `WorkPackageService.cs` validates `CustomerId` and `ProductId` on creation via `_customerQueryService.IsCustomerActiveAsync` and `_productQueryService.IsProductActiveAsync`.
   - No MediatR command, handler, or service method exists to update Customer or Product on an existing Work Package.
   - `WorkPackagesController.cs` exposes endpoints for objective, deadline, owner, activate, close, and request memberships, but has no endpoint for context/association updates.
4. **Frontend UI (`Cakra.Web`, `WorkPackageView.vue`)**:
   - The Create Work Package modal allows selecting Customer and Product.
   - In the Detail Drawer, Customer and Product are rendered strictly as read-only static text labels (`resolveCustomerDisplay(selectedWorkPackage)` and `resolveProductDisplay(selectedWorkPackage)`).
   - No dropdown controls or save buttons exist in the Detail Drawer for Customer and Product.

## Existing Constraints

1. **Strict DDD Aggregate Boundaries**: Per Domain Rules 11 and 12, a Work Package is a temporary grouping container and does not own Request lifecycles or Request attributes. Changing Work Package context must not alter constituent Requests.
2. **Lifecycle Invariants**: Work Packages in terminal state (`CLOSED`) must reject modifications to Customer and Product associations.
3. **Cross-Schema Isolation**: modul `WorkPackage` references `Customer` and `Product` by raw GUID identifiers without physical foreign keys or direct cross-schema database writes, validating active status through query services (`ICustomerQueryService`, `IProductQueryService`).
4. **Zero Migration Requirement**: Because `[CustomerId]` and `[ProductId]` already exist in the schema and repository, changes are confined to domain logic, application service, API controller, and UI.

---

# 3. Gap Analysis

| ID | Severity | Gap |
|---|---|---|
| GAP-001 | CRITICAL | Aggregate root `WorkPackage.cs` lacks domain method to update Customer and Product context and enforce lifecycle invariants. |
| GAP-002 | MAJOR | Domain event `WorkPackageContextChanged` is not defined or emitted when Customer or Product changes. |
| GAP-003 | MAJOR | `IWorkPackageService` and `WorkPackageService.cs` lack application command, handler, and cross-module active validation for updating context. |
| GAP-004 | MAJOR | `WorkPackagesController.cs` lacks unified endpoint `PUT /api/v1/work-packages/{id}/context` (accepting nullable customerId and productId). |
| GAP-005 | MAJOR | Frontend API helper `workpackages.ts` lacks `updateWorkPackageContext` function and payload interface. |
| GAP-006 | MAJOR | Detail Drawer in `WorkPackageView.vue` lacks interactive Customer and Product selection controls and Save button. |

---

# 4. Open Questions

| ID | Question | Impact | Status |
|---|---|---|---|
| OQ-001 | What happens to existing constituent Requests when a Work Package's Customer or Product changes? | Domain boundary and transactional scope: cascading vs independent. | CLOSED |
| OQ-002 | In which Work Package lifecycle states should updating Customer or Product be permitted? | Invariant enforcement in domain aggregate and API. | CLOSED |
| OQ-003 | Should users be allowed to clear (set to null / unassigned) Customer or Product independently? | Business optionality and entity nullability. | CLOSED |
| OQ-004 | How should the backend REST API endpoints and commands be structured? | REST API surface and MediatR command granularity. | CLOSED |
| OQ-005 | Should domain events be emitted when Customer or Product changes? | Audit trail, event dispatching, and feed projections. | CLOSED |
| OQ-006 | How should the editing controls be presented in the user interface (`WorkPackageView.vue`)? | UI component layout, user ergonomics, and drawer structure. | CLOSED |

---

# 5. Assumptions

| ID | Assumption |
|---|---|
| ASM-001 | No database schema migration is required because `[workpackage].[WorkPackages]` already contains nullable `[CustomerId]` and `[ProductId]` columns, and `WorkPackageRepository` already supports updating them. |
| ASM-002 | If a non-null `CustomerId` is provided, it must reference an active Customer validated via `ICustomerQueryService.IsCustomerActiveAsync`. |
| ASM-003 | If a non-null `ProductId` is provided, it must reference an active Product validated via `IProductQueryService.IsProductActiveAsync`. |
| ASM-004 | If `customerId` or `productId` is null or `Guid.Empty` in the update payload, it is treated as unassigning/clearing the respective association. |
| ASM-005 | Constituent Requests remain completely unaltered when Work Package context changes. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| RISK-001 | Attempting to update Customer or Product on a closed Work Package. | Inconsistent historical record. | Enforce guard in `WorkPackage.UpdateContext`: throw `WorkPackageDomainException` if `Status == WorkPackageStatus.Closed`. |
| RISK-002 | Associating an inactive or non-existent Customer or Product. | Referential integrity failure. | Validate non-null IDs through `ICustomerQueryService` and `IProductQueryService` prior to domain aggregate mutation. |
| RISK-003 | User confusion expecting constituent Requests to automatically change Customer/Product. | Operational mismatch. | Clear domain rules and documentation that Work Package association is grouping context only, leaving Request attributes untouched. |

---

# 7. Recommendations

## Recommended Approach: Native Aggregate Method with Unified Context Endpoint

1. **Domain Aggregate (`WorkPackage.cs`)**:
   - Add domain method:
     ```csharp
     public void UpdateContext(Guid? customerId, Guid? productId, DateTime? updatedAtUtc = null)
     ```
   - Invariant: throw `WorkPackageDomainException` if `Status == WorkPackageStatus.Closed`.
   - If either value actually changes, update `CustomerId`, `ProductId`, `UpdatedAt = updatedAtUtc ?? DateTime.UtcNow`, and raise `WorkPackageContextChanged`.
2. **Domain Event (`WorkPackageContextChanged.cs`)**:
   - Define record:
     ```csharp
     public sealed record WorkPackageContextChanged(
         Guid WorkPackageId,
         Guid? PreviousCustomerId,
         Guid? NewCustomerId,
         Guid? PreviousProductId,
         Guid? NewProductId,
         DateTime OccurredAtUtc) : IDomainEvent;
     ```
3. **Application Service (`IWorkPackageService.cs`, `WorkPackageService.cs`, `WorkPackageCommands.cs`)**:
   - Define `UpdateWorkPackageContextCommand(Guid WorkPackageId, Guid? CustomerId, Guid? ProductId) : IRequest<WorkPackageDto>`.
   - Implement `UpdateContextAsync(Guid workPackageId, Guid? customerId, Guid? productId, CancellationToken cancellationToken = default)`.
   - Validate non-null `CustomerId` via `_customerQueryService.IsCustomerActiveAsync` (throw `InvalidOperationException` or `KeyNotFoundException`).
   - Validate non-null `ProductId` via `_productQueryService.IsProductActiveAsync` (throw `InvalidOperationException` or `KeyNotFoundException`).
   - Persist aggregate via `_workPackageRepository.UpdateAsync(workPackage, cancellationToken)` and dispatch domain events.
4. **API Controller (`WorkPackagesController.cs`)**:
   - Add `PUT /api/v1/work-packages/{id}/context` accepting `UpdateWorkPackageContextBody` (`Guid? CustomerId`, `Guid? ProductId`).
   - Return updated `WorkPackageDto` with status `200 OK`.
5. **Frontend API & UI (`workpackages.ts`, `WorkPackageView.vue`)**:
   - Add `updateWorkPackageContext(id, { customerId, productId })` to `workpackages.ts`.
   - In `WorkPackageView.vue`:
     - Add reactive state `contextForm` (`customerId`, `productId`).
     - Populate `contextForm` on `syncDetailForms`.
     - In Detail Drawer, add a dedicated "Customer & Product" section with:
       - Customer `<select>` with `<option value="">-- None / Unassigned --</option>` and active customers.
       - Product `<select>` with `<option value="">-- None / Unassigned --</option>` and active products.
       - "Save Context" button disabled when package is closed or when no changes are pending.
     - Upon successful save, update `selectedWorkPackage`, refresh item in table, and display success toast/message.

---

# 8. Gap Closure

## GAP-001 (Domain Aggregate Model & Invariants)
### Decision
Add `UpdateContext(Guid? customerId, Guid? productId, DateTime? updatedAtUtc = null)` to `WorkPackage.cs`, rejecting updates when status is `CLOSED`.
### Rationale
Encapsulates domain logic and preserves aggregate invariant integrity.
### Impact
`Cakra.Modules.WorkPackage/Domain/WorkPackage.cs`.
### Architecture Impact
Aggregate root mutation method.
### Resolved By
User & Analyst
### Resolved Date
2026-10-07

---

## GAP-002 (Domain Event Definition)
### Decision
Create `WorkPackageContextChanged` domain event capturing previous and new Customer and Product IDs with occurrence timestamp.
### Rationale
Ensures domain auditability and event stream traceability.
### Impact
`Cakra.Modules.WorkPackage/Domain/Events/WorkPackageContextChanged.cs`.
### Architecture Impact
Domain event catalogue in Work Package module.
### Resolved By
User & Analyst
### Resolved Date
2026-10-07

---

## GAP-003 (Application Service & Active Entity Validation)
### Decision
Add `UpdateWorkPackageContextCommand` and handler in `WorkPackageService.cs` validating non-null Customer and Product active status via `ICustomerQueryService` and `IProductQueryService`.
### Rationale
Maintains clean CQRS command pipeline and guarantees valid cross-module references.
### Impact
`Cakra.Modules.WorkPackage/Services/WorkPackageService.cs`, `WorkPackageCommands.cs`, `IWorkPackageService.cs`.
### Architecture Impact
Command handling and cross-module query validation.
### Resolved By
User & Analyst
### Resolved Date
2026-10-07

---

## GAP-004 (REST API Endpoint)
### Decision
Expose `PUT /api/v1/work-packages/{id}/context` in `WorkPackagesController.cs`.
### Rationale
Provides a cohesive, single-trip endpoint for context modifications.
### Impact
`Cakra.Api/Controllers/WorkPackagesController.cs`.
### Architecture Impact
REST API contract expansion.
### Resolved By
User & Analyst
### Resolved Date
2026-10-07

---

## GAP-005 & GAP-006 (Frontend Client & Detail Drawer UI)
### Decision
Add `updateWorkPackageContext` to `workpackages.ts` and add a dedicated "Customer & Product" section with dropdowns and a Save button in `WorkPackageView.vue`.
### Rationale
Provides intuitive user interaction aligned with Owner and Deadline controls on `SCR-WP-001`.
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
- OQ-001: No cascading update — constituent Requests remain untouched per Domain Rules 11 and 12.
- OQ-002: Allowed in `DRAFT` and `ACTIVE` states; strictly forbidden once `CLOSED`.
- OQ-003: Customer and Product can be cleared back to unassigned (`null`) independently.
- OQ-004: Unified endpoint `PUT /api/v1/work-packages/{id}/context`.
- OQ-005: Unified domain event `WorkPackageContextChanged`.
- OQ-006: Dedicated section in Detail Drawer with dropdowns and Save button.
### Rationale
Directly aligned with user requirements from `/grill-me`.
### Impact
Clear, unambiguous boundary for technical realization.
### Architecture Impact
Solid foundation for target architecture definition.
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
1. Aggregate root domain mutation and state invariant enforcement in `WorkPackage.cs`.
2. New domain event `WorkPackageContextChanged`.
3. New REST API contract `PUT /api/v1/work-packages/{id}/context` and MediatR command.
4. Cross-module active entity validation in application service.
5. Frontend UI/UX enhancements on screen `SCR-WP-001`.

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

- ISSUE: [CR-024-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-024-ISSUE.md)
- DOMAIN: [work-package-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/work-package-domain.md)
- ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- UI SPECIFICATION: [Work Package Screen (`SCR-WP-001`)](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue)

Referenced codebase locations:

- [WorkPackage.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackage.cs)
- [WorkPackageRepository.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Persistence/WorkPackageRepository.cs)
- [WorkPackageService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageService.cs)
- [WorkPackageCommands.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageCommands.cs)
- [IWorkPackageService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Services/IWorkPackageService.cs)
- [WorkPackagesController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/WorkPackagesController.cs)
- [WorkPackageView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue)
- [workpackages.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/workpackages.ts)
