# Feature

## Identity

ID: FEAT-CUST-001
Name: Manage Customer Master Data
Type: Command

## Purpose

Enables Administrators to create new Customer Master records and update existing Customer Master records to maintain authoritative customer organizational data.

## User Outcome

Customer Master data is accurately created, updated, and maintained in an authoritative state, making valid customer records available for operational demand capture and tracking.

## Traceability

### Domains
- Customer

### Scenarios
- SC-CUST-001

### Use Cases
- UC-CUST-001

### User Journeys
- UJ-CUST-001

### Screens
- SCR-CUST-001 (Create Customer)
- SCR-CUST-002 (Edit Customer)

## Preconditions

- The actor is authenticated and authorized to maintain customer data (Administrator).
- Customer Code specified during creation must be unique across all existing customers.

## Capability

The system provides structured customer creation and edit forms (or modals) accessible from the Customer Portfolio View (`SCR-CUST-001` / `SCR-CUST-002`), capturing Customer Code, Customer Name, and Maintenance Contract status. Upon submission, the system validates inputs, verifies code uniqueness, executes the command against `CustomerService`, persists changes to `[customer].[Customers]`, and dispatches appropriate domain events (`CustomerCreated`, `CustomerActivated`, `CustomerInactivated`).

## Business Rules

- A Customer represents an organization, not an individual person (Customer Domain Rule 1).
- Every Customer must have a unique identity and Customer Code (Customer Domain Rule 2).
- Customer identity must remain stable throughout its lifecycle (Customer Domain Rule 4).
- Customer Status must be either `ACTIVE` or `INACTIVE` (Customer Domain Rule 7).
- A Customer may become inactive while preserving all historical references (Customer Domain Rule 5).
- A Customer must not be physically deleted when operational objects reference it (Customer Domain Rule 6).

## Success Result

A Customer Master record is created or updated with authoritative details and active/maintenance status, enabling immediate lookup in request forms and customer overview.

## Failure Conditions

- Customer Code or Customer Name is blank or missing (validation failure).
- Customer Code matches an existing customer record (conflict failure).
- Target Customer ID does not exist during update operation (not found failure).

## Acceptance Criteria

- [ ] Administrator can trigger "Add Customer" modal from Customer Portfolio View (`SCR-CUST-001`).
- [ ] Add Customer form enforces non-empty Customer Code and Customer Name.
- [ ] Submission with duplicate Customer Code displays a clear error message.
- [ ] Administrator can trigger "Edit Customer" modal to update Customer Name and Maintenance Contract status (`SCR-CUST-002`).
- [ ] Deactivating a Customer transitions Status to `INACTIVE` without removing historical data.
- [ ] Successful save immediately refreshes the Customer Portfolio list view.

## Implementation Notes

Executes `CreateCustomerCommand` or `UpdateCustomerCommand` via `CustomerService` and exposes REST API mutation endpoints in `CustomersController.cs`.
