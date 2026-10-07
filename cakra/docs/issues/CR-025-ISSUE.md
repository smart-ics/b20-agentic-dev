# ISSUE

## Metadata

ID: CR-025
Type: CHANGE-REQUEST
Status: OPEN
Title: Work Package Operational Visualization, Demand Density (Scope Pressure), and Executive Operations Cockpit

## Source

Reported By: User (COO / Operational Leadership)
Reported Date: 2026-10-07

## Description

Operating executives (COO, Operational Managers) currently have no effective visualization or triage mechanism for monitoring hundreds of concurrent Work Packages in Cakra. The existing system presents Work Packages in a basic flat table or individual detail panel without any representation of remaining effort, operational health, demand density, or progress.

Furthermore, traditional project management metrics—such as percentage-complete progress bars, artificial start dates, and speculative velocity estimates—fail in Cakra because capacity is fluid, programmers move dynamically across packages, and sunk effort does not guarantee completion or identify operational stalls. Executives require a factual, exception-based operational visualization system that immediately answers "Where do I direct attention right now?" without relying on artificial schedules, subjective progress percentages, or speculative velocity forecasting.

## Desired Outcome

1. **Factual Demand Density & Scope Pressure Calculation**:
   - For any active Work Package with a target deadline, calculate Scope Pressure based on observable physical work density:
     - Remaining Complexity = sum of Complexity of unclosed Requests.
     - Working Days Remaining = calendar working days from current date to Target Deadline.
     - Required Daily Burn = Remaining Complexity / Working Days Remaining.
     - Org Capacity Share = (Required Daily Burn / Org 30-Day Demonstrated Daily Output) × 100%.
   - For active Work Packages without a target deadline, explicitly categorize Scope Pressure as `UNPLANNED` (Pressure Unknown), preserving the distinction between low pressure and uncommitted pressure.
2. **Observable Operational Health Invariants (Zero Subjectivity)**:
   - System flags operational invariant violations derived strictly from observable domain states and events:
     - *Deadline Breach*: Working Days Remaining ≤ 0 while unclosed Requests exist.
     - *Active Blockers*: Count of constituent Requests with Status `BLOCKED` > 0.
     - *Dormancy*: Elapsed business days since the last recorded operational state mutation or domain event exceeds a configurable threshold (e.g., > 5 days).
     - *WIP Stagnancy / Flow Inventory*: Active concurrent requests open for extended periods (e.g., > 14 days) with zero closures.
     - *Flowing*: Normal active state with zero invariant violations.
3. **Pressure × Health Triage Matrix on Executive Dashboard**:
   - Provide an Executive Operations Cockpit displaying an interactive 2D Triage Matrix:
     - Columns: Scope Pressure Tiers (`UNPLANNED`, `NOMINAL` [<20%], `ELEVATED` [20-50%], `CRITICAL` [50-100%], `IMPOSSIBLE` [>100%]).
     - Rows: Health Invariant States (`DEADLINE BREACHED`, `ACTIVE BLOCKERS`, `DORMANT`, `WIP STAGNANT`, `FLOWING`).
   - Every matrix cell displays the count of matching active Work Packages and acts as an interactive drill-down filter.
   - Aggregate portfolio macro indicators are visible: Total Active Packages, Portfolio Aggregate Load (% of company capacity over-committed), packages With Deadline, and packages Without Deadline.
   - The dashboard operates on an exception-based model, filtering to packages with invariant violations or critical pressure while keeping nominal flowing packages collapsed.
   - Present cold operational facts only; executive decision recommendations are omitted to preserve uncompromised COO judgment.
4. **Enhanced Work Package Card Visuals**:
   - Work Package cards on lists and drawers display:
     - Scope Demand & Org Capacity Share tier (or `UNPLANNED`).
     - Target Deadline and Remaining Runway (working days left).
     - Remaining Complexity vs. Total Complexity.
     - Observable Health Invariant pills (e.g. Blockers, Dormancy age).
     - Flow Inventory facts (Active WIP count, Oldest In-Flight Age, 14-day Outflow count).
     - 14-Day Flow Barcode displaying daily event occurrences (closures, state transitions, blockers).
5. **Work Package Detail Workbench**:
   - Displays constituent Requests with explicit complexity ratings, state aging (days in current status), and blocker details.
   - Provides an interactive decision workbench to simulate the impact of de-scoping requests or extending the target deadline on the calculated Org Capacity Share.

## Current Situation

1. `WorkPackageView.vue` renders Work Packages as a standard table with ID, Objective, Owner, Customer, Product, Deadline, and Status columns.
2. There is no progress representation, remaining effort calculation, or demand density metric on Work Packages.
3. There is no detection or visualization of dormancy (inactive packages), blocked requests, or stagnant WIP at the Work Package level.
4. There is no executive portfolio dashboard, aggregate load calculation, or Pressure × Health triage matrix in Cakra.
5. In `WorkPackage.cs` and `WorkPackagesController.cs`, read queries return basic Work Package entity properties without aggregated request complexity or operational event statistics.

## Evidence

- Screen component: [WorkPackageView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue)
- Work Package Domain specification: [work-package-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/work-package-domain.md)
- Request Domain specification: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- Work Package Target Deadline issue: [CR-023-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-023-ISSUE.md)
- Architectural specification artifact: [work-package-operational-visualization-architecture.md](file:///C:/Users/drury/.gemini/antigravity/brain/ba7d9357-e8ff-4ef1-a8d2-4373df8bf458/work-package-operational-visualization-architecture.md)
- Intake interview alignment: Intake interview on 2026-10-07 (/grill-me) establishing:
  - Rejection of Progress %, artificial start dates, and speculative velocity.
  - Sizing anchored by Request Complexity (1–5 scale).
  - Scope Pressure anchored to Org 30-Day Demonstrated Daily Throughput.
  - Factual operational health invariants (Deadline Breach, Blockers, Dormancy, WIP Stagnancy).
  - Explicit `UNPLANNED` pressure class for packages without a deadline.
  - Interactive Pressure × Health Matrix as the primary triage navigation tool.
  - Pure factual reporting without algorithmic advice.

## Notes

- Intake interview confirmed:
  - Target Audience: COO managing 100–300 Work Packages concurrently.
  - Metric Decoupling: Scope Pressure (demand density) is decoupled from Operational Health (observable state invariants).
  - Purity Rule: No velocity math, no subjective progress percentages, no smoothed trendlines, no automated decision recommendations.
- Downstream workflow routing:
  - Next Stage: Discovery & Feasibility Assessment
  - Owning Role: `ica-analyst`
