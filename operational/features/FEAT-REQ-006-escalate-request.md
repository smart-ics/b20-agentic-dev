# Feature

## Identity

ID: FEAT-REQ-006
Name: Escalate Request
Type: Workflow

## Purpose

Enables the Request Owner to escalate an active Request when resolving the demand exceeds their operational authority, technical capability, or available resources.

## User Outcome

The Request is highlighted as an operational exception requiring higher-level attention, prompting timely managerial review and intervention across the feed and oversight screens.

## Traceability

### Domains
- Request

### Scenarios
- SC-REQ-006

### Use Cases
- UC-REQ-006

### User Journeys
- UJ-REQ-006

### Screens
- SCR-REQ-003

## Preconditions

- Target Request exists and is in `ACTIVE` state.
- The actor is the designated Request Owner.

## Capability

The system provides an "Escalate Request" action on `SCR-REQ-003: Request Detail`, collects the escalation reason and the specific nature of assistance required, updates the Request's operational condition to `Escalated`, logs the escalation event in the audit trail, and generates an exception post on `SCR-FEED-001: Operational Feed`.

## Business Rules

- Escalation is an operational condition within the `ACTIVE` state, not an independent lifecycle replacement (Request Domain Section 9.1, 9.2).
- The Request Owner must document the specific reason for escalation and describe the required assistance (UJ-REQ-006 Step 2).
- Escalating a request does not remove or unassign the current Request Owner; ownership remains intact until explicit reassignment occurs (Actor Model Assignment Rule 7; Manifesto Principle 12).
- Escalations must surface as operational exceptions in Feed observation (`UC-AWR-003`) and Management oversight views (`SCR-MGT-001`, `SCR-MGT-003`) (Manifesto Principle 5, 13).

## Success Result

The Request displays the active condition "Escalated", escalation details and timestamp are recorded in the State & Audit log, and an operational exception notification is broadcast to Management on the feed.

## Failure Conditions

- Escalation reason or required assistance is blank (validation failure).
- Actor is not the designated Request Owner (authority failure).
- Target Request is in `CLOSED` state (state conflict).

## Acceptance Criteria

- [ ] Escalate action is accessible on `SCR-REQ-003` for the assigned Request Owner.
- [ ] System requires entry of a non-empty escalation reason and assistance description.
- [ ] Request record displays a visible "Escalated" status badge.
- [ ] Escalation event is recorded in the Request State & Audit section with timestamp and owner identity.
- [ ] Escalation generates an operational exception post in `SCR-FEED-001`.
- [ ] The current Request Owner remains assigned following escalation.

## Implementation Notes

Updates the operational condition attribute within the Request Aggregate and emits an escalation event.
