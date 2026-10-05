---
Title: Feasibility Assessment for Administrative Customer Management Screen with Contact Persons Management
Code: CR-010
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-05
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Feature being assessed: Manage Customer Master Data and Contacts (`FEAT-CUST-001-manage-customer-master.md`), per request in [CR-010-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-010-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-010-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-010-ISSUE.md)
- FEATURE: [FEAT-CUST-001-manage-customer-master.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-CUST-001-manage-customer-master.md)
- DOMAIN: [customer-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/customer-domain.md)
- FEATURE (Reference): [FEAT-ORG-001-manage-persons.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-ORG-001-manage-persons.md)
- FEATURE (Reference): [FEAT-USR-001-manage-user-accounts.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-001-manage-user-accounts.md)
- ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

## Objective

Assess the feasibility, technical baseline, gaps, architectural impacts, and planning readiness to:

1. Introduce a dedicated Administrative Customer Management Screen (`CustomerManagementView.vue` at `/admin/customers`, `SCR-CUST-001`) with summary KPI metric cards, keyword search, status filtering, high-density tabular listings, and action buttons.
2. Provide a Customer Master modal dialog (`CustomerModal.vue`) supporting Create and Edit modes for Customer Code, Customer Name, and Active Maintenance Contract status.
3. Provide an embedded Customer Contact Person management interface (`CustomerContactModal.vue`) allowing administrators to view, add, edit, and toggle active/inactive status for contact persons associated with a customer.
4. Provide one-click Customer Activation and Deactivation lifecycle mutations with instant UI state updates and feedback.
5. Integrate the `/admin/customers` route into `router/index.ts` with `requiresRole: 'Administrator'` and add navigation links to `App.vue` under the Administration section.

---

# 2. Current State

Summarize relevant findings from current artifacts and codebase.

## Existing Behavior

1. **Backend REST API (`CustomersController.cs`)**:
   - [CustomersController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/CustomersController.cs) provides complete CRUD and lifecycle REST endpoints:
     - `GET /api/v1/customers` (with `activeOnly` query param)
     - `GET /api/v1/customers/active`
     - `GET /api/v1/customers/{id}`
     - `GET /api/v1/customers/{id}/contacts`
     - `POST /api/v1/customers`
     - `PUT /api/v1/customers/{id}`
     - `PUT /api/v1/customers/{id}/activate`
     - `PUT /api/v1/customers/{id}/deactivate`
     - `POST /api/v1/customers/{id}/contacts`
     - `PUT /api/v1/customers/{id}/contacts/{contactId}`
2. **Backend Customer Module (`Cakra.Modules.Customer`)**:
   - [Customer.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Customer/Domain/Customer.cs) and [CustomerContact.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Customer/Domain/CustomerContact.cs) enforce aggregate boundaries, state lifecycles (`ACTIVE`, `INACTIVE`), and event publication (`CustomerCreated`, `CustomerActivated`, `CustomerInactivated`, `CustomerContactAdded`, etc.).
   - [CustomerService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Customer/Services/CustomerService.cs) and [CustomerQueryService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Customer/Services/CustomerQueryService.cs) implement full command and query handling.
3. **Frontend API Client (`customers.ts`)**:
   - [customers.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/customers.ts) contains all required API client methods (`listCustomers`, `listActiveCustomers`, `getCustomerById`, `getCustomerContacts`, `createCustomer`, `updateCustomer`, `activateCustomer`, `deactivateCustomer`, `createCustomerContact`, `updateCustomerContact`).
4. **Frontend Existing Views & Modals**:
   - Customer interactions currently exist only inside [CustomerPortfolioView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/CustomerPortfolioView.vue) (`/analytics/customer-portfolio`), which is an analytical portfolio summary rather than a dedicated administrative master maintenance view.
   - [CreateCustomerModal.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/CreateCustomerModal.vue) and [EditCustomerModal.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/EditCustomerModal.vue) exist as legacy modals tied specifically to the portfolio view.
   - There is no dedicated `/admin/customers` view matching the unified pattern of [PersonManagementView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/PersonManagementView.vue) and [UserManagementView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/UserManagementView.vue).

## Existing Constraints

