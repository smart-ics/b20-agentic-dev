# Feature

## Identity

ID: FEAT-FCOL-003
Name: Create Operational Post
Type: Command
Status: REVOKED (Decommissioned via CR-001)

## Purpose

[REVOKED per Issue CR-001] Formerly enabled actors to compose and publish a new human-authored operational post. Direct user creation of operational posts is decommissioned. Operational posts in the Operational Feed are generated solely from Request creation (and associated request lifecycle events).

## User Outcome

[REVOKED per Issue CR-001] Users no longer directly author operational posts. Operational awareness is driven by system-generated posts derived from authoritative Request creation and lifecycle events.

## Traceability

### Issue
- CR-001 (Revocation of direct creation of operational posts)

### Domains
- Post (Updated: direct creation revoked)
- Request

### Scenarios
- SC-FCOL-003 (Decommissioned)

### Use Cases
- UC-FCOL-003 (Decommissioned)

### User Journeys
- UJ-FCOL-003 (Decommissioned)

### Screens
- SCR-POST-002 (Decommissioned)

## Preconditions

- None. Feature is decommissioned and direct invocation is disallowed.

## Capability

[DECOMMISSIONED] The authoring form on `SCR-POST-002: Create Post` and corresponding direct creation action buttons on `SCR-FEED-001` and `SCR-REQ-003` are removed. The backend endpoint `POST /api/v1/posts` and `CreateOperationalPostCommand` are decommissioned. Operational posts are generated exclusively by system events when Requests are created.

## Business Rules

- Direct user authoring or submission of operational posts is strictly prohibited (CR-001 directive; Post Domain Rule 6).
- Operational posts in the Operational Feed must originate as system-generated records from Request creation events (Post Domain Rule 6; Request Domain).
- Historical human-authored posts created prior to revocation remain preserved in read/thread views for audit integrity, but no new human-authored posts can be submitted.

## Success Result

Attempts to access `SCR-POST-002` or directly create operational posts are not available in the user interface. Operational feed items are generated strictly through Request creation.

## Failure Conditions

- Any attempt to submit direct post creation via API returns an error or is rejected by the system.

## Acceptance Criteria

- [x] Direct post creation capability (`FEAT-FCOL-003`) is marked as REVOKED per CR-001.
- [ ] Direct authoring UI (`SCR-POST-002`, "New Operational Post" button in `SCR-FEED-001`, and reference links in `SCR-REQ-003`) is removed.
- [ ] Backend direct post creation endpoint (`POST /api/v1/posts`) and command are decommissioned.
- [ ] Operational Feed entries are produced automatically upon Request creation (`RequestRecorded` domain event).
- [ ] Historical human-authored posts remain readable without schema corruption.

## Implementation Notes

Decommissioning of `FEAT-FCOL-003` requires removing UI entry points, decommissioning the command/endpoint, and routing operational post generation to the `RequestRecorded` domain event handler.
