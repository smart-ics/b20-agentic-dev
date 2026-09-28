# Feature

## Identity

ID: FEAT-REQ-005
Name: Reject Request
Type: Command

## Purpose

Enables the Request Owner to decline responsibility for a Request during evaluation, terminating active work and closing the request with a mandatory documented justification.

## User Outcome

The Request is formally declined without execution, its lifecycle status transitions to `CLOSED`, and the rejection outcome and justification are permanently preserved for organizational learning and audit.

## Traceability

### Domains
- Request

### Scenarios
- SC-REQ-005

### Use Cases
- UC-REQ-005

### User Journeys
- UJ-REQ-005

### Screens
- SCR-REQ-003

## Preconditions

- Target Request exists and is in `CAPTURED` or `ACTIVE` evaluation state.
- The actor is the assigned Request Owner.
- An explanatory rejection reason is provided.

## Capability

The system provides a "Reject Request" dialog on `SCR-REQ-003: Request Detail`, requires entry of an explanatory reason (with optional reference to an existing duplicate request), transitions the Request lifecycle state to `CLOSED`, sets Resolution outcome to `REJECTED`, records `ResolvedBy`, `ResolvedAt`, and description in the Resolution aggregate, and emits a `RequestRejected` event.

## Business Rules

- A Request may be rejected without being executed (Request Domain Rule 12).
- Rejecting a Request requires recording a meaningful explanatory reason; blank justifications are prohibited (Request Domain Rule 15; UJ-REQ-005 Step 3).
- Rejection transitions the Request directly to lifecycle state `CLOSED` with Resolution outcome `REJECTED` (Request Domain Section 9, Rule 15).
- A closed/rejected Request remains historically retrievable in search and reports (Request Domain Rule 16).
- If rejected as a duplicate, the owner can reference the existing Request ID (UJ-REQ-005 Alternative Paths).
- Rejection emits a `RequestRejected` event and publishes a system update on `SCR-FEED-001` (Post Domain Event 597).

## Success Result

Authoritative Request status becomes `CLOSED`, Resolution outcome is recorded as `REJECTED` with the explanatory justification, and the request is removed from active queues while preserved in history.

## Failure Conditions

- Rejection reason is empty or whitespace-only (validation failure).
- Actor is not the assigned Request Owner (authority failure).
- Target Request is already in `CLOSED` state (state conflict).

## Acceptance Criteria

- [ ] Reject action is available on `SCR-REQ-003: Request Detail` for the assigned Request Owner.
- [ ] System strictly enforces entry of a non-empty rejection reason before confirming rejection.
- [ ] Confirming rejection transitions Request lifecycle status to `CLOSED`.
- [ ] Resolution entity is populated with outcome `REJECTED`, author Person ID, and timestamp.
- [ ] Rejected request no longer appears in active queues (`SCR-REQ-005`) but remains retrievable in `SCR-REQ-004`.
- [ ] Rejection generates a system event post on the Operational Feed.

## Implementation Notes

Populates the Resolution component within the Request Aggregate and transitions lifecycle status to `CLOSED`.
