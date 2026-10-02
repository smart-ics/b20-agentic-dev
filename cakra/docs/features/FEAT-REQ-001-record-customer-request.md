# Feature

## Identity

ID: FEAT-REQ-001
Name: Record Customer Request
Type: Command

## Purpose

Enables operational actors to capture and formalize a customer operational demand, issue, or need into an authoritative structured Request record.

## User Outcome

The operational demand is formally recorded in the system in `CAPTURED` state, anchored to relevant customer and product context, and queued for ownership triage.

## Traceability

### Domains
- Request
- Customer

### Scenarios
- SC-REQ-001

### Use Cases
- UC-REQ-001

### User Journeys
- UJ-REQ-001

### Screens
- SCR-REQ-002
- SCR-FEED-001 (Modal affordance: CreateRequestModal)

## Preconditions

- The actor is authenticated and authorized to submit requests (Implementator, Management).
- Referenced Customer or Product (if specified) exist as valid entities in Customer and Product domains.

## Capability

The system provides a structured request creation form on `SCR-REQ-002: Create Request` as well as a direct modal dialog (`CreateRequestModal`) accessible on `SCR-FEED-001: Operational Feed` capturing Title, Detailed Description, Request Type (e.g., Bug, Feature, Support), Customer selection, Product selection, and optional Work Package. Upon submission, the system validates the input, generates a unique Request ID, sets initial status to `CAPTURED`, creates an associated system post in the Operational Feed, and navigates to `SCR-REQ-003: Request Detail` (or immediately refreshes the feed stream on `SCR-FEED-001` with a direct link in a success alert banner).

## Business Rules

- Every Request must have a unique identity (Request Domain Rule 1).
- A Request must contain sufficient information (non-empty Title and Description) to understand what is being requested (Request Domain Rule 4).
- Initial lifecycle status of a newly recorded request is `CAPTURED` (Request Domain Section 9).
- A Request may exist without a Customer, without a Product, and without a Work Package (Request Domain Rules 5, 6, 7).
- A Request is an operational demand and does not constitute a commitment or guarantee that ICS will execute the work (Request Domain Rules 10, 11; Manifesto Principle 9).
- Submitting the form emits a `RequestCreated` event and automatically generates a system post visible in `SCR-FEED-001: Operational Feed` (Post Domain Rule 7, 594; UI Layout 14-scr-req-002 line 48).
- Screen mapping includes `SCR-REQ-002: Create Request` (dedicated form) and `SCR-FEED-001: Operational Feed` via `CreateRequestModal` (UI Layout 14-scr-req-002, 10-scr-feed-001; Navigation req-nav, feed-nav; CR-003).

## Success Result

A new Request aggregate is created with status `CAPTURED`, a unique Request ID, and optional context links, and a corresponding system post is broadcast to the Operational Feed.

## Failure Conditions

- Title, Description, or Request Type is blank or missing (validation failure).
- Selected Customer, Product, or Work Package ID is invalid or not found (referential integrity failure).
- Actor lacks authorization to record requests (authority failure).

## Acceptance Criteria

- [ ] Creation form is accessible on `SCR-REQ-002: Create Request` via `SCR-REQ-001` or Global Quick Action.
- [ ] Creation modal is accessible on `SCR-FEED-001: Operational Feed` via header '+ Create Request' action.
- [ ] Form enforces validation on required fields (Title, Description, Type).
- [ ] Actor can associate active Customer and Product entities from dropdowns.
- [ ] Internal requests can be recorded without selecting a Customer.
- [ ] Successful submission creates a Request with lifecycle status `CAPTURED`.
- [ ] Submission generates a system-generated post on `SCR-FEED-001: Operational Feed`.
- [ ] Successful submission navigates to `SCR-REQ-003: Request Detail` or provides a direct link in the feed success banner.

## Implementation Notes

Creates a new Request Aggregate and emits a `RequestCreated` domain event to trigger feed integration.
