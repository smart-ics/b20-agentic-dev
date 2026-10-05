# ISSUE

## Metadata

ID: CR-010
Type: CHANGE-REQUEST
Status: OPEN
Title: Administrative Customer Management Screen with Contact Persons Management

## Source

Reported By: User
Reported Date: 2026-10-05

## Description

The user requested a dedicated Customer Management interface to manage customers. Through the requirements alignment interview, requirements were specified for a dedicated Administrative Customer Management screen (`SCR-CUST-001` / `CustomerManagementView.vue`) at route `/admin/customers`, accessible to Administrators. The screen provides searchable/filterable high-density tabular listings of customers, summary metrics (Total Customers, Active, Inactive, Active Maintenance Contracts), modal dialogs for creating and editing customer master data, modal dialogs for managing customer contact persons, and one-click customer activation/deactivation controls.

## Desired Outcome

1. A dedicated administrative view for managing customers (`CustomerManagementView.vue` at `/admin/customers`), guarded with `requiresAuth: true` and `requiresRole: 'Administrator'`.
2. Navigation entry in the sidebar menu under the "Administration" section with topbar contextual title "Customer Management".
3. Summary metric KPI cards displaying total customers, active customers, inactive customers, and active maintenance contracts count.
4. Real-time search by customer code and customer name, along with status filtering (All, Active, Inactive).
5. Customer Master creation and editing modal dialog (`CustomerModal.vue`) capturing Customer Code, Customer Name, and Active Maintenance Contract flag with client-side validation.
6. Embedded Customer Contact Person management allowing users to view, add, and edit contacts (Contact Name, Position, Email, Phone Number, Status) associated with any customer.
7. One-click Activation and Deactivation status toggle actions with clear visual loading state and error handling.

## Current Situation

1. The backend (`Cakra.Modules.Customer`) provides domain entities (`Customer`, `CustomerContact`) and API endpoints (`GET /customers`, `POST /customers`, `PUT /customers/{id}`, `PUT /customers/{id}/activate`, `PUT /customers/{id}/deactivate`, `GET /customers/{id}/contacts`, `POST /customers/{id}/contacts`, `PUT /customers/{id}/contacts/{contactId}`) and a TypeScript client in `@/api/customers.ts`.
2. Currently, customer interactions on the frontend are limited to `CustomerPortfolioView.vue` (`SCR-MGT-001`) under `/analytics/customer-portfolio`, which is an analytics/portfolio view rather than an administrative management interface.
3. There is no dedicated administrative route `/admin/customers` or primary customer master management table in the frontend navigation menu alongside User Management (`/admin/users`) and Person Management (`/admin/persons`).

## Evidence

- User request: "I need a form to manage customer."
- User interview / grill-me responses:
  - Dedicated Customer Management view under Administration (`/admin/customers`).
  - Guarded with Administrator role.
  - Summary metrics cards (Total, Active, Inactive, Active Maintenance Contracts).
  - Keyword search & status filtering.
  - Customer create/edit modal with validation.
  - Inline or modal-based Contact Persons management.
  - One-click customer activation/deactivation toggle.
  - Utilize existing Customer and Contact API endpoints.
- Frontend customer API: [customers.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/customers.ts)
- Navigation router: [index.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/router/index.ts)
- Application shell & sidebar: [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue)
- Backend Customer module: [Cakra.Modules.Customer](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Customer)

## Notes

This artifact formally captures the intake request for the Customer Management change request in a solution-neutral manner according to Knowledge-Centric SDLC standards. Detailed feasibility assessment, domain and feature updates, architecture design, and implementation planning belong to downstream analysis and architecture stages.
