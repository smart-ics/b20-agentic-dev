# Feature

## Identity

ID: FEAT-REQ-007
Name: Request Management Decision
Type: Workflow

## Purpose

Enables the Request Owner to elevate an active Request requiring an organizational policy determination, risk acceptance, or resource conflict resolution to Management.

## User Outcome

The Request is marked as awaiting a management decision with structured options and impact analysis documented, prompting leadership review and timely resolution.

## Traceability

### Domains
- Request
- Organization

### Scenarios
- SC-REQ-007

### Use Cases
- UC-REQ-007

### User Journeys
- UJ-REQ-007

### Screens
- SCR-REQ-003

## Preconditions

- Target Request exists and is in `ACTIVE` state.
- The actor is the designated Request Owner.

## Capability

The system provides a "Request Management Decision" action on `SCR-REQ-003: Request Detail`, capturing the core decision question, situational summary, options considered, and operational impact analysis. Upon submission, the system sets the Request condition to `Awaiting Management Decision`, appends the decision context to the audit trail, and alerts Management via `SCR-FEED-001`.

## Business Rules

- Operational decisions must be grounded in observable facts and supported by clear alternatives (Manifesto Principles 4, 6).
- "Awaiting Decision" is an operational condition within `ACTIVE` lifecycle state, not an independent lifecycle state (Request Domain Section 9.1, 9.2).
- Management possesses authority to review and decide upon elevated operational questions (Request Domain Section 4 - Manager; Actor Model Line 43).
- The decision submission must state the specific policy question, options, and recommended resolution (UJ-REQ-007 Step 2).
- Elevating a request for decision does not terminate or reassign the current Request Owner's assignment (Actor Model Assignment Rule 7).

## Success Result

The Request condition updates to "Awaiting Management Decision", the decision docket is logged in the request audit record, and an alert is broadcast across the feed to Management.

## Failure Conditions

- Decision question, situation summary, or options are blank (validation failure).
- Actor is not the assigned Request Owner (authority failure).
- Target Request is in `CLOSED` state (state conflict).

## Acceptance Criteria

- [ ] Decision request action is accessible on `SCR-REQ-003` for the Request Owner.
- [ ] System validates that decision question, options, and impact summary are provided.
- [ ] Request displays an "Awaiting Decision" condition badge in `SCR-REQ-003` and `SCR-REQ-001`.
- [ ] Decision request details are appended to the Request audit trail.
- [ ] Action emits a notification visible to Management on `SCR-FEED-001`.

## Implementation Notes

Records a decision request condition and docket within the Request Aggregate without mutating the core lifecycle status.