1. **Role Access**: Administrative management screens must be restricted to users possessing the `Administrator` or `Admin` role in both client-side route guards and UI rendering.
2. **Customer Identity**: Customer Code must remain unique and immutable after creation.
3. **Soft Lifecycle Transitions**: Customers and Contacts must not be physically deleted; they transition between `ACTIVE` and `INACTIVE` to preserve historical operational references.
4. **Contact Ownership**: Customer Contacts belong strictly to their parent Customer organization.

---

# 3. Gap Analysis

Identify gaps between the requested FEATURE (`FEAT-CUST-001`) and the current system.

| ID | Severity | Gap |
|------|------|------|
| GAP-001 | CRITICAL | Dedicated `CustomerManagementView.vue` screen does not exist at `/admin/customers`. |
| GAP-002 | CRITICAL | Route `/admin/customers` (`SCR-CUST-001`) is missing from `router/index.ts`. |
| GAP-003 | MAJOR | Navigation link for "Customer Management" is missing from the Administration section in `App.vue`. |
| GAP-004 | MAJOR | Dedicated, reusable `CustomerModal.vue` supporting both create and edit modes with responsive validation is missing for administrative CRUD. |
| GAP-005 | MAJOR | Dedicated `CustomerContactModal.vue` for managing contact persons (view, add, edit, toggle status) per customer is missing. |
| GAP-006 | MINOR | `currentScreenTitle` computation in `App.vue` lacks mapping for `/admin/customers`. |

---

# 4. Open Questions

Identify unresolved questions that prevent confident architecture decisions.

| ID | Question | Impact |
|------|------|------|
| OQ-001 | Should `CustomerManagementView.vue` replace the legacy customer modals in `CustomerPortfolioView.vue` or coexist alongside them? | Determines whether to refactor `CustomerPortfolioView.vue` to use the unified `CustomerModal.vue` or link directly to `/admin/customers`. |
| OQ-002 | Should contact persons be managed via a dedicated sub-modal dialog or an expandable drawer/accordion row in the table? | Affects UI layout and component modularity. |

---

# 5. Assumptions

Document assumptions made during the assessment.

| ID | Assumption |
|------|------|
| ASM-001 | Existing backend REST API in `CustomersController.cs` and `Cakra.Modules.Customer` is feature-complete and requires no schema or backend command changes. |
| ASM-002 | Frontend TypeScript API module `@/api/customers.ts` contains all necessary methods for customers and contacts and can be used directly. |
| ASM-003 | Administrative access should strictly follow the pattern of `PersonManagementView.vue` and `UserManagementView.vue` (`requiresRole: 'Administrator'`). |

---

# 6. Risks

Document identified risks.

| ID | Risk | Impact | Mitigation |
|------|------|------|------|
| RISK-001 | Attempting to deactivate a customer with active operational requests could confuse users if not clearly indicated. | Low | Retain `INACTIVE` badge and preserve historical records without breaking active requests, following Domain Rule 5. |
| RISK-002 | Duplicate customer code submission. | Low | Handle HTTP 409 Conflict from `POST /api/v1/customers` with clear inline error message on the modal form. |

---

# 7. Recommendations

## Option A: Unified Administration Screen with Modular Modals (Recommended)

1. Create `CustomerManagementView.vue` under `/admin/customers` with KPI summary cards (Total, Active, Inactive, Maintenance Contracts), search, status filter, and high-density customer table.
2. Create `CustomerModal.vue` for creating and editing customer master data.
3. Create `CustomerContactModal.vue` for viewing and managing contact persons per customer.
4. Register the route in `router/index.ts` and add the navigation item to `App.vue`.

### Advantages

- Perfectly matches established architecture and patterns of `PersonManagementView.vue` and `UserManagementView.vue`.
- Clean component decomposition and separation of concerns.
- Zero backend code changes needed; leverages robust existing backend endpoints.

### Disadvantages

- Requires creating two new modal components and one view component.

## Option B: Monolithic View Component with Inline Sub-Forms

Implement all customer and contact modals directly inside `CustomerManagementView.vue` without decomposing into separate child modal components.

### Advantages

- Fewer files created.

### Disadvantages

- Leads to a large, hard-to-maintain single file exceeding standard component size limits.
- Violates single-responsibility and reusability design principles.

---

# 8. Gap Closure

Record resolutions for gaps and open questions based on the requirements alignment interview.

