# ISSUE

## Metadata

ID: CR-030
Type: CHANGE-REQUEST
Status: OPEN
Title: Default Task Assignee to Work Package Owner with Replacement Capability in SCR-WP-001

## Source

Reported By: User
Reported Date: 2026-10-15

## Description

When users capture or add tasks to a Work Package in `SCR-WP-001` (`WorkPackageView.vue`), all newly recorded requests currently have no assigned owner and are placed into the `CAPTURED` state unassigned. Users must subsequently navigate to each request individually to assign responsibility.

Users request that all constituent tasks (requests) captured within or added to a Work Package automatically default their assignee/owner to the Work Package owner. At the same time, users must have the flexibility to replace or override this default assignee—both during quick capture preview before submitting and directly within the Work Package scope management list—or explicitly leave a task unassigned.

## Desired Outcome

1. **Default Assignee Pre-Population in Quick Capture**:
   - In the Create Work Package modal (`SCR-WP-001`), each parsed candidate task in the preview list defaults its assignee to the selected Work Package Owner.
   - In the Scope Management Quick Bulk Add panel (`SCR-WP-001`), each parsed candidate task in the preview list defaults its assignee to the current Work Package Owner.
   - If tasks are submitted directly from raw text without prior parsing, all generated requests automatically receive the Work Package Owner as their initial assignee.

2. **Inline Assignee Replacement During Capture Preview**:
   - Each candidate task item in both preview lists displays an inline assignee selector dropdown.
   - The dropdown displays all active organization personnel, with the Work Package Owner selected by default.
   - Users can replace the default assignee with another person or explicitly select "Unassigned".
   - In the Create Work Package modal, if the user changes the overall Work Package Owner in the modal form, candidate tasks that were not manually overridden automatically update their default assignee to follow the new package owner.

3. **Scope Management List Visibility & Inline Reassignment**:
   - In the Work Package Scope Management list (`activeScopeItems` on `SCR-WP-001`), each linked task item displays its current assigned owner (or indicates "Unassigned").
   - Users with package modification permissions can quickly reassign any task's owner directly from the Scope Management list via an inline assignee selector without leaving `SCR-WP-001`.

4. **Lifecycle and Assignment Persistence**:
   - Tasks recorded with an assigned owner transition immediately to `ASSIGNED` status with appropriate assignment audit trail records.
   - Tasks recorded as "Unassigned" remain in `CAPTURED` status.

5. **Non-Cascading Invariant**:
   - The package owner is an initial default at task capture time only.
   - Changing the Work Package Owner at a later date does not cascade or overwrite existing task assignments.
   - Linking an existing request to the Work Package preserves the request's existing assignment state.

## Current Situation

1. In `SCR-WP-001` (`WorkPackageView.vue`), `recordRequest` calls in both the Create modal (`handleCreateWorkPackage`) and Scope Quick Bulk Add (`handleBulkAddTasksToScope`) do not pass an owner or assignee parameter, leaving all newly generated requests in `CAPTURED` status without an owner.
2. The candidate task preview lists only display sequence, complexity badge, and title; they do not provide an assignee indicator or replacement control.
3. The Scope Management list items on `SCR-WP-001` display title, complexity, status, blockers, and state aging, but do not show the assigned owner or provide inline reassignment capability.
4. Backend request intake (`RecordRequestCommand` / `POST /api/v1/requests`) does not accept an initial owner/assignee parameter upon request recording.

## Evidence

- Screen component: [WorkPackageView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue#L982-L1005)
- Scope bulk add: [WorkPackageView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue#L1338-L1352)
- Scope list rendering: [WorkPackageView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue#L3259-L3370)
- Request API helper: [requests.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/requests.ts#L92-L115)
- Backend command: [RequestCommands.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs#L15-L29)
- Intake Alignment Interview: Conducted on 2026-10-15 via `/grill-me`, confirming constituent Requests as the target entity, pre-fill with package owner, inline override in preview, inline reassignment in Scope list, support for Unassigned option, and non-cascading behavior.

## Notes

- Intake interview confirmed:
  - Target Entity: Constituent Requests in the Work Package.
  - Replacement: Supported both during quick capture preview and via inline reassignment in the Scope Management list.
  - Flexibility: Tasks can be assigned to package owner (default), replaced with any active person, or set to "Unassigned".
  - Scope Stability: Existing task assignments are preserved when package owner changes or when existing requests are linked.
