# ISSUE

## Metadata

ID: CR-020
Type: CHANGE-REQUEST
Status: OPEN
Title: Optional Target Deadline for Request Aggregate and Screens

## Source

Reported By: User
Reported Date: 2026-10-06

## Description

Operational teams need the ability to define an optional target deadline for operational requests. Currently, requests have priority and complexity ratings, but lack any representation of a target completion date. When requests are captured or managed, users cannot record when the operational outcome is expected to be delivered, nor can operators or managers identify whether an active request is overdue relative to its target deadline.

## Desired Outcome

1. Users can optionally specify a target deadline when recording a new Request via Create Request flows (`SCR-REQ-002` / `CreateRequestModal.vue` and `CreateRequestView.vue`).
2. The target deadline is optional and handled as a date-only value with no past-date restriction upon entry (serving as an informational target date).
3. Authorized users can update or clear (set to null) the target deadline on an existing request via the Request Details edit flow (`SCR-REQ-003` / `RequestDetailView.vue`) while the request is in any active lifecycle state (`CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`).
4. Closed requests (`COMPLETED`, `CANCELLED`) cannot have their deadline modified.
5. When a deadline is set, changed, or cleared on an existing request, the change is explicitly recorded in the request assignment and lifecycle audit history timeline with a clear descriptive note (e.g., `Deadline set to YYYY-MM-DD`, `Deadline changed from YYYY-MM-DD to YYYY-MM-DD`, or `Deadline cleared`).
6. The target deadline is displayed in the Request Information card on `SCR-REQ-003`.
7. If an open request has a deadline that has passed (`Deadline < Today`), the UI prominently displays an "Overdue" badge/indicator across request detail and list/card views to alert operators. Once a request is closed (`COMPLETED` or `CANCELLED`), the overdue warning is no longer shown.
8. Request read models and DTOs include the optional deadline date.

## Current Situation

1. The `Request` aggregate root (`Request.cs`) and database table `[request].[Requests]` do not contain a deadline or due date attribute.
2. Request creation forms and endpoints (`CreateRequestModal.vue`, `CreateRequestView.vue`, `POST /api/v1/requests`) do not accept a deadline parameter.
3. The Request Detail screen (`RequestDetailView.vue`, `SCR-REQ-003`) and core attribute edit modal only manage Title, Description, Request Type, and Priority, with no deadline field.
4. There is no overdue calculation or visual indicator in the application.

## Evidence

- Domain aggregate: [Request.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs#L11-L67)
- Database schema: [0006_request_tables.sql](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Migrations/Scripts/0006_request_tables.sql#L29-L47)
- Create modal component: [CreateRequestModal.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/CreateRequestModal.vue#L92-L100)
- Request detail view: [RequestDetailView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue#L644-L688)
- Intake interview alignment: Intake interview on 2026-10-06 confirming optional date-only target deadline, active state lifecycle gating, timeline audit note generation upon deadline change, overdue badge behavior, and placement in creation and detail edit forms.

## Notes

- Intake interview confirmed:
  - Granularity: Date-only value (stored in UTC), purely informational target date with no past-date restriction.
  - Mutability: Optional at creation; editable or clearable during active states (`CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`); immutable once closed (`COMPLETED`, `CANCELLED`).
  - Audit logging: Explicit assignment audit history entry logged whenever deadline changes, with note detailing old/new date or clearance.
  - Overdue visualization: Prominent badge when `Deadline < Today` while open; cleared when closed.
  - Form placements: `CreateRequestModal.vue`, `CreateRequestView.vue`, and `RequestDetailView.vue` (both card display and edit modal).
- Downstream workflow routing:
  - Stage: Discovery & Feasibility Assessment
  - Owning Role: `ica-analyst`
