# Feature

## Identity

ID: FEAT-REQ-002
Name: Assign Request Owner
Type: Command

## Purpose

Enables the actor to assign request owner to achieve operational outcomes.

## User Outcome

The user can successfully execute the operational intent defined in the use case.

## Traceability

### Domains
- Request

### Scenarios
- SC-REQ-002

### Use Cases
- UC-REQ-002

### User Journeys
- UJ-REQ-002

### Screens
- SCR-REQ-003

## Preconditions

The actor is authenticated and authorized to perform this action. System state allows the execution of this capability.

## Capability

The system provides the necessary capability to assign request owner, facilitating the interaction required by the actor.

## Business Rules

- Execution must comply with constraints defined in the related domain and operational scenario.
- Proper authorization must be enforced.

## Success Result

The authoritative state of the system is updated to reflect the successful execution of assign request owner.

## Failure Conditions

- Validation failures (e.g., missing required input).
- Authority failures (actor lacks permission).
- State conflicts (action invalid in current state).

## Acceptance Criteria

- [ ] Capability is accessible from SCR-REQ-003
- [ ] Supports UC-REQ-002 outcome
- [ ] Successfully updates or retrieves necessary state
- [ ] Handles failure conditions gracefully

## Implementation Notes

None.
