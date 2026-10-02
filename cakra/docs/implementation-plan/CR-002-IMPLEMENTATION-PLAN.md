---
Title: Implementation Plan for Customer Master Management - Add and Update Customer (CR-002)
Code: CR-002
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-03
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-002`: Customer Master Management - Add and Update Customer capability. Enable Administrators to create new Customer Master records, update existing Customer attributes, manage Customer status transitions (`ACTIVE` / `INACTIVE`), and maintain associated Customer Contacts via REST API mutation endpoints and frontend modal dialogs (`SCR-CUST-001`, `SCR-CUST-002`).

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- FEATURE: [FEAT-CUST-001-manage-customer-master.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-CUST-001-manage-customer-master.md)
- ARCHITECTURE: [CR-002-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-002-ARCHITECTURE.md) (authoritative capability architecture), [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- FEASIBILITY-ASSESSMENT: [CR-002-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-002-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

Architecture Applicability: ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers all backend API, frontend UI, documentation, and test suite updates required to realize `CR-002`:

1. **Backend API Controller (`Cakra.Api`)**:
   - Add HTTP `POST /api/v1/customers` endpoint in `CustomersController.cs` mapping to `CreateCustomerCommand`.
   - Add HTTP `PUT /api/v1/customers/{id}` endpoint in `CustomersController.cs` mapping to `UpdateCustomerCommand`.
   - Add HTTP `PUT /api/v1/customers/{id}/activate` and `PUT /api/v1/customers/{id}/deactivate` endpoints mapping to `ActivateCustomerCommand` and `DeactivateCustomerCommand`.
   - Add HTTP `POST /api/v1/customers/{id}/contacts` and `PUT /api/v1/customers/{id}/contacts/{contactId}` endpoints mapping to `CreateCustomerContactCommand` and `UpdateCustomerContactCommand`.
   - Implement error handling returning `409 Conflict` on duplicate code or `404 Not Found` on missing IDs.

2. **Frontend UI Components & API Helper (`Cakra.Web`)**:
   - Extend `Cakra.Web/src/api` with HTTP POST and PUT helper methods for Customer and Contact mutations.
   - Implement `CreateCustomerModal.vue` (`SCR-CUST-001`) with form validation for Customer Code, Customer Name, and Maintenance Contract toggle.
   - Implement `EditCustomerModal.vue` (`SCR-CUST-002`) allowing Customer attribute updates and Contact management.
   - Update `CustomerPortfolioView.vue` with "Add Customer" button and "Edit Customer" card actions.

3. **Documentation & Integration Test Suite (`Cakra.Api.Tests`, `cakra/docs`)**:
   - Create controller integration tests in `CustomersControllerTests.cs` asserting `201 Created`, `200 OK`, `400 Bad Request`, and `409 Conflict` HTTP responses.
   - Register screen IDs `SCR-CUST-001` and `SCR-CUST-002` in `screen-inventory.md` and `management-navigation.md`.

---

# 3. Dependencies

- .NET 8 SDK / C# 12
- Node.js & npm (Vue 3 / Vite)
- MediatR in-process command pipeline
- Microsoft SQL Server LocalDB for integration tests

For slice dependencies:
- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires referenced slice to have implementation status `IMPLEMENTED`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Backend API Controller Mutation Endpoints | IMPLEMENTED | GO | 1/1 |
| P2 - Frontend UI Modals & Portfolio Integration | IMPLEMENTED | GO | 1/1 |
| P3 - Integration Tests & Documentation Alignment | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Backend API Controller Mutation Endpoints

Implementation Status: IMPLEMENTED
Review Status: GO

### P1-S01

Title: Expose Customer & Customer Contact REST Mutation Endpoints in CustomersController

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Add HTTP `POST /api/v1/customers`, `PUT /api/v1/customers/{id}`, `PUT /api/v1/customers/{id}/activate`, `PUT /api/v1/customers/{id}/deactivate`, `POST /api/v1/customers/{id}/contacts`, and `PUT /api/v1/customers/{id}/contacts/{contactId}` endpoints in `CustomersController.cs` (`Cakra.Api`). Map request DTOs directly to MediatR commands (`CreateCustomerCommand`, `UpdateCustomerCommand`, `ActivateCustomerCommand`, `DeactivateCustomerCommand`, `CreateCustomerContactCommand`, `UpdateCustomerContactCommand`).

Depends On: None

Repository: `cakra`

Completion Criteria:
- `CustomersController.cs` exposes HTTP `POST /api/v1/customers` returning `201 CreatedAtAction` with `CustomerDto`.
- `CustomersController.cs` exposes HTTP `PUT /api/v1/customers/{id}` returning `200 OK` with updated `CustomerDto`.
- `CustomersController.cs` exposes `PUT /api/v1/customers/{id}/activate` and `deactivate` endpoints.
- `CustomersController.cs` exposes `POST` and `PUT` endpoints for Customer Contacts.
- Exception handling converts duplicate `CustomerCode` `InvalidOperationException` into HTTP `409 Conflict`.

Notes:
Utilizes existing domain commands in `Cakra.Modules.Customer.Services`.
Implemented mutation endpoints in `CustomersController.cs` for Customer and Customer Contact CRUD operations with RFC 7807 409 Conflict exception handling for duplicate CustomerCode.

---

## P2 - Frontend UI Modals & Portfolio Integration

Implementation Status: IMPLEMENTED
Review Status: GO

### P2-S02

Title: Implement Create/Edit Customer Modal Components and Portfolio View Action Buttons

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Implement `CreateCustomerModal.vue` (`SCR-CUST-001`) and `EditCustomerModal.vue` (`SCR-CUST-002`) components in `Cakra.Web/src/components`. Extend `Cakra.Web/src/api` with API mutation functions (`createCustomer`, `updateCustomer`, `activateCustomer`, `deactivateCustomer`, `createCustomerContact`, `updateCustomerContact`). Add "Add Customer" header button and card "Edit" action buttons to `CustomerPortfolioView.vue` to open modals and refresh customer data on save.

Depends On: P1-S01

Repository: `cakra`

Completion Criteria:
- `Cakra.Web/src/api/customers.ts` (or `http.ts`) exports customer mutation helper methods.
- `CreateCustomerModal.vue` provides form fields for Customer Code, Customer Name, Maintenance Contract toggle, and input validation.
- `EditCustomerModal.vue` enables updating Customer attributes, status toggling, and managing Customer Contacts.
- `CustomerPortfolioView.vue` renders "Add Customer" button and "Edit" action on customer cards, opening respective modals and re-fetching portfolio data upon save.

Notes:
Modal components emit `saved` events to trigger parent view data reload.
Created `Cakra.Web/src/api/customers.ts` with typed REST client helper methods (`createCustomer`, `updateCustomer`, `activateCustomer`, `deactivateCustomer`, `createCustomerContact`, `updateCustomerContact`, `getCustomerById`, `getCustomerContacts`).
Created `CreateCustomerModal.vue` (`SCR-CUST-001`) with form validation, error handling, and maintenance contract toggle.
Created `EditCustomerModal.vue` (`SCR-CUST-002`) supporting attribute editing, status activation/deactivation, and complete Customer Contact lifecycle management.
Updated `CustomerPortfolioView.vue` with "Add Customer" header button and summary card "Edit Customer" button, wired to open respective modals and reload customer dropdown and portfolio analytics on save.

---

## P3 - Integration Tests & Documentation Alignment

Implementation Status: IMPLEMENTED
Review Status: GO

### P3-S03

Title: Add Controller Integration Tests and Update Screen Inventory Documentation

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Create controller integration tests in `CustomersControllerTests.cs` asserting successful creation/updates (`201 Created`, `200 OK`) and validation/conflict error cases (`400 Bad Request`, `409 Conflict`). Register `SCR-CUST-001` and `SCR-CUST-002` in `screen-inventory.md` and `management-navigation.md`.

Depends On: P1-S01, P2-S02

Repository: `cakra`

Completion Criteria:
- Controller integration tests execute clean assertions for Customer and Contact mutation endpoints.
- All unit/integration tests pass cleanly.
- `screen-inventory.md` and `management-navigation.md` are updated with `SCR-CUST-001` and `SCR-CUST-002`.

Notes:
Created `CustomersControllerTests.cs` with integration tests covering `201 Created`, `200 OK`, `401 Unauthorized`, `404 Not Found`, and RFC 7807 `409 Conflict` duplicate CustomerCode handling for Customer and Customer Contact mutation endpoints.
Registered `SCR-CUST-001` (Create Customer Modal) and `SCR-CUST-002` (Edit Customer Modal) in `screen-inventory.md` and `management-navigation.md`.
Remediated RV-001 by updating `options.Cookie.SecurePolicy` to `CookieSecurePolicy.Always` in `CakraAuthenticationExtensions.cs`. All 202 unit tests and 124 integration tests pass cleanly with 0 failures.
