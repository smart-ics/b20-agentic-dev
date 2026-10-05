---
Title: Administrative Customer Management Screen Implementation Plan
Code: CR-010
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-05
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement the Administrative Customer Management screen, Customer Master modal, and Customer Contact Person modal per [FEAT-CUST-001-manage-customer-master.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-CUST-001-manage-customer-master.md) and [CR-010-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-010-FEASIBILITY-ASSESSMENT.md).

**Planning Mode:** FEATURE-PLANNING

**Referenced artifacts:**
- FEATURE: [FEAT-CUST-001-manage-customer-master.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-CUST-001-manage-customer-master.md)
- FEASIBILITY-ASSESSMENT: [CR-010-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-010-FEASIBILITY-ASSESSMENT.md) (Planning Readiness: `READY-FOR-PLANNING`)
- DOMAIN: [customer-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/customer-domain.md)

**Architecture Applicability:** ARCHITECTURE-NOT-REQUIRED

No architectural target-state artifact was required. Implementation relies on existing technical structure. Approved feasibility decisions are authoritative for the change. The current codebase is the source of current technical truth.

---

# 2. Planning Scope

This plan covers the implementation of the dedicated Administrative Customer Management interface, Customer modal, Customer Contact modal, router and navigation integration, build verification, and traceability documentation.

Scope breakdown:
- Frontend Presentation:
  - `CustomerModal.vue` (`SCR-CUST-002`): Reusable modal supporting Create and Edit modes for Customer Code, Customer Name, and Active Maintenance Contract status flag.
  - `CustomerContactModal.vue` (`SCR-CUST-003`): Reusable modal for managing contact persons (view list, create, edit, toggle status) for a selected customer.
  - `CustomerManagementView.vue` (`SCR-CUST-001`): Primary administration screen at `/admin/customers` featuring summary KPI cards, keyword search, status filtering, customer data table, and one-click activation/deactivation.
  - `router/index.ts` & `App.vue`: Route `/admin/customers` registration guarded with `requiresRole: 'Administrator'`, sidebar navigation entry under Administration, and topbar screen title mapping.
- Build & Verification:
  - `npm run type-check` and `npm run build` in `Cakra.Web`.
- Documentation & Traceability:
  - Screen inventory, feature catalog, and feature traceability documentation updates.

---

# 3. Dependencies

**External Dependencies:** None (backend REST API endpoints and `@/api/customers.ts` are already fully implemented).

**Slice Dependencies:** Listed in `Depends On` field for each slice below.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---------|---------|---------|---------|
| P1 - Frontend Modal Components | IMPLEMENTED | GO | 2/2 |
| P2 - View & Navigation Integration | IMPLEMENTED | GO | 2/2 |
| P3 - Build & Type Verification | IMPLEMENTED | GO | 1/1 |
| P4 - Documentation & Traceability | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Frontend Modal Components

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P1-S01

**Title:** Customer Master Create & Edit Modal Component (`CustomerModal.vue`)

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Create `CustomerModal.vue` in `src/components/` supporting `create` and `edit` modes for Customer Master records.

**Depends On:** None

**Repository:** Cakra.Web

**Completion Criteria:**
- `CustomerModal.vue` accepts props: `isOpen: boolean`, `mode: 'create' | 'edit'`, `customer: CustomerDto | null`.
- Emits events: `close`, `saved`.
- Form inputs: Customer Code (required, uppercase formatted, editable only in `create` mode), Customer Name (required), Has Active Maintenance Contract (switch / checkbox).
- Client-side validation: prevents submission when code or name is blank.
- Invokes `createCustomer(...)` or `updateCustomer(...)` from `@/api/customers`.
- Displays inline loading indicator and server-returned error message (e.g. 409 conflict).
- Modal backdrop and ESC/Close button handlers operate cleanly without memory leaks.

---

### P1-S02

**Title:** Customer Contact Persons Management Modal (`CustomerContactModal.vue`)

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Create `CustomerContactModal.vue` in `src/components/` for managing contact persons associated with a customer.

**Depends On:** None

**Repository:** Cakra.Web

