---
Title: Manage Request Blockers
Code: FEAT-REQ-009
Artifact: FEATURE
Version: 1.0
LastUpdated: 2026-10-15
---

# 1. Purpose

Enable operational teams to record, monitor, clear, and audit blocking impediments on Requests, ensuring work is explicitly suspended when blocked, preventing premature completion, and preserving a permanent history of operational impediments and their resolution.

# 2. Business Outcome

Users gain transparent, authoritative control over operational obstacles. Impediments explicitly pause work and prevent completion until resolved, while full audit trails of who raised each blocker, when, and how it was cleared remain available for organizational learning, post-mortems, and performance reviews.

# 3. Participating Domains

- **Request Domain**: Owns Request lifecycle states, RequestBlocker entities, validation rules preventing completion/resumption while blocked, and audit history.
- **Organization Domain**: Authenticates actors, verifies the assigned Request Owner, and enforces authorization for clearing blockers.

# 4. Trigger

A team member or collaborator identifies an impediment halting progress on an open Request, or the assigned Request Owner resolves an existing blocker and clears it.

# 5. Preconditions

- The target Request exists and is in an open operational state (`ASSIGNED`, `IN_PROGRESS`, or `PAUSED`).
- For recording a blocker: The actor is an authenticated collaborator.
- For clearing a blocker: The blocker is currently active (`IsCleared = false`), and the actor is the assigned Request Owner, an Administrator, or a Manager.

# 6. Operational Flow

1. **Record Blocker**:
   - Actor opens the Request Detail view (`SCR-REQ-003`).
   - Actor submits a blocker description detailing the impediment.
   - The system records the blocker with actor identity and timestamp.
   - If the Request was `IN_PROGRESS`, the system transitions its state to `PAUSED`.
   - The blocker appears in the active blockers list and visual blocker indicators appear on summary screens (`SCR-REQ-004`, `SCR-WP-001`).
2. **Inspect Blockers**:
   - Collaborators inspect active blockers, understanding why progress is halted and what resolution is required.
3. **Clear Blocker**:
   - The impediment is resolved in real life.
   - The assigned Request Owner opens the blocker and submits a clearance action with an optional clearance note.
   - The system marks the blocker as cleared, recording the clearing actor, timestamp, and note.
   - The blocker moves to the historical cleared blockers log.
   - If all blockers are cleared, the Request remains in `PAUSED` state until the owner explicitly resumes work.
4. **Resume / Complete Work**:
   - With 0 active blockers remaining, the owner resumes work (`IN_PROGRESS`).
   - Upon concluding all tasks and sub-tasks with no active blockers, the owner completes the Request (`COMPLETED`).

# 7. Domain Orchestration

- **Request Domain** manages the aggregate boundary, enforcing that adding a blocker suspends active work and that active blockers prevent `Complete` and `StartWork`.
- **Organization Domain** provides actor identity and role verification to ensure only authorized owners/managers can clear blockers.

# 8. Constraints

- Strict immutability: Blockers cannot be edited or deleted once created (Request Domain Rule 26).
- Strict completion guard: Completion is prohibited while any active blocker exists (Request Domain Rule 24).
- Resumption control: Clearing the last blocker does not auto-resume work; manual owner action is required (Request Domain Rule 27).
- Cancellation tolerance: Requests with open blockers may be cancelled without clearing blockers first (Request Domain Rule 28).

# 9. Exceptions

- Attempting to add a blocker to a closed request (`COMPLETED` or `CANCELLED`): System rejects the action.
- Attempting to complete a request with active blockers: System rejects the action.
- Attempting to resume work while active blockers remain: System rejects the action.
- Non-owner attempting to clear a blocker: System rejects the action with an authorization error.

# 10. Acceptance Criteria

- [ ] Any open request in `ASSIGNED`, `IN_PROGRESS`, or `PAUSED` accepts adding a blocker with a non-empty description.
- [ ] Adding a blocker to an `IN_PROGRESS` request automatically transitions it to `PAUSED`.
- [ ] Attempting to call `Complete` on a request with one or more active blockers fails with a clear validation error.
- [ ] Attempting to call `StartWork` on a request with one or more active blockers fails with a clear validation error.
- [ ] The assigned owner can clear an active blocker with an optional clearance explanation.
- [ ] Cleared blockers are permanently retained in history with creation and clearance metadata.
- [ ] Active blockers are prominently displayed on `SCR-REQ-003`, `SCR-REQ-004`, and `SCR-WP-001`.
