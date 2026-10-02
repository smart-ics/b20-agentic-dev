---
Title: Feasibility Assessment for Customer Master Management - Add and Update Customer (CR-002)
Code: CR-002
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-03
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Feature being assessed: Customer Master Management - Add and Update Customer capability ([FEAT-CUST-001-manage-customer-master.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-CUST-001-manage-customer-master.md)), per request in [CR-002-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-002-ISSUE.md).

Referenced artifacts:

- DOMAIN: [customer-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/customer-domain.md)
- FEATURE: [FEAT-CUST-001-manage-customer-master.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-CUST-001-manage-customer-master.md)
- ISSUE: [CR-002-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-002-ISSUE.md)

## Objective

Assess the feasibility, existing implementation baseline, gap closure decisions, architectural impacts, and planning readiness to:

1. Expose REST API mutation endpoints for creating, updating, activating, and deactivating Customer records and Customer Contacts.
2. Build frontend UI dialog components (`SCR-CUST-001: Create Customer`, `SCR-CUST-002: Edit Customer`) within `CustomerPortfolioView.vue` (or modal components).
3. Ensure domain events (`CustomerCreated`, `CustomerActivated`, `CustomerInactivated`) and business validation rules (code uniqueness, non-empty attributes) are enforced.

---

# 2. Current State

## Existing Behavior

1. **Backend Application Layer (`Cakra.Modules.Customer`)**:
   - `CustomerService.cs` already implements MediatR command handlers: `CreateCustomerCommand`, `UpdateCustomerCommand`, `CreateCustomerContactCommand`, `UpdateCustomerContactCommand`, `DeactivateCustomerCommand`, and `ActivateCustomerCommand`.
   - Aggregate classes `Customer.cs` and `CustomerContact.cs` and repositories `CustomerRepository.cs` and `CustomerContactRepository.cs` implement database CRUD operations against `[customer].[Customers]` and `[customer].[CustomerContacts]`.
2. **Backend REST API Controller Layer (`Cakra.Api`)**:
   - `CustomersController.cs` exposes ONLY HTTP GET lookup endpoints (`GET /api/v1/customers`, `GET /api/v1/customers/active`, `GET /api/v1/customers/{id}`, `GET /api/v1/customers/{id}/contacts`).
   - `CustomersController.cs` DOES NOT currently expose HTTP `POST` or `PUT` endpoints for creating, updating, or deactivating customer master records or contacts.
3. **Frontend UI Layer (`Cakra.Web`)**:
   - `CustomerPortfolioView.vue` is a read-only portfolio overview displaying customer cards and associated request summaries.
   - Frontend lacks UI modals or forms for adding a new customer or editing existing customer attributes/contacts.
4. **Documentation & Traceability**:
   - `customer-domain.md` documents Customer aggregate rules and domain lifecycles.
   - `FEAT-CUST-001-manage-customer-master.md` has been introduced to formalize user outcome and acceptance criteria.
   - Screen Inventory and Navigation maps currently lack `SCR-CUST-001` and `SCR-CUST-002` screen entries.

## Existing Constraints

1. `CustomerCode` must be unique across all customer records.
2. Deleting customer records with historical references is prohibited (Domain Rule 6); inactivation must be used instead.
3. Customer Contacts must belong to a single Customer aggregate.
4. `Customer` status state transitions permit `ACTIVE -> INACTIVE` and `INACTIVE -> ACTIVE`.

---

# 3. Gap Analysis

Identify gaps between the requested FEATURE and the current system.