**Completion Criteria:**
- `CustomerContactModal.vue` accepts props: `isOpen: boolean`, `customer: CustomerDto | null`.
- Emits event: `close`.
- Fetches contacts using `getCustomerContacts(customer.id)` on modal open.
- Displays contacts in a compact table (Name, Position, Email, Phone, Status badge, Actions).
- Provides an inline or sub-section form to Add Contact and Edit Contact (Name, Position, Email, Phone Number, Status).
- Validates contact name is non-empty and email has valid format if provided.
- Invokes `createCustomerContact(...)` and `updateCustomerContact(...)` from `@/api/customers`.
- Refreshes contact list upon save and shows clear success/error notifications.

---

## P2 - View & Navigation Integration

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P2-S03

**Title:** Customer Management Screen (`CustomerManagementView.vue`)

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Create `CustomerManagementView.vue` (`SCR-CUST-001`) in `src/views/` following the visual design and structure of `PersonManagementView.vue` and `UserManagementView.vue`.

**Depends On:** P1-S01, P1-S02

**Repository:** Cakra.Web

**Completion Criteria:**
- Loads all customers via `listCustomers()` from `@/api/customers`.
- Displays top summary KPI cards: Total Customers, Active Customers, Inactive Customers, Active Maintenance Contracts.
- Search and filter bar: keyword search matching customer code and name, status filter dropdown (`ALL`, `ACTIVE`, `INACTIVE`).
- Customer Table renders: Customer Code, Customer Name, Active Maintenance Contract badge, Status badge (`ACTIVE` / `INACTIVE`), and Action buttons.
- "Add Customer" button triggers `CustomerModal` in `create` mode.
- "Edit" action triggers `CustomerModal` in `edit` mode.
- "Contacts" action triggers `CustomerContactModal` with selected customer.
- "Activate" / "Deactivate" action toggles status via `activateCustomer(id)` / `deactivateCustomer(id)` with confirmation prompt and loading state.
- Table and KPI metrics automatically refresh after any mutation.

---

### P2-S04

**Title:** Router Configuration & Sidebar Navigation Integration

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Integrate `/admin/customers` route in Vue Router and add navigation item under Administration in `App.vue`.

**Depends On:** P2-S03

**Repository:** Cakra.Web

**Completion Criteria:**
- `src/router/index.ts`: Add route `/admin/customers` with `component: () => import('@/views/CustomerManagementView.vue')`, `requiresAuth: true`, `requiresRole: 'Administrator'`, `screenId: 'SCR-CUST-001'`.
- `src/App.vue`:
  - Add sidebar link `<router-link to="/admin/customers">` with building icon (`bi-building-gear` or `bi-building`) in the Administration section (under `v-if="isAdmin"`).
  - Update `currentScreenTitle` computed property: `if (path.startsWith('/admin/customers')) return 'Customer Management'`.

---

## P3 - Build & Type Verification

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P3-S05

**Title:** Frontend TypeScript Compilation and Build Verification

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Verify that `Cakra.Web` compiles with zero TypeScript errors and passes production build.

**Depends On:** P2-S04

**Repository:** Cakra.Web

**Completion Criteria:**
- `npm run type-check` executes with 0 errors.
- `npm run build` succeeds cleanly.

---

## P4 - Documentation & Traceability

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P4-S06

**Title:** Screen Inventory & Feature Traceability Documentation Update

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Update documentation artifacts to reflect `SCR-CUST-001`, `SCR-CUST-002`, `SCR-CUST-003`, and feature traceability for `FEAT-CUST-001`.

**Depends On:** P2-S04

**Repository:** docs

**Completion Criteria:**
- Update `cakra/docs/navigation/screen-inventory.md` with `SCR-CUST-001` (Customer Management), `SCR-CUST-002` (Customer Modal), and `SCR-CUST-003` (Customer Contact Modal).
- Update `cakra/docs/features/feature-traceability.md` and `feature-catalog.md` linking `FEAT-CUST-001` and `CR-010`.

---

# 6. Change Log

- 2026-10-05: Initial creation and release of approved implementation plan for CR-010 (Execution Approval: APPROVED).
