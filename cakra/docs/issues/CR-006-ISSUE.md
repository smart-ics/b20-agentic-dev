# ISSUE

## Metadata

ID: CR-006
Type: CHANGE-REQUEST
Status: OPEN
Title: Request Sub-Tasks Breakdown and Completion Progress Tracking

## Source

Reported By: User
Reported Date: 2026-10-03

## Description

The user requested that operational Requests can be broken down into sub-tasks with progress tracking based on completed sub-tasks.

All functional and operational specifications for this change request:
1. **Sub-Task Composition**: An operational Request can have zero or more lightweight sub-tasks representing actionable breakdown items required to fulfill the demand.
2. **Sub-Task Attributes**: Each sub-task captures a title, an optional assignee (Person from Organization domain), a binary status (`Pending` / `Completed`), completion metadata (timestamp and actor), and sort position.
3. **Completion Prerequisite Rule**: A Request cannot transition to `Completed` while any of its sub-tasks remain incomplete. The system must strictly reject completion attempts if unresolved sub-tasks exist.
4. **Sub-Task Lifecycle and Mutability**:
   - Sub-tasks can be added, updated, re-ordered, removed, or marked completed/reopened only while the parent Request is in an active lifecycle state (`Captured`, `Evaluating`, `Accepted`, `InProgress`, `Escalated`).
   - If a sub-task is no longer needed or deemed out-of-scope, it can be deleted/removed while the Request is active.
   - Once a Request reaches a closed state (`Completed` or `Rejected`), all sub-tasks become immutable and read-only.
5. **Reopening Behavior**: A completed sub-task can be reopened back to `Pending` at any point as long as the parent Request is active.
6. **Completion Percentage Metric**:
   - Every Request maintains a standardized completion percentage (integer 0 to 100%) and sub-task counts (total and completed).
   - When sub-tasks exist, completion percentage is calculated as `(CompletedSubTasksCount / TotalSubTasksCount) * 100`.
   - When no sub-tasks exist, completion percentage is 100% if the Request status is `Completed`, and 0% otherwise.
   - The metric is persisted on the Request record to support fast SQL sorting, filtering, and reporting.
7. **Role and Assignment Authorization**:
   - Creating, modifying, reordering, or removing sub-tasks is permitted for the Request Owner, Administrators, Team Leads, and Managers.
   - Marking a sub-task completed or reopening it is permitted for the assigned Person of that sub-task, the Request Owner, and operational management roles.
8. **Intake and Management Interface**:
   - Requesters can optionally provide an initial list of sub-tasks during Request creation (`RecordRequestCommand`).
   - Dedicated commands are provided to add, remove, complete, and reopen sub-tasks incrementally after creation.
9. **Auditability and Domain Events**: Every sub-task lifecycle action emits a domain event (`RequestSubTaskAdded`, `RequestSubTaskCompleted`, `RequestSubTaskReopened`, `RequestSubTaskRemoved`) capturing actor, timestamp, sub-task identifiers, and updated progress metrics.
10. **Query and Workspace Presentation**:
    - Query DTOs expose the full sub-task list along with summary counters (`TotalSubTasksCount`, `CompletedSubTasksCount`, `CompletionPercentage`).
    - Sub-tasks assigned to an individual are visible in their personal workspace (e.g. My Requests / Assigned Work) without changing the overall Request Owner.
    - User interface displays an interactive checklist card on the Request Detail page, progress bars showing percentage and fraction (e.g., `60% (3/5)`) in Request List, Detail, and personal queue views.

## Desired Outcome

1. Operational demand owners and requesters can decompose complex Requests into actionable sub-tasks.
2. Individual sub-tasks can be assigned to different collaborators while maintaining single Request ownership.
3. System strictly prevents closing or completing a Request when any sub-tasks remain unresolved.
4. Operational progress is transparently reflected via completion percentage (0-100%) and fractional counts across table, detail, and personal queue views.
5. Authorized actors can toggle sub-task completion, reopen tasks for rework, or delete unneeded tasks during active request states.
6. Sub-task states and progress become immutable once a Request is closed (`Completed` or `Rejected`).
7. Sub-task lifecycle changes are fully auditable through domain events and operational feed visibility.
8. Assigned team members can view and resolve their assigned sub-tasks directly in their personal workspace.

## Current Situation

1. The `Request` aggregate root ([Request.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs)) contains operational metadata (Title, Description, RequestType, Status, Priority, Complexity, OwnerPersonId), but has no child collection or mechanism for sub-tasks.
2. The Request completion workflow ([RequestService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs)) permits transitioning to `Completed` without checking for unresolved child work.
3. No progress tracking score, sub-task counters, or completion percentage exists on `Request` or `RequestDto`.
4. Responsibility delegation is restricted to top-level Request ownership (`OwnerPersonId`), with no ability to distribute discrete tasks to other collaborators.
5. In persistence ([RequestRepository.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Persistence/RequestRepository.cs)), table `[request].[Requests]` has no sub-task count or percentage columns, and no child `[request].[RequestSubTasks]` table exists.
6. The frontend application ([Cakra.Web](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web)) provides no checklist controls on [RequestDetailView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue), no progress visualization on [RequestListView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestListView.vue), and no sub-task queue on [MyRequestsView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/MyRequestsView.vue).

## Evidence

- Brainstorming Specification: [request_subtasks_brainstorming_specification.md](file:///C:/Users/drury/.gemini/antigravity/brain/5878bc7a-289d-4fc8-89b7-77673ed6d3a2/request_subtasks_brainstorming_specification.md)
- Request Aggregate Root: [Request.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs)
- Request Commands: [RequestCommands.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs)
- Request DTO: [RequestDto.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Models/RequestDto.cs)
- Request Service Implementation: [RequestService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs)
- Request Repository: [RequestRepository.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Persistence/RequestRepository.cs)
- Frontend Views: [RequestDetailView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue), [RequestListView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestListView.vue), [MyRequestsView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/MyRequestsView.vue)

## Notes

This artifact formally captures the change request in an intake format adhering to Knowledge-Centric SDLC standards without prescribing architectural solutions. Feasibility analysis, domain model updates, feature specifications, architecture updates, and implementation planning will be executed by downstream roles.
