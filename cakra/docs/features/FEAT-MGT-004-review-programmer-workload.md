# Feature

## Identity

ID: FEAT-MGT-004
Name: Review Programmer Workload
Type: Query

## Purpose

Provides management with a real-time overview of active request assignments distributed across all programmers, identifying team capacity and bottleneck imbalances.

## User Outcome

Management inspects the active workload of the development team, spots overloaded individuals, and determines whether new requests can be assigned or active requests need rebalancing.

## Traceability

### Domains
- Request
- Organization

### Scenarios
- SC-MGT-004

### Use Cases
- UC-MGT-004

### User Journeys
- UJ-MGT-004

### Screens
- SCR-MGT-003

## Preconditions

- The actor holds the `Management` organizational role.
- Active team members exist with development or implementation roles in the Organization Domain.

## Capability

The system displays a team distribution summary on `SCR-MGT-003: Programmer Workload Review` showing each programmer alongside their active request count and workload alert indicators. Selecting an individual displays their detailed active queue (Request ID, Title, Status, Priority, Customer) and allows management to navigate directly to `SCR-REQ-003: Request Detail` to initiate reassignment or investigate blockers.

## Business Rules

- Operational assignment defines current ownership of specific requests and is distinct from organizational responsibility or Module PIC accountability (Actor Model Section Core Principle, Lines 86-117).
- Active workload calculations consider only Requests in `ACTIVE` or `CAPTURED` lifecycle states where `OwnerPersonId` matches the individual (Request Domain Section 5, 9).
- Closed requests are excluded from active workload tallies.
- Capacity assessment is a derived projection computed from active request ownership facts (Manifesto Principles 7, 16).
- Selecting an active request navigates to `SCR-REQ-003: Request Detail` (UI Layout 20-scr-mgt-003, lines 45-48).

## Success Result

Management views real-time active assignment counts across all team members and drills down into individual queues to make informed workload balancing decisions.

## Failure Conditions

- Actor lacks the `Management` organizational role (authority failure).
- Selected programmer has zero active assignments (system displays an empty active queue for that individual).

## Acceptance Criteria

- [ ] Screen `SCR-MGT-003` is accessible to authorized Management actors.
- [ ] Team overview displays active request counts for all active programmers.
- [ ] Selecting a programmer populates their active queue with Request ID, Title, Status, Priority, and Customer.
- [ ] Closed requests are excluded from workload metrics and queue lists.
- [ ] Selecting an active request navigates to `SCR-REQ-003: Request Detail` to support reassignment.
- [ ] Workload review operations produce zero mutations on domain entities.

## Implementation Notes

Real-time query projection over active Request Aggregate instances grouped by assigned Person identity.
