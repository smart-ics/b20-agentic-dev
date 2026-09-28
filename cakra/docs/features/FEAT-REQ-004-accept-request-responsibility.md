# Feature

## Identity

ID: FEAT-REQ-004
Name: Accept Request Responsibility
Type: Command

## Purpose

Enables the designated Request Owner to formally accept operational responsibility for resolving an assigned Request, advancing the request into the active operational lifecycle.

## User Outcome

The Request transitions from captured/evaluation state to `ACTIVE` state, confirming the owner's commitment to manage, coordinate, and resolve the operational demand.

## Traceability

### Domains
- Request

### Scenarios
- SC-REQ-004

### Use Cases
- UC-REQ-004

### User Journeys
- UJ-REQ-004

### Screens
- SCR-REQ-003

## Preconditions

- Target Request exists and is in `CAPTURED` state.
- The actor is the designated Request Owner.

## Capability

The system provides an "Accept Responsibility" action on `SCR-REQ-003: Request Detail`, allows the owner to record optional initial notes or target expectations, transitions the Request lifecycle state to `ACTIVE`, appends an acceptance entry to the State & Audit log, and emits a `RequestStatusChanged` event.

## Business Rules

- Only the currently designated Request Owner can accept operational responsibility (Request Domain Section 4; UC-REQ-004).
- Confirming acceptance transitions the Request lifecycle state from `CAPTURED` to `ACTIVE` (Request Domain Section 9).
- Accepting responsibility establishes accountability for resolving the request, but does not by itself constitute a delivery commitment (Request Domain Rules 10, 11; Manifesto Principle 9).
- The acceptance timestamp and actor identity must be permanently preserved in the Request audit log (Request Domain Rule 14).
- Acceptance generates a system feed event visible on `SCR-FEED-001: Operational Feed` (Post Domain Event 594).

## Success Result

Authoritative Request status becomes `ACTIVE`, acceptance details are appended to the audit trail, and the request is recognized as an active operational work item.

## Failure Conditions

- Actor is not the assigned Request Owner (authority failure).
- Target Request is already in `ACTIVE` or `CLOSED` state (state conflict).

## Acceptance Criteria

- [ ] Accept Responsibility action is enabled on `SCR-REQ-003` only for the assigned Request Owner.
- [ ] Confirming acceptance updates the authoritative Request status to `ACTIVE`.
- [ ] Acceptance event and timestamp are appended to the State & Audit section of `SCR-REQ-003`.
- [ ] Acceptance generates an operational update in the associated feed and `SCR-FEED-001`.
- [ ] Non-owners are prevented from accepting responsibility.

## Implementation Notes

Executes authoritative state transition from `CAPTURED` to `ACTIVE` on the Request Aggregate.