## GAP-001: Dedicated Customer Management View

### Decision
Implement `CustomerManagementView.vue` (`SCR-CUST-001`) in `src/views/` following the visual structure and behavior of `PersonManagementView.vue`.

### Rationale
Provides a cohesive administrative experience for managing customer master data with metrics, search, and lifecycle actions.

### Impact
Adds a new primary screen for administrators.

### Architecture Impact
Standard Vue 3 component additions within the existing frontend structure.

### Resolved By
Analyst (with User Alignment)

### Resolved Date
2026-10-05

---

## GAP-002 & GAP-003 & GAP-006: Routing and Navigation

### Decision
Register `/admin/customers` in `router/index.ts` with `requiresAuth: true` and `requiresRole: 'Administrator'`. Add "Customer Management" link under Administration in `App.vue` and update `currentScreenTitle`.

### Rationale
Ensures secure role-based access and seamless discovery in the application navigation menu.

### Impact
Route accessible exclusively by authenticated Administrators.

### Architecture Impact
None; follows existing router guard convention.

### Resolved By
Analyst (with User Alignment)

### Resolved Date
2026-10-05

---

## GAP-004 & GAP-005: Customer and Contact Modals

### Decision
Create `CustomerModal.vue` (for Customer create/edit) and `CustomerContactModal.vue` (for managing contacts per customer) in `src/components/`.

### Rationale
Separates customer master editing from contact persons lifecycle while allowing easy invocation from the customer management table.

### Impact
Clean, modular component architecture.

### Architecture Impact
None.

### Resolved By
Analyst (with User Alignment)

### Resolved Date
2026-10-05

---

## OQ-001: Coexistence with CustomerPortfolioView

### Decision
`CustomerManagementView.vue` at `/admin/customers` serves as the authoritative administrative master data screen. `CustomerPortfolioView.vue` remains under `/analytics/customer-portfolio` as an analytical reporting screen.

### Rationale
Maintains clean separation between administrative master data operations and business intelligence / portfolio analytics.

### Impact
Clear operational boundaries.

### Architecture Impact
None.

### Resolved By
Analyst

### Resolved Date
2026-10-05

---

## OQ-002: Contact Persons Management UX

### Decision
Use a dedicated `CustomerContactModal.vue` triggered by a "Contacts" button on each customer row, showing the list of existing contacts and providing forms to add or edit contact persons.

### Rationale
Keeps the main customer table clean and uncluttered while providing full contact CRUD capabilities.

### Impact
Intuitive modal workflow for contact management.

### Architecture Impact
None.

### Resolved By
Analyst

### Resolved Date
2026-10-05

---

# 9. Architecture Applicability

Record the Architecture Applicability decision.

## Decision
ARCHITECTURE-NOT-REQUIRED

## Rationale
The technical architecture, backend modules (`Cakra.Modules.Customer`), domain models, REST API controllers (`CustomersController.cs`), and frontend API clients (`customers.ts`) are already fully implemented and tested. The change consists solely of frontend UI view, modal components, and route/nav configuration following established patterns (`SCR-ORG-001`, `SCR-USR-001`). No new backend services, schema changes, or cross-cutting structural modifications are required.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved
- [x] All required decisions recorded
- [x] All blocking open questions resolved
- [x] Architecture can be finalized or updated (or confirmed ARCHITECTURE-NOT-REQUIRED)

## Status

READY-FOR-PLANNING

## Notes

All feasibility gaps, open questions, and UI requirements have been closed and recorded. The Architect has evaluated Architecture Applicability as ARCHITECTURE-NOT-REQUIRED and formally granted the READY-FOR-PLANNING gate. Planning may proceed immediately.

---

# 11. References

Referenced artifacts:

- DOMAIN: [customer-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/customer-domain.md)
- FEATURE: [FEAT-CUST-001-manage-customer-master.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-CUST-001-manage-customer-master.md)
- ISSUE: [CR-010-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-010-ISSUE.md)

Referenced codebase locations:

- [CustomersController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/CustomersController.cs)
- [customers.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/customers.ts)
- [PersonManagementView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/PersonManagementView.vue)
- [UserManagementView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/UserManagementView.vue)
- [router/index.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/router/index.ts)
- [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue)
