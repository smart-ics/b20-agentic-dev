# ISSUE

## Metadata

ID: CR-023
Type: CHANGE-REQUEST
Status: OPEN
Title: Target Deadline Date for Work Package Aggregate and Screen (SCR-WP-001)

## Source

Reported By: User
Reported Date: 2026-10-07

## Description

Operational teams and project managers using Work Packages (`SCR-WP-001`) currently have no mechanism to record or track an overarching target completion deadline for a Work Package container. While individual operational requests have an optional target deadline attribute (introduced in CR-020), the parent Work Package aggregate root lacks a target deadline attribute. 

When operational work packages are created, scheduled, or tracked, users cannot specify when the entire package of work is targeted to be delivered, nor can operators or managers identify whether an active work package has exceeded its target completion date.

## Desired Outcome

1. **Target Deadline Attribute**:
   - The Work Package aggregate root (`WorkPackage.cs`) and database table `[workpackage].[WorkPackages]` include an optional target deadline attribute (`Deadline`).
   - The deadline is handled as a date-only value (stored normalized to UTC midnight, e.g., `YYYY-MM-DD`), serving as an informational target delivery date without past-date restriction upon entry.
2. **Creation Flow**:
   - Users can optionally specify a target deadline date when creating a new Work Package via the "Create Work Package" modal on `SCR-WP-001` (`WorkPackageView.vue`).
3. **Lifecycle & Mutability Rules**:
   - The target deadline is optional at creation.
   - Authorized users can update or clear (set to null) the target deadline on an existing Work Package while it remains in `DRAFT` or `ACTIVE` lifecycle states.
   - Once a Work Package is `CLOSED`, its target deadline becomes strictly immutable and cannot be updated.
4. **Backend API Endpoints**:
   - A dedicated REST endpoint is provided to update/clear the deadline: `PUT /api/v1/work-packages/{id}/deadline` (accepting a payload with optional `deadline` date or `null`).
   - The general update / objective endpoint (`PUT /api/v1/work-packages/{id}/objective`) or Work Package update command can also accommodate the deadline update.
   - Updating the deadline updates the entity property and `UpdatedAt` timestamp (no separate domain event required).
5. **Read Models & DTOs**:
   - `WorkPackageDto` exposes the optional `Deadline` date.
   - Read queries and endpoints (`GET /api/v1/work-packages`, `GET /api/v1/work-packages/{id}`) return the target deadline.
6. **UI Display & Overdue Visualization on SCR-WP-001**:
   - **Work Package List Table**: Displays a "Deadline" column showing the formatted date (e.g. `YYYY-MM-DD` or `MMM D, YYYY`).
   - **Work Package Detail Panel**: Displays the target deadline in the Work Package information card with an edit/clear control available when in `DRAFT` or `ACTIVE` states.
   - **Overdue Indicator**: If an open Work Package (`DRAFT` or `ACTIVE`) has a deadline that is in the past (`Deadline < Today`), the UI displays a prominent "Overdue" badge/indicator to alert operators. Once the package is `CLOSED`, the overdue warning is hidden.

## Current Situation

1. The `WorkPackage` domain aggregate (`WorkPackage.cs`) and database table `[workpackage].[WorkPackages]` do not have a `Deadline` column or property.
2. The Create Work Package modal (`WorkPackageView.vue`) and creation command (`CreateWorkPackageCommand`) only accept `name`, `objective`, `ownerPersonId`, `customerId`, and `productId`.
3. The Work Package Detail Panel and table on `SCR-WP-001` do not display or allow updating a Work Package target deadline.
4. There is no overdue calculation or badge for Work Packages.

## Evidence

- Domain aggregate: [WorkPackage.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackage.cs#L12-L103)
- Database schema: [0007_workpackage_tables.sql](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Migrations/Scripts/0007_workpackage_tables.sql)
- Screen component: [WorkPackageView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue#L184-L215)
- DTO model: [WorkPackageDto.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Models/WorkPackageDto.cs#L10-L111)
- Controller & Commands: [WorkPackagesController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/WorkPackagesController.cs) and [WorkPackageCommands.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageCommands.cs)
- Intake interview alignment: Intake interview on 2026-10-07 confirming target entity (Work Package aggregate itself), date-only UTC granularity without past restriction, editable/clearable in DRAFT & ACTIVE, immutable when CLOSED, dedicated update endpoint `PUT /api/v1/work-packages/{id}/deadline`, no domain event required, and overdue badge logic when past due.

## Notes

- Intake interview confirmed:
  - Target Entity: Work Package aggregate root itself (`WorkPackages` table).
  - Granularity: Date-only value (stored in UTC midnight), informational target date with no past-date restriction.
  - Lifecycle & Mutability: Optional at creation; editable or clearable in `DRAFT` and `ACTIVE`; immutable in `CLOSED`.
  - API Surface: Dedicated endpoint `PUT /api/v1/work-packages/{id}/deadline` accepting optional date or `null`.
  - Domain Events: None required (updating property and `UpdatedAt` timestamp).
  - Visualization: Display in `SCR-WP-001` list table and detail card, with prominent "Overdue" badge if `Deadline < Today` while in `DRAFT` or `ACTIVE`.
- Downstream workflow routing:
  - Next Stage: Discovery & Feasibility Assessment
  - Owning Role: `ica-analyst`