| ID | Severity | Gap |
|---|---|---|
| GAP-001 | CRITICAL | REST API mutation endpoints (`POST /api/v1/customers`, `PUT /api/v1/customers/{id}`, `POST /api/v1/customers/{id}/contacts`, `PUT /api/v1/customers/{id}/contacts/{contactId}`) are missing in `CustomersController.cs`. |
| GAP-002 | CRITICAL | UI modal/form components (`CreateCustomerModal.vue` / `EditCustomerModal.vue`) and action buttons are missing from `CustomerPortfolioView.vue` in `Cakra.Web`. |
| GAP-003 | MAJOR | Navigation maps (`management-navigation.md`) and Screen Inventory (`screen-inventory.md`) do not document `SCR-CUST-001` (Create Customer) and `SCR-CUST-002` (Edit Customer). |
| GAP-004 | MINOR | Customer mutation API integration unit/integration tests covering `CustomersController` HTTP endpoints are missing. |

---

# 4. Open Questions

Identify unresolved questions that prevent confident architecture decisions.

| ID | Question | Impact |
|---|---|---|
| OQ-001 | Should Customer Master creation and editing be integrated into `CustomerPortfolioView.vue` via modal dialogs or as separate route pages? | UI layout, routing configuration, and navigation flow. |
| OQ-002 | Should Customer Contact management (Add/Update contact details) be included in the scope of `CR-002` alongside Customer aggregate updates? | API controller endpoint scope and modal form design. |
| OQ-003 | What authorization policies should be applied to `POST` / `PUT` endpoints in `CustomersController.cs`? | Security and role-based access control. |

---

# 5. Assumptions

Document assumptions made during the assessment.

| ID | Assumption |
|---|---|
| ASM-001 | Domain command handlers (`CreateCustomerCommand`, `UpdateCustomerCommand`) in `Cakra.Modules.Customer` are fully functional and require no internal domain model changes. |
| ASM-002 | Managing Customer Contacts is an integral part of Customer Master administration and should be supported via dedicated endpoints and UI modal tabs/forms. |
| ASM-003 | `CustomerCode` remains immutable after creation to maintain stable cross-domain foreign key references. |

---

# 6. Risks

Document identified risks.

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| RISK-001 | Unique code conflict when creating a customer with an existing `CustomerCode`. | Medium | Surface `InvalidOperationException` from service as HTTP `409 Conflict` or `400 Bad Request` with clear validation error message in UI. |
| RISK-002 | Deactivating a customer while active requests exist for that customer. | Low | Display a warning badge or confirmation modal in UI showing active request count before allowing inactivation. |

---

# 7. Recommendations

Recommend possible approaches for addressing major gaps.

## Option A: Integrated Modal Dialogs in Customer Portfolio View (Recommended)

Expose HTTP mutation endpoints in `CustomersController.cs` and implement `CreateCustomerModal.vue` and `EditCustomerModal.vue` as dialogs launched directly from `CustomerPortfolioView.vue`.

### Advantages

- Direct, seamless user experience without requiring full page navigation.
- Preserves existing route architecture (`/customers`).

### Disadvantages

- Modal dialog space must be cleanly organized to handle both Customer fields and Customer Contacts.

## Option B: Separate Route Views for Customer Add and Edit

Create dedicated route paths (`/customers/new`, `/customers/:id/edit`) and standalone full-page Vue components.

### Advantages

- Provides maximum screen real estate for complex contact lists and audit details.

### Disadvantages

- Adds page navigation overhead and requires adding new Vue router entries.

---

# 8. Gap Closure

Record resolutions for gaps and open questions.

## GAP-001

### Status

CLOSED

### Decision

Expose HTTP mutation endpoints in `CustomersController.cs`:
- `POST /api/v1/customers` -> `CreateCustomerCommand`
- `PUT /api/v1/customers/{id}` -> `UpdateCustomerCommand`
- `PUT /api/v1/customers/{id}/activate` -> `ActivateCustomerCommand`
- `PUT /api/v1/customers/{id}/deactivate` -> `DeactivateCustomerCommand`
- `POST /api/v1/customers/{id}/contacts` -> `CreateCustomerContactCommand`
- `PUT /api/v1/customers/{id}/contacts/{contactId}` -> `UpdateCustomerContactCommand`

### Rationale

