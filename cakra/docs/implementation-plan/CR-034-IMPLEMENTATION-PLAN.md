---
Title: Implementation Plan for Compact Operational Awareness Board Redesign with Dual-Signal Elapsed and Effort-to-Limit Indicators (CR-034)
Code: CR-034
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-15
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-034`: Compact redesign of the Work in Progress (WIP) operational awareness board (`SCR-REQ-006`), introducing dual independent time signals (pure elapsed time heat and effort-to-limit budget burn) and quick-glance workload triage indicators across the Cakra system.

Deliver end-to-end realization across:
1. Backend WIP read-model query and DTO enhancement in `Cakra.Modules.Request`: select `Complexity` from `[request].[Requests]`, compute `BudgetHours` (1=2h, 2=4h, 3=8h, 4=16h, 5=24h), `BurnPercentage`, and `IsOverBudget`, and expose them on `TaskWorkInProgressDto`.
2. Frontend API client contract alignment in `src/frontend/Cakra.Web/src/api/requests.ts`: export updated TypeScript interfaces.
3. Frontend screen re-implementation in `src/frontend/Cakra.Web/src/views/WorkInProgressView.vue`: dense continuous feed layout based on prototype `scr-req-006-redesign.html`, featuring:
   - Dual time signals in active and paused task rows (prominent heat-colored elapsed text + 4px burn bar with fraction, percentage, and `OVER` badge).
   - Person left-border workload escalation (0–1 normal green, 2–3 busy amber, 4+ overloaded red) with count badges.
   - StatusStrip `over budget` KPI metric and `Over Budget` filter pill.
   - Slide-over drawer inspection with dual-signal telemetry and `Complexity: {n} ({h}h)` labeling.
   - 100% preservation of all existing `data-testid` and `data-screen-id="SCR-REQ-006"` selectors.
4. Full-stack verification across backend test suites and frontend build checks.

Planning Mode: FEATURE-PLANNING

Referenced artifacts:
- ISSUE: [CR-034-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-034-ISSUE.md)
- FEASIBILITY-ASSESSMENT: [CR-034-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-034-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)
- ARCHITECTURE: [CR-034-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-034-ARCHITECTURE.md) (authoritative target architecture)

Architecture Applicability: ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers read-model query projection, server-side budget/burn metrics calculation, DTO contracts, TypeScript client interfaces, Vue view re-implementation, and full-stack test verification:

1. **Backend Read Model & Query Service (`Cakra.Modules.Request`)**:
   - Update `activeRequestsSql` in `RequestQueryService.cs` to `SELECT r.[Complexity]`.
   - Update private row class `WorkInProgressRequestRow` to include `public int Complexity { get; init; }`.
   - Implement static helper `GetBudgetHoursForComplexity(int complexity)` mapping 1=2h, 2=4h, 3=8h, 4=16h, 5=24h.
   - Calculate `BurnPercentage` and `IsOverBudget` during DTO mapping.
   - Extend `TaskWorkInProgressDto` in `WorkInProgressDto.cs` with `Complexity`, `BudgetHours`, `BurnPercentage`, and `IsOverBudget`.
   - Add unit tests verifying `GetWorkInProgressOverviewAsync` projection and burn arithmetic.
2. **Frontend API Client (`Cakra.Web`)**:
   - Update `TaskWorkInProgressDto` interface in `src/api/requests.ts` with matching fields.
3. **Frontend Screen Redesign (`Cakra.Web`)**:
   - Re-implement `WorkInProgressView.vue` (`SCR-REQ-006`) with dense continuous feed layout, dual time signals, workload escalation borders, StatusStrip KPI ribbon additions, filter pills, and updated inspection drawer.
   - Strictly preserve all automated test attributes (`data-testid`, `data-screen-id="SCR-REQ-006"`).
4. **Full-Stack Verification**:
   - Execute backend test suite (`dotnet test`).
   - Execute frontend typecheck and build (`npm run build`).

---

# 3. Dependencies

- .NET 8 SDK & ASP.NET Core (`Cakra.Api`, `Cakra.Modules.Request`)
- Dapper object mapping in `Cakra.Modules.Request.Persistence`
- Node.js & npm (Vue 3 / Vite / TypeScript in `src/frontend/Cakra.Web`)

For slice dependencies:
- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires the referenced slice to have implementation status `IMPLEMENTED`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Backend Read Model & Query Enhancement | IMPLEMENTED | GO | 1/1 |
| P2 - Frontend API Client & Operations Screen | IMPLEMENTED | GO | 2/2 |
| P3 - Full-Stack Verification & Non-Regression | NOT-STARTED | GO | 1/1 |

---

# 5. Phases

## P1 - Backend Read Model & Query Enhancement

Implementation Status: IMPLEMENTED
Review Status: GO

### P1-S01

Title: Extend WIP Read Model DTO and Query Projection with Complexity & Budget Metrics

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Extend `TaskWorkInProgressDto` and `RequestQueryService.GetWorkInProgressOverviewAsync` to retrieve `Complexity` from `[request].[Requests]`, calculate `BudgetHours` using the approved formula (1=2h, 2=4h, 3=8h, 4=16h, 5=24h), compute `BurnPercentage`, and determine `IsOverBudget`. Add unit tests in `Cakra.Modules.Request.Tests`.

Depends On: None

Repository: cakra

Completion Criteria:
- `TaskWorkInProgressDto` in `Cakra.Modules.Request/Models/WorkInProgressDto.cs` contains `Complexity`, `BudgetHours`, `BurnPercentage`, and `IsOverBudget`.
- `RequestQueryService.cs` queries `r.[Complexity]`, maps it in `WorkInProgressRequestRow`, and populates `TaskWorkInProgressDto` with correct budget hours and burn metrics.
- Unit tests in `Cakra.Modules.Request.Tests` verify that:
  - Complexity 1 yields 2.0 budget hours; Complexity 3 yields 8.0 budget hours; Complexity 5 yields 24.0 budget hours.
  - Burn percentage equals `Math.Round((TotalInProgressHours / BudgetHours) * 100)`.
  - `IsOverBudget` is true when `TotalInProgressHours >= BudgetHours`.
  - Zero divide-by-zero risk when budget hours is 0.
- All backend unit tests pass (`dotnet test`).

Notes:
- The SQL change is purely additive to the existing Dapper query.
- Implementation Completed:
  - Added `Complexity`, `BudgetHours`, `BurnPercentage`, and `IsOverBudget` to `TaskWorkInProgressDto`.
  - Added `r.[Complexity]` to `activeRequestsSql` and mapped it in `WorkInProgressRequestRow`.
  - Added static methods `GetBudgetHoursForComplexity(int complexity)` and `CalculateBurnMetrics(double totalHours, double budgetHours)` to `RequestQueryService`.
  - Created unit test suite `WorkInProgressBudgetBurnTests.cs` and updated `WorkInProgressDurationCalculationTests.cs`.
  - Verified 100% test pass rate (621 unit tests, 202 integration tests).

Changed Files:
- `src/backend/Cakra.Modules.Request/Models/WorkInProgressDto.cs`
- `src/backend/Cakra.Modules.Request/Services/RequestQueryService.cs`
- `tests/backend/Cakra.Tests.Unit/Request/WorkInProgressDurationCalculationTests.cs`
- `tests/backend/Cakra.Tests.Unit/Request/WorkInProgressBudgetBurnTests.cs`

---

## P2 - Frontend API Client & Operations Screen

Implementation Status: IMPLEMENTED
Review Status: GO

### P2-S02

Title: Update Frontend TypeScript DTO Contract for WIP Tasks

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Update the `TaskWorkInProgressDto` TypeScript interface in `src/frontend/Cakra.Web/src/api/requests.ts` to include `complexity: number`, `budgetHours: number`, `burnPercentage: number`, and `isOverBudget: boolean`.

Depends On: P1-S01

Repository: cakra

Completion Criteria:
- `src/frontend/Cakra.Web/src/api/requests.ts` exports `TaskWorkInProgressDto` with all 4 new fields typed correctly.
- TypeScript compilation checks pass with no type errors.

Notes:
- Contract matches the backend DTO fields in `WorkInProgressDto.cs`.
- Implementation Completed:
  - Added `complexity: number`, `budgetHours: number`, `burnPercentage: number`, and `isOverBudget: boolean` to `TaskWorkInProgressDto` in `src/frontend/Cakra.Web/src/api/requests.ts`.
  - Verified TypeScript compilation and production build succeed with zero errors via `npm run build` (`vue-tsc --noEmit && vite build`).

Changed Files:
- `src/frontend/Cakra.Web/src/api/requests.ts`

---

### P2-S03

Title: Re-Implement WorkInProgressView with Compact Feed, Dual Time Signals, Workload Tiers, and KPI Metrics

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Re-implement `src/frontend/Cakra.Web/src/views/WorkInProgressView.vue` (`SCR-REQ-006`) into a dense, high-signal continuous feed based on `scr-req-006-redesign.html`. Realize:
1. Dual time signals on active and paused task rows:
   - Signal 1: Prominent monospaced elapsed counter colored by duration tiers (`≤4.0h` green, `>4.0h` amber, `>8.0h` red glow).
   - Signal 2: Horizontal 4px burn bar (`<75%` green, `75–99%` amber, `≥100%` red with overflow pip), elapsed vs budget label (e.g. `4h 0m / 8h`), `C{n}` badge, and `OVER` red badge when `isOverBudget`.
2. Workload triage left-border styling on person rows:
   - Green (0–1 paused tasks).
   - Amber (2–3 paused tasks / Busy).
   - Red (4+ paused tasks / Overloaded) with count badges (`N paused`, `N paused ⚠`).
3. Top StatusStrip ribbon:
   - Add `over budget` KPI metric counting persons with at least one over-budget task.
4. Filter toolbar:
   - Add `Over Budget` filter pill.
5. Contextual `SlideOverDrawer`:
   - Display dual time metrics panel and label strictly as `Complexity: {n} ({h}h)`.
6. Strict selector preservation:
   - Preserve all existing `data-testid` and `data-screen-id="SCR-REQ-006"` selectors.

Depends On: P2-S02

Repository: cakra

Completion Criteria:
- `WorkInProgressView.vue` renders compact continuous feed layout with dual time signals.
- Elapsed time counter applies correct color and glow based on duration thresholds.
- Progress bar and `OVER` badge accurately reflect `burnPercentage` and `isOverBudget`.
- Person row left-border color dynamically reflects paused task counts.
- StatusStrip shows `over budget` count and `Over Budget` filter pill isolates affected personnel.
- Drawer displays dual signal metrics and `Complexity: {n} ({h}h)`.
- All automated test selectors (`data-testid`, `data-screen-id`) remain present and functional.

Notes:
- Implementation Completed:
  - Re-implemented `src/frontend/Cakra.Web/src/views/WorkInProgressView.vue` based on the approved redesign prototype (`scr-req-006-redesign.html`).
  - Added Signal 1 (pure elapsed time counter colored by `≤4h` green, `>4h` amber, `>8h` red with glow) and Signal 2 (4px burn bar with overflow pip, fraction label, `C{n}` badge, and `OVER` badge).
  - Implemented dynamic person row left-border workload tiers (`ok-row`, `busy-row`, `crit-row`, `idle-row`) with corresponding count badges.
  - Added `over budget` KPI item to `StatusStrip` and `Over Budget` filter pill to the quick filter toolbar.
  - Updated `SlideOverDrawer` with dedicated dual time telemetry panel and strictly labeled `Complexity: {n} ({h}h)`.
  - Preserved 100% of existing `data-testid` and `data-screen-id="SCR-REQ-006"` attributes.
  - Created unit test suite `tests/unit/wip-view.spec.ts` covering all tier classifications, heat colors, over-budget detection, and customer display resolution (25 tests passing).
  - Verified full test suites pass (`npm run test` with 219 tests passed; `dotnet test` with 621 unit and 202 integration tests passed; `npm run build` succeeds cleanly).

Changed Files:
- `src/frontend/Cakra.Web/src/views/WorkInProgressView.vue`
- `src/frontend/Cakra.Web/tests/unit/wip-view.spec.ts`

---

## P3 - Full-Stack Verification & Non-Regression

Implementation Status: NOT-STARTED
Review Status: GO

### P3-S04

Title: Full-Stack Test Suite Execution & Non-Regression Verification

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Execute the full backend test suite (`dotnet test`) and frontend test/build verification (`npm run test:unit`, `npm run build`) to ensure zero regressions across existing operational features and automated test selectors.

Depends On: P2-S03

Repository: cakra

Completion Criteria:
- `dotnet test` executes with 100% pass rate.
- Frontend unit tests and build pass (`npm run build` succeeds with zero errors).
- Automated test selectors on `SCR-REQ-006` verify cleanly.

Notes:
- Final verification slice before declaring the plan ready for completion.
- Implementation Completed:
  - Full backend test suite executed via `dotnet test`: 621 unit tests and 202 integration tests passed (823 total, 100% pass rate, 0 failed, 0 skipped).
  - Full frontend unit test suite executed via `npm run test` (vitest): 9 test files, 220 tests passed (100% pass rate).
  - Frontend production build & typecheck executed via `npm run build` (`vue-tsc --noEmit && vite build`): succeeded with zero TypeScript errors and successful production bundle generation.
  - Automated test selectors on `SCR-REQ-006` verified cleanly and covered with automated assertions in `tests/unit/wip-view.spec.ts`.

Changed Files:
- `src/frontend/Cakra.Web/tests/unit/wip-view.spec.ts`

---

# 6. Change Log

- 2026-10-15: Initial plan authoring from CR-034-ARCHITECTURE.md. Slices partitioned across backend query/DTO enhancement (P1-S01), frontend contract (P2-S02), compact screen redesign (P2-S03), and full-stack verification (P3-S04). Execution Approval granted.
