# ISSUE

## Metadata

ID: CR-021
Type: CHANGE-REQUEST
Status: OPEN
Title: Work in Progress (WIP) Tracking and Single In-Progress Task Policy

## Source

Reported By: User
Reported Date: 2026-10-07

## Description

Operational teams and personnel need operational visibility into currently active and paused work across all team members, as well as clear tracking of how much total time has elapsed in active execution for each task across multiple start/pause cycles.

Furthermore, operational discipline requires that an individual person can only actively work on at most one task at any given time (single in-progress policy), preventing concurrent multitasking on conflicting demands. Currently, the system lacks an operations-wide Work in Progress view and does not restrict a person from starting multiple tasks concurrently.

## Desired Outcome

1. A dedicated "Work in Progress" screen is accessible to all authenticated users under the Operations section (`/operations/wip`).
2. The view lists all persons who currently have active work (those who have an `IN_PROGRESS` task or at least one `PAUSED` task), attributed strictly to their current assigned owner. If a task was reassigned, it is reported under the current owner only.
3. For each person:
   - Displays their single `IN_PROGRESS` task (prominently at the top), or indicates idle if none is currently in progress.
   - Displays all of their `PAUSED` tasks, ordered by most recently paused/updated.
4. Persons with an active `IN_PROGRESS` task appear first (ordered by name), followed by persons with only `PAUSED` tasks.
5. For each listed task, displays the cumulative elapsed time spent in `IN_PROGRESS` across the entire history of the task (accounting for all start-to-pause and start-to-complete cycles). If the task is currently `IN_PROGRESS`, the live ongoing duration up to the current time is dynamically included in the total.
6. Total in-progress time is formatted as hours and minutes (e.g., `3h 45m` or `0h 25m`), with total decimal hours available via tooltip and data payload.
7. Each task provides a direct link to the Request Detail screen (`/requests/{id}`) for full inspection and execution of lifecycle actions. The screen itself requires no additional search/filter controls.
8. Enforcement of the single in-progress policy: When a person attempts to start work on a task, the action is rejected with a validation error if the person already has another task in `IN_PROGRESS`, requiring them to explicitly pause or complete their ongoing task first.

## Current Situation

1. There is no operations-wide screen or route (`/operations/wip`) for all authenticated users to view team work in progress; active workload visibility is currently restricted to management analytics (`SCR-MGT-003`).
2. The system does not compute or display cumulative elapsed `IN_PROGRESS` duration per task across start/pause cycles.
3. `StartWork` does not enforce a single active in-progress task per person; an owner can transition multiple assigned or paused tasks into `IN_PROGRESS` simultaneously without restriction.

## Evidence

- Domain aggregate: [Request.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs#L251-L345)
- StartWork command handler: [RequestService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs#L341-L368)
- Assignment history schema: [0006_request_tables.sql](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Migrations/Scripts/0006_request_tables.sql#L83-L113)
- Navigation menu structure: [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue#L107-L144)
- Intake interview alignment: Intake interview on 2026-10-07 confirming feature scope (reporting + enforcement), current owner attribution, overall cumulative duration calculation, live in-progress calculation, rejection on start if already active, placement under Operations at `/operations/wip` for all users, ordering, and hours/minutes display format.

## Notes

- Intake interview confirmed:
  - Route: `/operations/wip`, titled "Work in Progress", under Operations sidebar section.
  - Audience: All authenticated users (not restricted to Management/Admin).
  - Policy: Single `IN_PROGRESS` task per person. Attempting to start a second task is rejected with a validation error.
  - Inclusion: Any person with an `IN_PROGRESS` task OR at least one `PAUSED` task.
  - Calculation: Cumulative `IN_PROGRESS` duration across all historical intervals for the task, plus ongoing live time if currently in progress.
  - Format: Hours and minutes (e.g. `3h 45m`), with decimal hours in tooltip and API.
  - Navigation: Links to `/requests/{id}` without inline action controls or filter controls on the WIP page.
- Downstream workflow routing:
  - Next Stage: Discovery & Feasibility Assessment
  - Owning Role: `ica-analyst`
