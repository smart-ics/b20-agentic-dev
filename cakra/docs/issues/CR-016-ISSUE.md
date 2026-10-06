# ISSUE

## Metadata

ID: CR-016
Type: CHANGE-REQUEST
Status: OPEN
Title: Simplify Request Lifecycle

## Source

Reported By: User
Reported Date: 2026-10-06

## Description

The user requested simplifying the Request lifecycle by removing dedicated workflow features for Escalation and Management Decision, eliminating intermediate triage ceremony (Evaluate, Accept, and Reject), adding support for pausing/suspending active work, and focusing the Request module on essential direct actions.

Discussions, blockers, questions, and requests for assistance will be handled through direct comments on the request rather than separate escalation dockets. Management actions will directly update the request (e.g. reassigning, pausing, or cancelling) rather than recording separate decision records.

## Desired Outcome

1. **Simplified Lifecycle State Machine**:
   - The Request lifecycle consists of four active states and two terminal states:
     - `CAPTURED`: Newly recorded request, unassigned.
     - `ASSIGNED`: Owner designated, awaiting work initiation.
     - `IN_PROGRESS`: Owner is actively working on the request.
     - `PAUSED`: Work suspended due to priority changes, blockers, or management direction.
     - `COMPLETED`: Terminal state indicating verified completion of work.
     - `CANCELLED`: Terminal state for invalid, duplicate, or discarded requests.
   - The legacy states `EVALUATING`, `ACCEPTED`, `REJECTED`, and `ESCALATED` are retired.

2. **Removal of Intermediate Triage Ceremony**:
   - The formal `Evaluate` and `Accept` stages are removed. Evaluation is performed by the assigned owner as part of direct work; acceptance is implicit when the owner starts work.
   - The formal `Reject` action during evaluation is replaced by the general `Cancel Request` action.

3. **Work Suspension ("Stop / Pause Work")**:
   - Users can pause active work on a request, transitioning it from `IN_PROGRESS` to `PAUSED`.
   - The action accepts an optional pause reason/note recorded in the state change history audit log.
   - Both the assigned owner and managers/team leads/admins have authority to trigger pause.
   - Work is resumed when the assigned owner triggers "Start Work", transitioning `PAUSED` back to `IN_PROGRESS`.

4. **Direct Work Initiation ("Start Work")**:
   - Starting work transitions the request from `ASSIGNED` or `PAUSED` into `IN_PROGRESS`.
   - Authority to trigger "Start Work" is strictly restricted to the assigned owner, ensuring personal accountability and commitment.

5. **Owner Assignment and Reassignment**:
   - Initial assignment transitions `CAPTURED` -> `ASSIGNED`.
   - Reassigning an active request currently in `IN_PROGRESS` or `PAUSED` resets its status to `ASSIGNED`, requiring the new owner to explicitly initiate work via "Start Work".
   - Reassignment can be performed from any non-terminal state by managers, team leads, admins, or the current owner.

6. **Cancellation ("Cancel Request")**:
   - A request can be cancelled from any non-terminal state (`CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`), transitioning it to `CANCELLED`.
   - A mandatory cancellation reason is required and recorded in the resolution record.
   - Unfinished subtasks do not block cancellation.

7. **Removal of Escalation and Management Decision Dockets**:
   - The separate `Escalate`, `RequestManagementDecision`, and `ApplyManagementDecision` actions and their dedicated forms/dockets are removed.
   - Blockers, questions, and requests for assistance are communicated through comments and activity history.
   - Management intervenes via direct actions (reassign, pause, cancel) and comments.

8. **Embedded Comments & Discussion on Request Detail (`SCR-REQ-003`)**:
   - A dedicated Comments & Discussion section is embedded directly on `SCR-REQ-003: Request Detail`, integrated with the operational post and feed.

9. **Data Migration & Analytics Adaptation**:
   - Existing database records are migrated:
     - `ESCALATED` -> `PAUSED`
     - `EVALUATING` & `ACCEPTED` -> `ASSIGNED`
     - `REJECTED` -> `CANCELLED`
   - Analytics models and views replace `Escalated` metrics with `Paused` metrics (tracking paused requests as suspended work).

## Current Situation

1. The Request workflow enforces rigid multistep triage: `CAPTURED` -> `EVALUATING` (via Assign Owner) -> `ACCEPTED` (via Accept Responsibility) -> `IN_PROGRESS` (via Start Progress), adding unnecessary administrative overhead.
2. The system contains dedicated `Escalate` actions and an `ESCALATED` state requiring separate escalation justifications and dockets when blockers arise.
3. The system contains dedicated `ManagementDecisionRequested` and `ApplyManagementDecision` mechanisms requiring separate decision dockets, notes, and targets.
4. The only transition out of `IN_PROGRESS` is `Complete`, offering no mechanism to pause or suspend active work when priorities change or work is blocked.
5. The Request Detail view (`SCR-REQ-003`) lacks embedded comments and discussion, requiring users to navigate to the feed to converse on a request.
6. Operational analytics track blockers strictly via `ESCALATED` status (`EscalatedCount`, `IsBlocked`).

## Evidence

- User Request:
  - "Simplify Request Lifecycle"
  - "Remove Escalation: Escalation should not be a separate workflow action. Any discussion, blocker, or request for assistance can be handled through comments on the request."
  - "Remove Management Decision: Management actions should directly update the request instead of creating a separate decision record."
  - "Support Work Suspension: Introduce a Stop / Pause Work action that moves the request out of IN_PROGRESS without marking it as completed."
  - "The Request module should focus on only a few essential lifecycle actions: Assign / Reassign Owner, Start Work, Stop / Pause Work, Complete Request."
- Alignment Interview (/grill-me) decisions:
  - Triage streamlining: Remove Evaluate, Accept, and Reject states. Target states are `CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`, `COMPLETED`, `CANCELLED`.
  - Pause Work rules: Optional pause reason/note recorded in audit log; authorized for both Owner and Manager/Lead/Admin; resumed via "Start Work" (`PAUSED` -> `IN_PROGRESS`).
  - Reassignment rule: Reassigning an owner from `IN_PROGRESS` or `PAUSED` resets status to `ASSIGNED`, requiring the new owner to explicitly click "Start Work".
  - Cancellation rule: Allowed from any non-terminal state (`CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`), requiring an explicit reason, without blocking on unfinished subtasks.
  - Start Work authority: Strictly the assigned owner to ensure accountability and personal commitment.
  - Collaboration: Embed a dedicated Comments & Discussion section directly on `SCR-REQ-003: Request Detail`, linked to the request's operational post.
  - Data Migration: Migrate `ESCALATED` -> `PAUSED`, `EVALUATING` & `ACCEPTED` -> `ASSIGNED`, and `REJECTED` -> `CANCELLED` via database migration; update analytics to track `Paused` requests.
- Related files:
  - Request Aggregate Root: [Request.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs)
  - Request Status Enum: [RequestStatus.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/RequestStatus.cs)
  - Request Commands: [RequestCommands.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs)
  - Request Service: [RequestService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs)
  - Request API Controller: [RequestsController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/RequestsController.cs)
  - Request Detail View: [RequestDetailView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue)
  - Management Analytics Service: [ManagementAnalyticsService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Analytics/Services/ManagementAnalyticsService.cs)

## Notes

This artifact formally captures the intake request for Simplify Request Lifecycle (CR-016) according to Knowledge-Centric SDLC standards. In accordance with the Knowledge Lifecycle, detailed analysis, feasibility assessment, domain and feature updates, architecture design, and implementation planning belong to downstream stages.
