# ISSUE

## Metadata

ID: CR-032
Type: CHANGE-REQUEST
Status: OPEN
Title: Add Blocker Capability and History Tracking to Requests

## Source

Reported By: User
Reported Date: 2026-10-15

## Description

Operational requests frequently encounter external dependencies, technical impediments, customer clarifications, or management holds that prevent the assigned owner from progressing or completing work. Currently, there is no structured mechanism on requests to record distinct blockers, track when and why work is impeded, or maintain an audit trail of how blockers were resolved.

Users require the capability to record blockers directly on a request. Each blocker must capture the blocking impediment, prevent completion of the request while unresolved, and be clearable when resolved so that work can safely resume and reach completion. Furthermore, cleared blockers must be permanently retained as historical records for accountability and operational visibility.

## Desired Outcome

1. **Structured Blocker Recording**:
   - Any collaborator or team member can log an impediment as an active blocker on an open request (in `ASSIGNED`, `IN_PROGRESS`, or `PAUSED` state) with a mandatory description/reason.
   - Multiple blockers can be recorded over the lifecycle of a request.
   - Blockers are strictly append-and-clear (immutable audit trail; cannot be edited or deleted once created).

2. **Lifecycle and Completion Safeguards**:
   - Recording a blocker automatically transitions an active request to `PAUSED` state.
   - An active (uncleared) blocker strictly blocks the request from transitioning to `COMPLETED`.
   - An active blocker strictly prevents the assigned owner from resuming active work (`IN_PROGRESS`).
   - If a request is cancelled (`CANCELLED`), cancellation is permitted even with open blockers, and all blockers are preserved as historical records.

3. **Blocker Clearance and Work Resumption**:
   - Only the assigned owner (or manager / administrator) can clear an active blocker, providing an optional clearance explanation/note.
   - Clearing all active blockers leaves the request in `PAUSED` state; the owner must explicitly resume work to transition back to `IN_PROGRESS`.

4. **Historical Audit Trail**:
   - The system permanently retains full history for all blockers, recording creation actor, creation timestamp, reason, clearance actor, clearance timestamp, and clearance resolution notes.

5. **Operational Visibility**:
   - `SCR-REQ-003` (Request Detail): Dedicated panel displaying active blockers, actions to add and clear blockers, and an expandable or dedicated history of cleared blockers.
   - `SCR-REQ-004` (My Requests) and `SCR-WP-001` (Work Package Scope): Distinct visual blocker badge / indicator and blocker reason displayed on cards and list items when active blockers exist.

## Current Situation

1. The `Request` aggregate root (`Request.cs`) supports pausing work (`PauseWork`) via an ephemeral text note attached to `RequestAssignment`, but does not support multiple concurrent blockers or first-class blocker entities.
2. The `Complete` method in `Request.cs` only validates that checklist sub-tasks are complete; it has no awareness or validation of open blockers.
3. The `StartWork` method in `Request.cs` allows transitioning from `PAUSED` to `IN_PROGRESS` without checking whether blocking conditions remain active.
4. When work is paused, previously entered pause notes are overwritten or lost in historical state transitions without structured clearance reasons or resolution timestamps.
5. In the frontend, `RequestDetailView.vue` (`SCR-REQ-003`), `MyRequestsView.vue` (`SCR-REQ-004`), and `WorkPackageView.vue` (`SCR-WP-001`) only inspect single pause assignment notes rather than rendering structured blocker entries and resolution logs.

## Evidence

- Requirements and alignment established via `/grill-me` intake interview on 2026-10-15.
- Codebase references:
  - Aggregate Root: [Request.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs#L316-L450)
  - Status Enum: [RequestStatus.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/RequestStatus.cs#L17-L19)
  - Work Package Telemetry Blocker Note: [WorkPackageDto.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Models/WorkPackageDto.cs#L230-L241)
  - Request Detail Screen: [RequestDetailView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue)
  - My Requests Screen: [MyRequestsView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/MyRequestsView.vue)

## Notes

- Intake artifact is solution-neutral. Database schema design, API endpoints, domain event structures, and telemetry calculation updates will be analyzed in downstream feasibility and architecture phases.
