---
Title: Customer Master Management - Add and Update Customer Architecture
Code: CR-002
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-03
---

# 1. Overview

This architecture defines the technical realization for Change Request `CR-002`: Customer Master Management - Add and Update Customer capability.

It realizes [FEAT-CUST-001-manage-customer-master.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-CUST-001-manage-customer-master.md) and technical gap closures from [CR-002-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-002-FEASIBILITY-ASSESSMENT.md), establishing REST API mutation endpoints, modal UI screen components (`SCR-CUST-001: Create Customer`, `SCR-CUST-002: Edit Customer`), and full Customer & Contact CRUD capabilities.

# 2. Architectural Basis

## Business Context

- DOMAIN: [customer-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/customer-domain.md)
- FEATURE: [FEAT-CUST-001-manage-customer-master.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-CUST-001-manage-customer-master.md)
- ISSUE: [CR-002-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-002-ISSUE.md)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-002-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-002-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

This architecture consumes and realizes the approved decisions:
- `GAP-001`: Expose HTTP mutation endpoints in `CustomersController.cs` (`POST /api/v1/customers`, `PUT /api/v1/customers/{id}`, `PUT /api/v1/customers/{id}/activate`, `PUT /api/v1/customers/{id}/deactivate`, `POST /api/v1/customers/{id}/contacts`, `PUT /api/v1/customers/{id}/contacts/{contactId}`).
- `GAP-002`: Implement action buttons and UI modals (`CreateCustomerModal.vue`, `EditCustomerModal.vue`) in `CustomerPortfolioView.vue`.
- `GAP-003`: Document screens `SCR-CUST-001` and `SCR-CUST-002` in navigation and screen inventory.
- `GAP-004`: Add controller integration tests for Customer mutation operations.
- `OQ-001`: In-context Modal Dialog UX pattern.
- `OQ-002`: Include Customer Contact management in `CR-002` scope.
- `OQ-003`: Standard ASP.NET Core authorization policy enforcement.

# 3. Scope

## Included
- Addition of REST API mutation actions to `CustomersController.cs` (`Cakra.Api`):
  - `POST /api/v1/customers` -> `CreateCustomerCommand`
  - `PUT /api/v1/customers/{id}` -> `UpdateCustomerCommand`
  - `PUT /api/v1/customers/{id}/activate` -> `ActivateCustomerCommand`
  - `PUT /api/v1/customers/{id}/deactivate` -> `DeactivateCustomerCommand`
  - `POST /api/v1/customers/{id}/contacts` -> `CreateCustomerContactCommand`
  - `PUT /api/v1/customers/{id}/contacts/{contactId}` -> `UpdateCustomerContactCommand`
- Creation of `CreateCustomerModal.vue` and `EditCustomerModal.vue` components in `Cakra.Web`.
- Updates to `CustomerPortfolioView.vue` to add "Add Customer" button and "Edit Customer" card action.
- Update to frontend API client (`Cakra.Web/src/api`) to expose mutation HTTP helpers.
- Updates to Screen Inventory (`screen-inventory.md`) and Navigation maps (`management-navigation.md`).
- Integration tests in `Cakra.Api.Tests` covering `CustomersController` mutation endpoints.

## Excluded
- Database schema changes (tables `[customer].[Customers]` and `[customer].[CustomerContacts]` already exist).
- Domain service changes in `Cakra.Modules.Customer` (command handlers already exist in `CustomerService.cs`).

# 4. Technical Decisions

## TD-001: REST API Controller Wiring to Existing MediatR Handlers
`CustomersController` (`Cakra.Api`) will inject `IMediator` and map incoming HTTP payloads directly to `CreateCustomerCommand`, `UpdateCustomerCommand`, `ActivateCustomerCommand`, `DeactivateCustomerCommand`, `CreateCustomerContactCommand`, and `UpdateCustomerContactCommand`.

## TD-002: Error Handling & Code Conflict Mapping
- When `CreateCustomerCommand` throws `InvalidOperationException` due to duplicate `CustomerCode`, `CustomersController` returns HTTP `409 Conflict` (or `400 Bad Request` with problem details).
- When a target Customer ID is not found, controller returns HTTP `404 Not Found`.

## TD-003: Frontend Modal Dialog Pattern
Modal dialogs (`CreateCustomerModal.vue` and `EditCustomerModal.vue`) will be implemented as modular components in `Cakra.Web/src/components`. They emit `saved` events to `CustomerPortfolioView.vue` to trigger automated list re-fetching.

