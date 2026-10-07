# ISSUE

## Metadata

ID: CR-024
Type: CHANGE-REQUEST
Status: OPEN
Title: Update Customer and Product Association for Work Package Aggregate and Screen (SCR-WP-001)

## Source

Reported By: User
Reported Date: 2026-10-07

## Description

Users managing Work Packages on `SCR-WP-001` can associate an optional Customer and Product when creating a new Work Package. However, once a Work Package has been created, there is currently no capability in the system to update, reassign, or clear the Customer and Product associations.

In operational practice, a Work Package's context may change or may have been created before the relevant Customer or Product was identified. Furthermore, an initiative may initially be classified under a Customer or Product and later pivoted to a cross-cutting or internal initiative (or vice versa), requiring the ability to modify or clear these associations on an existing Work Package.

## Desired Outcome

1. **Context Modification Capability**:
   - Authorized users can update the associated Customer, Product, or both on an existing Work Package.
   - Authorized users can clear (unassign / set to `null`) an existing Customer or Product association independently.
2. **Lifecycle & Mutability Rules**:
   - Updating or clearing Customer and Product is permitted while the Work Package is in `DRAFT` or `ACTIVE` lifecycle states.
   - Once a Work Package is `CLOSED`, its Customer and Product associations become strictly immutable.
3. **Domain Boundary & Aggregate Independence**:
   - Updating the Customer or Product on a Work Package does not cascade or alter the Customer or Product attributes of existing Requests grouped inside the Work Package. Constituent Requests retain their own independent attributes per Domain Rules 11 and 12.
4. **Backend API & Service**:
   - A unified context update endpoint is available (e.g. `PUT /api/v1/work-packages/{id}/context`) accepting optional/nullable `customerId` and `productId`.
   - Appropriate entity validation is enforced (e.g. ensuring any referenced non-null Customer or Product exists and is active via query services).
   - A unified domain event (e.g. `WorkPackageContextChanged`) is emitted when associations are updated.
5. **UI & User Experience on SCR-WP-001**:
   - The Work Package Detail Panel / Drawer in `WorkPackageView.vue` provides dedicated editing controls for Customer and Product (such as dropdown selectors including an unassigned / none option, accompanied by a Save action), consistent with Owner and Deadline editing sections.
   - The metadata summary display updates immediately to reflect the saved associations.

## Current Situation

1. The `WorkPackage` aggregate root (`WorkPackage.cs`) only accepts `customerId` and `productId` in its factory method `Create(...)` and persistence rehydration method; it lacks domain methods to mutate `CustomerId` and `ProductId`.
2. `IWorkPackageService` and `WorkPackagesController` do not provide commands, handlers, or REST API endpoints for updating Customer or Product on an existing Work Package.
3. In `WorkPackageView.vue`, Customer and Product are rendered solely as read-only static text in the detail panel, with no selection controls or save mechanism available after creation.

## Evidence

- Domain aggregate: [WorkPackage.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackage.cs#L29-L34)
- Domain specification: [work-package-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/work-package-domain.md#L88-L98)
- Application service: [WorkPackageService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageService.cs#L98-L155)
- API controller: [WorkPackagesController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/WorkPackagesController.cs#L140-L170)
- Screen component: [WorkPackageView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue#L1801-L1808)
- Frontend API helper: [workpackages.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/workpackages.ts#L61-L68)
- Intake interview alignment: Intake interview on 2026-10-07 (/grill-me) confirming:
  - No cascading update to constituent Requests.
  - Allowed in DRAFT and ACTIVE states; strictly forbidden once CLOSED.
  - Independent clearing (nulling) of Customer and Product supported.
  - Unified context endpoint (`PUT /api/v1/work-packages/{id}/context`).
  - Unified domain event (`WorkPackageContextChanged`).
  - Dedicated section with dropdowns and Save button in the detail drawer.

## Notes

- Intake interview confirmed:
  - Scope: Work Package aggregate root and `SCR-WP-001` screen.
  - Cascading: Strict aggregate boundary, no cascading to constituent Requests.
  - Lifecycle: Allowed in `DRAFT` and `ACTIVE`; immutable in `CLOSED`.
  - Mutability: Clearable / nullable independently.
  - Eventing: Unified `WorkPackageContextChanged` domain event.
- Downstream workflow routing:
  - Next Stage: Discovery & Feasibility Assessment
  - Owning Role: `ica-analyst`