`CustomerService.cs` already contains robust MediatR command handlers for all these operations; only the REST controller routing needs to be added. Accepted by User/PO.

### Impact

Enables full programmatic and UI-driven CRUD operations for Customer master data.

### Architecture Impact

Requires updating `CustomersController.cs` signature and OpenAPI annotations.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03


---

## GAP-002

### Status

CLOSED

### Decision

Implement `CreateCustomerModal.vue` and `EditCustomerModal.vue` components and integrate them with "Add Customer" and "Edit Customer" action buttons in `CustomerPortfolioView.vue`.

### Rationale

Option A provides an intuitive in-context administration workflow without cluttering router definitions. Accepted by User/PO.

### Impact

Frontend users with appropriate permissions can directly add or edit customer records via action buttons in the portfolio view.

### Architecture Impact

Adds modal components under `Cakra.Web/src/components` and API calls in `Cakra.Web/src/api`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## GAP-003

### Status

CLOSED

### Decision

Document screen IDs `SCR-CUST-001` (Create Customer Modal) and `SCR-CUST-002` (Edit Customer Modal) in `screen-inventory.md` and `management-navigation.md`.

### Rationale

Maintains complete UI artifact traceability across the system.

### Impact

Ensures UI coverage and traceability matrix compliance.

### Architecture Impact

Documentation updates only.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## GAP-004

### Status

CLOSED

### Decision

Add controller-level integration tests asserting HTTP `201 Created`, `200 OK`, `400 Bad Request`, and `409 Conflict` responses for Customer mutation endpoints.

### Rationale

Ensures backend controller logic and validation rules are thoroughly tested.

### Impact

Prevents regressions in Customer API operations.

### Architecture Impact

Adds test coverage in backend test suite.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## OQ-001

### Decision

Use integrated Modal Dialogs in `CustomerPortfolioView.vue` (Option A).

### Rationale

Simpler UX and minimal routing footprint.

### Impact

Fast operational workflow.

### Architecture Impact

Component-level modal state in `CustomerPortfolioView.vue`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## OQ-002

### Decision

Include Customer Contact creation and editing within the scope of `CR-002` and `FEAT-CUST-001`.

### Rationale

Customer Contacts belong to the Customer Aggregate and are essential contact details for operational communication.

### Impact

Complete Customer Master administration capability delivered in one feature package.

### Architecture Impact

Exposes contact mutation API endpoints and contact editing form fields.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## OQ-003

### Decision

Apply `[Authorize]` attribute on `CustomersController.cs` mutation endpoints, standardizing role access to logged-in users with Administrator role where applicable.

### Rationale

Aligns with `customer-domain.md` §4 specifying `Administrator` as responsible for maintaining Customer master data.

### Impact

Secures Customer master mutation endpoints.

### Architecture Impact

Standard ASP.NET Core authorization attributes.

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

Introducing HTTP mutation endpoints in `CustomersController.cs`, defining new UI screen components (`SCR-CUST-001`, `SCR-CUST-002`), and updating navigation/screen inventory artifacts involve structural API, UI, and documentation changes that require formal target-state architecture updates and implementation planning by `ica-architect`.

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

All feasibility gaps, open questions, and closure decisions have been reviewed and accepted. Gate `READY-FOR-PLANNING` granted by `ica-architect`. Target architecture artifact `CR-002-ARCHITECTURE.md` has been created.

---

# 11. References

Referenced artifacts:

- DOMAIN: [customer-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/customer-domain.md)
- FEATURE: [FEAT-CUST-001-manage-customer-master.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-CUST-001-manage-customer-master.md)
- ISSUE: [CR-002-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-002-ISSUE.md)

Referenced codebase locations:

- [CustomersController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/CustomersController.cs)
- [CustomerCommands.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Customer/Services/CustomerCommands.cs)
- [CustomerService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Customer/Services/CustomerService.cs)
- [CustomerPortfolioView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/CustomerPortfolioView.vue)
