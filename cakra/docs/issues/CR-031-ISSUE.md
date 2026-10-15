# ISSUE

## Metadata

ID: CR-031
Type: CHANGE-REQUEST
Status: OPEN
Title: Display Truncated RequestId in My Request Screen (SCR-REQ-004) and Support Query Normalization

## Source

Reported By: User
Reported Date: 2026-10-15

## Description

In the 'My Request' screen (SCR-REQ-004), the RequestId is currently displayed as a full 36-character GUID in the card header and sub-task lists. The full raw GUID provides little cognitive meaning to the user, consumes significant horizontal screen real estate, and distracts focus from actionable details (e.g., ticket title, status, and priority).

Users request that the Request ID be visually compacted into a concise, recognizable reference format across SCR-REQ-004, while keeping the full GUID accessible on demand and maintaining seamless searchability.

## Desired Outcome

1. **Truncated Request ID Representation**:
   - In SCR-REQ-004 Assigned Requests cards, display the Request ID as an 8-character hash prefix formatted as `#<8-chars>` (e.g., `#a1b2c3d4`) rather than the full 36-character GUID string.
   - In SCR-REQ-004 Assigned Sub-Tasks table, display the parent request ID formatted with the same `#<8-chars>` truncation.
2. **Full Identifier Accessibility**:
   - Keep the full GUID accessible via native browser tooltip (`title="req.id"` / `title="subTask.requestId"`) when hovering over the truncated ID.
   - Maintain the ID as a direct navigation link to the Request Detail screen (`SCR-REQ-003`).
3. **Search Query Normalization**:
   - Search filtering in SCR-REQ-004 must normalize user input by stripping leading `#` symbols so queries matching either `#a1b2c3d4` or `a1b2c3d4` successfully match tickets.

## Current Situation

1. In `SCR-REQ-004` (`MyRequestsView.vue`, lines 681–689), the Assigned Requests card header renders `{{ req.id }}` in full font-monospace cyan text, displaying all 36 characters of the GUID directly next to priority and status badges.
2. In `SCR-REQ-004` (`MyRequestsView.vue`, lines 902–904), the Assigned Sub-Tasks table renders `{{ subTask.requestId }}` as full 36-character GUID text under each parent request title.
3. The search filters in `MyRequestsView.vue` check `(req.id ?? '').toLowerCase().includes(q)`. If a user types `#<id>` as displayed on the screen, searching returns 0 results because the raw GUID string does not contain a `#`.

## Evidence

- Screen Component: [MyRequestsView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/MyRequestsView.vue#L681-L689)
- Sub-Task Table Parent Request Column: [MyRequestsView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/MyRequestsView.vue#L902-L904)
- Search Filter Logic: [MyRequestsView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/MyRequestsView.vue#L145-L157)
- UI Specification Reference: [16-scr-req-004.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/16-scr-req-004.md#L68-L72)
- Existing Codebase Precedents:
  - [FeedTimelineCard.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/FeedTimelineCard.vue#L516)
  - [WorkPackageView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue#L2489)
- Intake Alignment Interview: Conducted on 2026-10-15 via `/grill-me`, confirming the `#<8-chars>` convention, scope covering both Assigned Requests and Sub-Tasks tabs, native tooltip hover for full GUID, and search normalization stripping `#`.

## Notes

- Intake interview confirmed:
  - Truncation convention: `#` prefix followed by the first 8 characters of the GUID.
  - Hover behavior: Native HTML `title` tooltip displaying full GUID.
  - Navigation behavior: Click continues to navigate directly to Request Detail (`/requests/${id}`).
  - Search normalization: Strip leading `#` in search inputs to avoid false empty results.
  - Scope: Confined to `SCR-REQ-004` (`MyRequestsView.vue`). Other screens (`SCR-REQ-005`, `SCR-REQ-003`) remain untouched.
