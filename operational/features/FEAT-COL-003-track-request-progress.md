# Feature

## Identity

ID: FEAT-COL-003
Name: Track Request Progress
Type: Query

## Purpose

Provides comprehensive visibility into the current operational progress, milestone transitions, ownership status, and chronological activity history of an individual Request.

## User Outcome

The actor monitors the real-time progression of a Request, detects stalled steps or blockers, verifies current ownership, and determines appropriate follow-up actions.

## Traceability

### Domains
- Request

### Scenarios
- SC-COL-003

### Use Cases
- UC-COL-003

### User Journeys
- UJ-COL-003

### Screens
- SCR-REQ-001
- SCR-REQ-003

## Preconditions

- The Request exists in the Request Domain.
- The actor is authenticated and authorized to view Request records.

## Capability

The system surfaces real-time status indicators in `SCR-REQ-001: Request List` and renders a comprehensive chronological progression timeline, audit log, and activity stream on `SCR-REQ-003: Request Detail` (within the State & Audit and Associated Feed Stream sections). The actor can inspect when and by whom the request was created, assigned, evaluated, transitioned, escalated, or completed.

## Business Rules

- Authoritative current Request state is the sole source of truth for progress; arbitrary progress percentages are prohibited (Manifesto Principles 2, 3; Request Domain Section 9).
- Progress tracking must display the current lifecycle state (`CAPTURED`, `ACTIVE`, `CLOSED`), any active operational condition (e.g., Escalated, Awaiting Decision, Needs Attention), the assigned Request Owner, and timestamps (Request Domain Section 9; UI Layout 15-scr-req-003).
- The State & Audit history must chronologically record significant state and ownership transitions with timestamps and acting individuals (Request Domain Rule 14).
- The chronological activity stream reflects all associated notes, discussions, and system-generated events linked to the Request.

## Success Result

The actor views an accurate, chronological record of the Request's progression, current ownership, and active condition, allowing informed decisions on whether progress aligns with operational expectations.

## Failure Conditions

- Specified Request ID does not exist in the system (not found failure).
- Actor lacks permission to view request progress (authority failure).

## Acceptance Criteria

- [ ] Current status and assigned owner are prominently displayed on `SCR-REQ-001` and `SCR-REQ-003`.
- [ ] `SCR-REQ-003: Request Detail` displays the complete chronological log of state changes, assignments, and timestamps.
- [ ] Operational conditions (e.g., Escalated, Awaiting Decision) are surfaced as visual status badges.
- [ ] Chronological activity stream displays all associated comments, notes, and system events.
- [ ] Inspecting progress is read-only and does not mutate Request state.

## Implementation Notes

Derives progress visualization strictly from the Request Aggregate lifecycle and audit log.
