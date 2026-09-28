# Feature

## Identity

ID: FEAT-MGT-002
Name: Review Customer Request Progress
Type: Query

## Purpose

Provides management with an aggregated portfolio view of request progress, delivery health, and status distribution grouped by Customer organization.

## User Outcome

Management monitors operational progress and request fulfillment for a customer, detects blocked or delayed requests, and decides on necessary managerial follow-up or intervention.

## Traceability

### Domains
- Request
- Customer

### Scenarios
- SC-MGT-002

### Use Cases
- UC-MGT-002

### User Journeys
- UJ-MGT-002

### Screens
- SCR-MGT-001

## Preconditions

- The actor holds the `Management` organizational role.
- Customer master records exist in the Customer Domain.

## Capability

The system provides a Customer selector on `SCR-MGT-001: Customer Progress Review`, computes portfolio health metrics (total active requests, requests blocked or needing attention, recent resolutions), renders a data grid of all requests associated with the selected customer, and enables management to navigate directly to `SCR-REQ-003: Request Detail` to inspect or intervene on specific requests.

## Business Rules

- Only customers recognized in the Customer Domain can be selected (Customer Domain Section 5, 6).
- Customer status and maintenance contract status (`HasActiveMaintenanceContract`) reflect authoritative Customer Domain state (Customer Domain Rule 9).
- Request progress, state, and ownership are derived directly from the authoritative Request Domain without inventing synthetic domain metrics (Manifesto Principles 2, 3, 16).
- Portfolio health summaries (Active, Blocked, Resolved counts) are derived projections computed from Request entities (Manifesto Principle 16).
- Selecting any request in the grid navigates to `SCR-REQ-003: Request Detail` (UI Layout 18-scr-mgt-001, lines 53-56).

## Success Result

Management inspects the comprehensive request portfolio for the selected customer, understands delivery progress and exceptions, and can drill down into individual requests.

## Failure Conditions

- Actor lacks the `Management` organizational role (authority failure).
- The selected Customer has no associated requests (system renders an empty request grid with zeroed summary metrics).

## Acceptance Criteria

- [ ] Screen `SCR-MGT-001` is accessible to authorized Management actors.
- [ ] Management can select any active Customer from the Customer Domain.
- [ ] Portfolio summary displays current counts for Active, Needs Attention/Blocked, and Resolved requests.
- [ ] Grid lists all Requests linked to the selected Customer with ID, Title, Status, Assignee, and Last Update.
- [ ] Selecting a request row navigates directly to `SCR-REQ-003: Request Detail`.
- [ ] Reviewing progress does not mutate any underlying Request or Customer domain state.

## Implementation Notes

Analytical query projection joining Customer master data with Request Aggregate instances.
