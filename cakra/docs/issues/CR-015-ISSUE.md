# ISSUE

## Metadata

ID: CR-015
Type: CHANGE-REQUEST
Status: OPEN
Title: Task Drag-and-Drop Reordering in Work Package

## Source

Reported By: User
Reported Date: 2026-10-05

## Description

The user requested the ability to reorder and reposition tasks (linked operational requests) within a Work Package via drag-and-drop in the Work Package management screen (`SCR-WP-001`). Users need an intuitive way to visually prioritize and organize the execution sequence of operational requests belonging to a given Work Package, and have this sequence saved and reflected across user sessions.

## Desired Outcome

1. Users can reorder linked operational requests (tasks) within the scope list of a selected Work Package using drag-and-drop.
2. Each task item in the Work Package scope list displays a clear visual grab/drag handle icon to initiate the reordering action.
3. The UI updates the list order immediately (optimistic UI) upon dropping an item into its new position, providing smooth feedback.
4. The reordered sequence is automatically saved to the backend.
5. If saving fails, the UI displays an error alert and automatically rolls back the task list to its previous order.
6. Drag-and-drop reordering is enabled when the Work Package is in `DRAFT` or `ACTIVE` status, and is disabled/locked when the Work Package is in `CLOSED` status (read-only).
7. Newly linked requests added to a Work Package are placed at the end of the sequence by default.
8. The custom sequence is preserved across page refreshes and displayed consistently to all users viewing the Work Package scope.

## Current Situation

1. In `WorkPackageView.vue` (`SCR-WP-001`), linked operational requests within the Work Package scope section are rendered in a static list.
2. The current backend schema (`workpackage.WorkPackageRequests`) tracks only `AddedAt` and does not support an explicit sort order column.
3. Scope queries return linked requests ordered solely by addition timestamp (`AddedAt`), with no mechanism to customize or reorder their operational sequence.
4. Users have no interactive drag-and-drop affordance to reposition tasks within a package.

## Evidence

- User Request:
  - "/grill-me Tasks in Work Package can be drag and drop to re-posistion or sort it"
- Alignment Interview (/grill-me) decisions:
  - Scope Target: Reordering linked operational Requests (tasks) within a single Work Package to define their sequence/execution order.
  - Persistence Strategy: Persist an explicit integer sort order in `workpackage.WorkPackageRequests` updated via a batch reorder endpoint.
  - Status Rules: Allowed in both `DRAFT` and `ACTIVE` statuses; disabled/locked in `CLOSED` status.
  - Frontend UX: Dedicated drag handle icon (`bi bi-grip-vertical`) with optimistic UI update and automatic backend save on drop.
  - Failure Strategy: Display error alert and automatically roll back the task list to the previous order on API failure.
- Related files:
  - Work Package View: [WorkPackageView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue)
  - Work Package Aggregate Root: [WorkPackage.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackage.cs)
  - Work Package Request Entity: [WorkPackageRequest.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackageRequest.cs)
  - Work Package Repository: [WorkPackageRepository.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Persistence/WorkPackageRepository.cs)
  - Work Package Table Migration: [0007_workpackage_tables.sql](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Migrations/Scripts/0007_workpackage_tables.sql)

## Notes

This artifact formally captures the intake request for Task Drag-and-Drop Reordering in Work Packages (CR-015) according to Knowledge-Centric SDLC standards. Feasibility assessment, domain/feature updates, architecture design, and implementation planning belong to downstream stages.
