# Feature

## Identity

ID: FEAT-REQ-008
Name: Review Request Completion
Type: Workflow

## Purpose

Enables an authorized Implementator to inspect completed work and resolution evidence on a Request, and formally accept the resolution (closing the request) or reject it (returning it to the owner for rework).

## User Outcome

Completed requests are systematically verified against original operational demands; satisfactory work is formally closed, while deficient resolutions are returned to the owner with concrete rework feedback.

## Traceability

### Domains
- Request

### Scenarios
- SC-REQ-008

### Use Cases
- UC-REQ-008

### User Journeys
- UJ-REQ-008

### Screens
- SCR-REQ-001
- SCR-REQ-003

## Preconditions

- Target Request exists, is in `ACTIVE` state, and the Request Owner has reported work completion with proposed resolution details.
- The actor holds the `Implementator` role in the Organization Domain.

## Capability

The system provides completion review controls on `SCR-REQ-003: Request Detail` (and filterable completed queues on `SCR-REQ-001: Request List`), displays the original demand alongside recorded resolution notes and evidence, and provides two decisive operational actions: "Accept Resolution" and "Request Rework".

## Business Rules

- Closing a Request must record its meaningful outcome (`ResolvedBy`, `ResolvedAt`, `Outcome`, `Description`) (Request Domain Rules 15, Resolution Section 5).
- Accepting the resolution transitions Request lifecycle status to `CLOSED` with outcome `RESOLVED` (Request Domain Section 9).
- Rejecting the resolution maintains Request status as `ACTIVE` and routes the request back to the Request Owner with mandatory rework feedback appended to the audit history (UC-REQ-008; UJ-REQ-008 Alternative Path).
- A closed Request remains historically retrievable and cannot undergo further active workflow changes without reopening (Request Domain Rule 16).
- The review outcome generates a system feed post on `SCR-FEED-001: Operational Feed` (Post Domain Events 597).

## Success Result

If resolution is accepted: Request lifecycle status becomes `CLOSED` with Resolution outcome `RESOLVED`, and active work ends.
If rework is requested: Request remains in `ACTIVE` status with rework feedback logged, notifying the Request Owner.

## Failure Conditions

- Actor does not hold the `Implementator` role (authority failure).
- Target Request is not in a completion-pending state (state conflict).
- Rework requested without providing mandatory rework feedback (validation failure).

## Acceptance Criteria

- [ ] Completion review controls are available on `SCR-REQ-003: Request Detail` for Implementators.
- [ ] Implementator can inspect original demand details and resolution evidence side by side.
- [ ] Confirming "Accept Resolution" transitions Request status to `CLOSED` with outcome `RESOLVED`.
- [ ] Confirming "Request Rework" requires explanatory feedback and keeps Request status `ACTIVE`.
- [ ] Resolution details (`ResolvedBy`, `ResolvedAt`, outcome, description) are permanently preserved in the Request audit log.
- [ ] Completed requests awaiting review can be filtered on `SCR-REQ-001: Request List`.

## Implementation Notes

Finalizes the Resolution component within the Request Aggregate and handles the terminal transition to `CLOSED` or continuation in `ACTIVE`.