## TD-004: Contact Management within Edit Customer Modal
`EditCustomerModal.vue` provides tabbed or sectioned UI allowing administrators to update Customer attributes (Name, Maintenance status, Active status) as well as managing associated contacts (Add contact, edit position/phone/email/status).

# 5. Component Responsibilities

| Component | Responsibility |
|---|---|
| `CustomersController` (`Cakra.Api`) | Exposes HTTP GET lookup endpoints and HTTP POST/PUT mutation endpoints for Customer and Customer Contact CRUD operations. |
| `CustomerService` (`Cakra.Modules.Customer`) | Handles `CreateCustomerCommand`, `UpdateCustomerCommand`, `ActivateCustomerCommand`, `DeactivateCustomerCommand`, `CreateCustomerContactCommand`, `UpdateCustomerContactCommand`. |
| `CustomerPortfolioView.vue` (`Cakra.Web`) | Portfolio list view displaying customer cards, summary counts, "Add Customer" action header button, and "Edit" card actions. |
| `CreateCustomerModal.vue` (`Cakra.Web`) | Form modal (`SCR-CUST-001`) capturing `CustomerCode`, `CustomerName`, and `HasActiveMaintenanceContract`. |
| `EditCustomerModal.vue` (`Cakra.Web`) | Form modal (`SCR-CUST-002`) displaying and updating existing Customer attributes and nested Customer Contacts list. |

# 6. Integration Design

| Source | Target | Purpose |
|---|---|---|
| `CustomerPortfolioView.vue` | `CreateCustomerModal.vue` / `EditCustomerModal.vue` | Opens creation or edit modal dialog for selected customer. |
| `CreateCustomerModal.vue` / `EditCustomerModal.vue` | `Cakra.Web/src/api` | Sends HTTP `POST`/`PUT` requests to backend Customer endpoints. |
| `CustomersController` (`Cakra.Api`) | `IMediator` | Dispatches MediatR commands (`CreateCustomerCommand`, etc.) to `Cakra.Modules.Customer`. |
| `CustomerService` | `ICustomerRepository` / `ICustomerContactRepository` | Persists aggregate updates to SQL database. |

# 7. Data Ownership

| Data | Owner |
|---|---|
| Customer Master Records (`[customer].[Customers]`) | `Customer` Domain (`Cakra.Modules.Customer`) |
| Customer Contact Records (`[customer].[CustomerContacts]`) | `Customer` Domain (`Cakra.Modules.Customer`) |

# 8. Database Design

## New Tables
None required.

## Modified Tables
None required (`[customer].[Customers]` and `[customer].[CustomerContacts]` already contain required schema).

## Relationships
Existing 1-to-N relationship between `Customer` (`CustomerId`) and `CustomerContact` (`CustomerId`).

## Migration Considerations
No SQL migrations required.

# 9. Cross-Cutting Concerns

- **Security**: Apply `[Authorize]` attribute to all mutation endpoints in `CustomersController.cs`.
- **Validation**: Enforce FluentValidation rules (`CreateCustomerCommandValidator`, `UpdateCustomerCommandValidator`) via MediatR pipeline.
- **Audit**: `CreatedAt` and `UpdatedAt` timestamps automatically managed by `Customer.cs` domain entity.

# 10. Implementation Constraints

- Must use existing MediatR command handlers in `Cakra.Modules.Customer`.
- Must not perform physical DELETE operations on customer records; use deactivation instead.
- REST endpoints must conform to API naming standards (`api/v1/customers/...`).

# 11. Acceptance Conditions

- [ ] HTTP `POST /api/v1/customers` creates a new Customer and returns `201 Created` with `CustomerDto`.
- [ ] HTTP `PUT /api/v1/customers/{id}` updates Customer attributes and returns `200 OK`.
- [ ] HTTP `PUT /api/v1/customers/{id}/activate` and `deactivate` correctly toggle Customer status.
- [ ] HTTP `POST` and `PUT` endpoints for contacts manage contact records.
- [ ] UI provides "Add Customer" button and "Edit Customer" card actions in `CustomerPortfolioView.vue`.
- [ ] UI modals `CreateCustomerModal.vue` (`SCR-CUST-001`) and `EditCustomerModal.vue` (`SCR-CUST-002`) function correctly.
- [ ] Integration test suite covers all new controller endpoints.
