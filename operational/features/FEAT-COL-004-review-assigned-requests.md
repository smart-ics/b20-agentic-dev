# Feature

## Identity

ID: FEAT-COL-004
Name: Review Assigned Requests
Type: Query

## Purpose

Provides a personalized operational view for an implementator or request owner displaying only the requests currently assigned to them, organized by status, priority, and urgency.

## User Outcome

The actor maintains complete visibility over their personal operational responsibilities, prioritizes immediate tasks, spots items requiring attention, and navigates directly to work on them.

## Traceability

### Domains
- Request
- Organization

### Scenarios
- SC-COL-004

### Use Cases
- UC-COL-004

### User Journeys
- UJ-COL-004

### Screens
- SCR-REQ-005

## Preconditions

- The actor is authenticated and represents a Person recognized in the Organization Domain.
- One or more Requests may be assigned to the actor (`OwnerPersonId` matches actor).

## Capability

The system provides a dedicated personalized screen on `SCR-REQ-005: My Assigned Requests` featuring a Workload Summary (active assigned count, needs attention/due soon count) and an Assigned Request List (card/grid queue displaying Request ID, Title, Priority, Due Date, Customer, and Status). The actor can review, sort, and select any assigned request, transitioning directly to `SCR-REQ-003: Request Detail` to execute work.

## Business Rules

- The assigned queue displays only Requests where `OwnerPersonId` equals the authenticated actor's `PersonId` (Actor Model Section Operational Assignment, Lines 86-117).
- Closed requests are excluded from the default active queue and workload summary (Request Domain Section 9).
- Workload summary metrics (Active count, Needs Attention count) are dynamically derived from the actor's active assigned requests (Manifesto Principle 3, 12).
- Operational assignment reflects work execution responsibility and is independent of organizational role or Module PIC accountability (Actor Model Assignment Rules 1, 3, 5).
- Selecting an assigned request navigates to `SCR-REQ-003: Request Detail` (UI Layout 17-scr-req-005; Navigation coll-nav).

## Success Result

The actor views their complete personal operational queue with accurate summary counts and selects their next priority request to execute.

## Failure Conditions

- Actor is unauthenticated or not recognized as an organizational Person (authority failure).
- Actor has zero active assigned requests (system displays an empty state indicating no active responsibilities).

## Acceptance Criteria

- [ ] Screen `SCR-REQ-005: My Assigned Requests` is accessible via Global Navigation (`Requests > My Assigned`).
- [ ] Workload summary displays correct count of active assigned requests and items requiring attention.
- [ ] Queue lists only requests assigned to the logged-in actor.
- [ ] Each queue entry displays Request ID, Title, Status, Priority, and Customer.
- [ ] Selecting an item navigates to `SCR-REQ-003: Request Detail`.
- [ ] Closed requests are not displayed in the active workload queue.

## Implementation Notes

Filtered projection of the Request Aggregate filtered by authenticated Person identity.
