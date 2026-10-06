# ISSUE

## Metadata

ID: CR-018
Type: CHANGE-REQUEST
Status: OPEN
Title: Edit Request Core Attributes in SCR-REQ-003

## Source

Reported By: User
Reported Date: 2026-10-06

## Description

Users need the capability to edit a request's core attributes—specifically Title, Description, Priority, and Request Type—directly within the authoritative Request Detail screen (`SCR-REQ-003: Request Detail`). Although the screen layout specification identifies "Edit Core Attributes" as an authoritative action, the application currently provides neither an interface nor a processing path to modify these fundamental attributes once a request has been created.

## Desired Outcome

1. Authorized operational actors can edit core attributes (Title, Description, Request Type, Priority) directly from `SCR-REQ-003: Request Detail`.
2. Editing is permitted in all active lifecycle states (`CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`) and prohibited once a request is closed (`COMPLETED`, `CANCELLED`, `REJECTED`).
3. Authorization permits the assigned Request Owner, Managers, and Administrators to edit the request, as well as authenticated operational staff if the request is unassigned in `CAPTURED` state.
4. An `Edit` trigger button is available on the Request Detail Card header next to the title, opening an "Edit Request Details" modal dialog.
5. Input validation requires non-empty Title (maximum 255 characters) and non-empty Description, providing clear feedback on errors and updating the display in-place with a success notification upon completion.
6. Context linkages (Customer, Product, Work Package) remain separate and are not altered by this edit operation.
7. Updating request core attributes maintains consistency with the associated root operational post in the Operational Feed stream (updating its Title and Description), without producing noisy feed items or extraneous assignment audit records.

## Current Situation

1. The Request Detail screen (`RequestDetailView.vue`, `SCR-REQ-003`) displays the Title and Description in a read-only presentation card without an edit control or modal trigger.
2. Only complexity rating, checklist sub-tasks, ownership assignment, and lifecycle transitions (Start, Pause, Complete, Cancel) have dedicated operational actions on this screen.
3. The backend API lacks a dedicated request update endpoint for core attributes on existing requests.
4. The screen specification (`15-scr-req-003.md`) documents "Edit Core Attributes" as an available action for Request Owner, but this action is unfulfilled in the implementation.

## Evidence

- UI Layout specification: [15-scr-req-003.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/15-scr-req-003.md#L72-L74)
- Screen component: [RequestDetailView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue#L1053-L1070)
- Backend API controller: [RequestsController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/RequestsController.cs)
- Intake interview alignment: Intake interview on 2026-10-06 establishing lifecycle states, authorized roles, modal UI pattern, editable fields (Title, Description, Priority, Request Type), and feed synchronization.

## Notes

- Intake interview confirmed:
  - Editable fields: Title, Description, Priority, and Request Type.
  - Context linkages (Customer, Product, Work Package) are excluded from this modal.
  - Active states only; locked when closed.
  - Authorization: Request Owner, Manager, Administrator (and authenticated staff if unassigned in CAPTURED).
  - Modal dialog pattern launched from the Request Detail card header.
  - Synchronization of root feed post title/content with no extraneous audit/feed noise.
- Downstream workflow routing:
  - Stage: Discovery & Feasibility Assessment
  - Owning Role: `ica-analyst`
