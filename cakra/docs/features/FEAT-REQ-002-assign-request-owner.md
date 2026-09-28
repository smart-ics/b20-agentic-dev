# Feature

## Identity

ID: FEAT-REQ-002
Name: Assign / Reassign Request Owner
Type: Command

## Purpose

Enables operational actors to assign initial operational ownership to a captured request, or reassign an active request to a different assignee, establishing and maintaining unambiguous accountability for request assignment and resolution.

## User Outcome

The Request is assigned to a designated Person who assumes operational responsibility for evaluating, executing, or resolving the demand, with ownership history preserved and notified to stakeholders.

## Traceability

### Domains
- Request
- Organization

### Scenarios
- SC-REQ-002
- SC-MGT-001

### Use Cases
- UC-REQ-002
- UC-MGT-001

### User Journeys
- UJ-REQ-002
- UJ-MGT-001

### Screens
- SCR-REQ-001
- SCR-REQ-003

## Preconditions

- Target Request exists and is in a mutable state (`CAPTURED` or `ACTIVE`). Requests in `CLOSED` state cannot be assigned or reassigned.
- The actor has appropriate assignment authority:
  - Implementators have authority to perform initial owner assignment during triage (`UC-REQ-002`).
  - Management has authority to reassign ownership at any stage (`UC-MGT-001`).
- The proposed owner is an active Person recognized in the Organization Domain.

## Capability

The system provides owner assignment and reassignment controls on `SCR-REQ-003: Request Detail` (and quick assignment affordance on `SCR-REQ-001: Request List`), renders a selectable list of eligible organization members, validates the proposed assignee, updates `OwnerPersonId` on the Request aggregate, records the transition in the audit history with timestamp and acting actor, and emits a `RequestOwnerChanged` event.

## Business Rules

- Every active Request must have exactly one Request Owner (Request Domain Rule 2).
- Request Owner must reference an active Person from the Organization Domain (Request Domain Rule 3).
- Operational assignment defines ownership of a specific operational object and is independent of organizational role, Module PIC, or Customer Pimpro accountability (Actor Model Assignment Rules 1, 2, 3, 4; Manifesto Principle 12).
- Reassigning an owner preserves historical assignment records in the Request audit log; historical data must not be overwritten (Request Domain Rule 14; Organization Domain Rule 11).
- Initial assignment from `CAPTURED` state enables the new owner to proceed with evaluation (`UC-REQ-003`).
- Reassignment generates an operational system feed notification on `SCR-FEED-001` (Post Domain Event 595; UI Layout 15-scr-req-003, line 77).
- A closed Request cannot have its owner changed unless explicitly reopened through governance (Request Domain Section 9).

## Success Result

The Request's `OwnerPersonId` is set to the selected Person, the ownership transition is appended to the State & Audit log, and the request appears immediately in the new owner's assigned queue (`SCR-REQ-005`).

## Failure Conditions

- Target Request is in `CLOSED` lifecycle state (state conflict).
- Selected assignee is not an active Person in the Organization Domain (validation failure).
- Actor lacks authorization to assign or reassign requests (authority failure).
- Proposed assignee is identical to the current owner (rejected as redundant no-op).

## Acceptance Criteria

- [ ] Initial assignment action is accessible from `SCR-REQ-001` and `SCR-REQ-003` by Implementators.
- [ ] Reassignment action is accessible from `SCR-REQ-003` by Management.
- [ ] Owner selection list is populated only with active Persons from the Organization Domain.
- [ ] Confirming assignment updates `Request.OwnerPersonId` in the authoritative record.
- [ ] Ownership change is recorded in the Request State & Audit section with timestamp and actor name.
- [ ] Assigned request appears immediately on `SCR-REQ-005: My Assigned Requests` for the new owner.
- [ ] Assignment attempts on closed requests are rejected.

## Implementation Notes

Unifies initial triage assignment (`UC-REQ-002`) and managerial reassignment (`UC-MGT-001`) into a single state mutation on the Request Aggregate with role-differentiated validation.
