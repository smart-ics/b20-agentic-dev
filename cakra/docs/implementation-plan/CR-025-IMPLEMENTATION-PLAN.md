---
Title: Implementation Plan for Work Package Operational Visualization, Demand Density, and Executive Operations Cockpit (CR-025)
Code: CR-025
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-07
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-025`: Work Package Operational Visualization, Demand Density (Scope Pressure benchmarked against Org Demonstrated Daily Throughput), Observable Operational Health Invariants, and the Executive Operations Cockpit (`SCR-WP-002`) in Cakra.

Deliver end-to-end technical realization across:
1. Cross-module empirical throughput query contract in `Cakra.Modules.Request` computing Organization 30-Day Demonstrated Daily Output ($C_{\text{org}}$) across completed requests.
2. Structural demand density engine in `Cakra.Modules.WorkPackage` computing Scope Pressure (Required Daily Burn) and Org Capacity Share ($\% \text{ of } C_{\text{org}}$), with strict classification of dateless packages as `UNPLANNED`.
3. Observable Operational Health Invariants evaluation engine deriving factual states (`DEADLINE_BREACHED`, `ACTIVE_BLOCKERS`, `DORMANT`, `WIP_STAGNANT`, `FLOWING`) without subjective estimation.
4. Unified Operations Cockpit REST API endpoint `GET /api/v1/work-packages/operations-cockpit` in `WorkPackagesController.cs` returning portfolio macro metrics, 2D matrix cell counts, and Work Package telemetry models.
5. Executive Operations Cockpit screen `SCR-WP-002` (`OperationsCockpitView.vue` at `/operations/cockpit`) featuring the interactive 2D Pressure × Health Triage Matrix with drillable cells.
6. Modernized Work Package Card on `SCR-WP-001` (`WorkPackageView.vue`) displaying Capacity Share tiers, Health Invariant pills, Flow Inventory facts, and 14-day Flow Activity Barcodes.
7. Enhanced Work Package Detail Workbench on `SCR-WP-001` providing request state aging, blocker visibility, and an interactive de-scoping and deadline simulator.
8. Full-stack verification ensuring zero regressions across all backend unit/integration tests and frontend builds.

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- ISSUE: [CR-025-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-025-ISSUE.md)
- DOMAIN: [work-package-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/work-package-domain.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- FEASIBILITY-ASSESSMENT: [CR-025-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-025-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)
- ARCHITECTURE: [CR-025-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-025-ARCHITECTURE.md) (authoritative target architecture)

Architecture Applicability: ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers all domain calculations, application queries, REST endpoints, frontend API helpers, UI components, and full-stack verifications required to realize `CR-025`:

1. **Request Module Throughput Service (`Cakra.Modules.Request`)**:
   - Add `GetOrgDemonstratedDailyThroughputAsync` to `IRequestQueryService` and `RequestQueryService`.
   - Add optimized scalar SQL query in `RequestRepository.cs` summing completed request complexity over rolling 30 days.
   - Add unit tests in `Cakra.Tests.Unit`.
2. **Work Package Telemetry Calculator & Models (`Cakra.Modules.WorkPackage`)**:
   - Define DTO models: `OperationsCockpitDto`, `PortfolioMetricsDto`, `PressureHealthMatrixDto`, `WorkPackageTelemetryDto`, `FlowBarcodeDayDto`.
   - Implement `WorkPackageTelemetryCalculator` evaluating working days, Required Daily Burn, Org Capacity Share tiers, Health Invariants, Flow Inventory counters, and 14-day barcodes.
   - Add unit tests for telemetry math.
3. **Application Query & REST Controller (`Cakra.Modules.WorkPackage`, `Cakra.Api`)**:
   - Implement `GetOperationsCockpitQuery` and `GetOperationsCockpitQueryHandler`.
   - Expose `GET /api/v1/work-packages/operations-cockpit` in `WorkPackagesController.cs`.
   - Add integration tests in `Cakra.Tests.Integration`.
4. **Frontend API Client & Screen (`Cakra.Web`)**:
   - Add TypeScript interfaces and `getOperationsCockpit()` helper in `src/frontend/Cakra.Web/src/api/workpackages.ts`.
   - Implement `OperationsCockpitView.vue` (`SCR-WP-002`) at `/operations/cockpit` with route in `router/index.ts` and sidebar link in `App.vue`.
   - Modernize Work Package cards on `SCR-WP-001` (`WorkPackageView.vue`) with Capacity Share badges, Health Invariant pills, and 14-day flow barcodes.
   - Modernize Detail Drawer on `SCR-WP-001` with request state aging and the interactive Decision Simulator.
5. **Full-Stack Verification**:
   - Execute backend test suite (`dotnet test`).
   - Execute frontend production build (`npm run build`).

---

# 3. Dependencies

- .NET 8 SDK & ASP.NET Core (`Cakra.Api`, `Cakra.Modules.WorkPackage`, `Cakra.Modules.Request`)
- MediatR & FluentValidation
- Dapper object mapping
- Node.js & npm (Vue 3 / Vite / TypeScript in `src/frontend/Cakra.Web`)

For slice dependencies:
- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires the referenced slice to have implementation status `IMPLEMENTED`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Core Telemetry Engine & Throughput Contract | IMPLEMENTED | GO | 2/2 |
| P2 - Application Query & REST API Endpoint | IMPLEMENTED | GO | 2/2 |
| P3 - Frontend API Client & Executive Operations Cockpit (SCR-WP-002) | IMPLEMENTED | GO | 2/2 |
| P4 - Work Package Screen Modernization & Workbench (SCR-WP-001) | IMPLEMENTED | GO | 2/2 |
| P5 - Full-Stack Verification & Regression Validation | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Core Telemetry Engine & Throughput Contract

Implementation Status: IMPLEMENTED
Review Status: GO

### P1-S01

Title: Implement Organization Demonstrated Daily Throughput Service ($C_{\text{org}}$)

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Add `GetOrgDemonstratedDailyThroughputAsync(int windowDays = 30, CancellationToken cancellationToken = default)` to `IRequestQueryService` and `RequestQueryService.cs`, executing an optimized Dapper scalar query in `RequestRepository.cs` summing completed request complexity over rolling 30 days with a safety floor of 1.0 pt/day. Add unit test coverage.

Depends On: None

Repository: cakra

Completion Criteria:
- Method `GetOrgDemonstratedDailyThroughputAsync` exists on `IRequestQueryService` and `RequestQueryService`.
- Repository query in `RequestRepository.cs` calculates `SUM(Complexity)` for `Status = 'COMPLETED'` within the rolling window.
- Safety floor of 1.0 pt/day is returned if throughput is 0 or negative.
- Unit tests in `Cakra.Tests.Unit` verify calculation accuracy and floor handling.

Notes: Implemented empirical organization demonstrated daily throughput contract ($C_{\text{org}}$) per Architecture CR-025 TD-001. Added `GetOrgDemonstratedDailyThroughputAsync` and `GetOrgDemonstratedDailyThroughput` to `IRequestQueryService` and `RequestQueryService.cs`. Added `GetCompletedComplexitySumSinceAsync` to `IRequestRepository` and `RequestRepository.cs` executing optimized Dapper scalar query (`SELECT COALESCE(SUM(Complexity), 0) FROM [request].[Requests] WHERE Status = 'COMPLETED' AND UpdatedAt >= @SinceUtc`). Implemented calculation dividing rolling window complexity sum by `windowDays` with safety floor `throughput <= 0.0 ? 1.0 : throughput` and invalid window protection. Registered `IRequestRepository` and `ISystemClock` dependencies in `RequestModule.cs`. Added unit tests in `RequestThroughputTests.cs` and updated mock in `RequestCompletionAndQueriesTests.cs`. All unit tests passed (514 passed, 0 failed).
Changed files:
- `src/backend/Cakra.Modules.Request/IRequestQueryService.cs`
- `src/backend/Cakra.Modules.Request/Persistence/IRequestRepository.cs`
- `src/backend/Cakra.Modules.Request/Persistence/RequestRepository.cs`
- `src/backend/Cakra.Modules.Request/RequestModule.cs`
- `src/backend/Cakra.Modules.Request/Services/RequestQueryService.cs`
- `tests/backend/Cakra.Tests.Unit/Request/RequestCompletionAndQueriesTests.cs`
- `tests/backend/Cakra.Tests.Unit/Request/RequestThroughputTests.cs`

---

### P1-S02

Title: Implement Work Package Telemetry Models & Calculation Engine

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Create DTO models (`OperationsCockpitDto`, `PortfolioMetricsDto`, `PressureHealthMatrixDto`, `WorkPackageTelemetryDto`, `FlowBarcodeDayDto`) in `Cakra.Modules.WorkPackage.Models`. Implement `WorkPackageTelemetryCalculator` evaluating Scope Pressure, Org Capacity Share tiers, Health Invariants (`DEADLINE_BREACHED`, `ACTIVE_BLOCKERS`, `DORMANT`, `WIP_STAGNANT`, `FLOWING`), Flow Inventory counters, and 14-day barcode activity. Add unit tests for telemetry math.

Depends On: None

Repository: cakra

Completion Criteria:
- DTO records defined in `Cakra.Modules.WorkPackage.Models`.
- `WorkPackageTelemetryCalculator` implements:
  - Working days calculation excluding Saturdays and Sundays.
  - Required Daily Burn and Capacity Share math benchmarked to $C_{\text{org}}$.
  - `UNPLANNED` pressure tier assignment when `Deadline == null`.
  - Observable Health Invariant evaluation with severity precedence.
  - Flow Inventory counters and 14-day barcode generation.
- Unit tests in `Cakra.Tests.Unit` verify all edge cases (null deadline, zero remaining days, zero blockers, dormant thresholds).

Notes:
- Defined DTO models in `Cakra.Modules.WorkPackage.Models`: `OperationsCockpitDto`, `PortfolioMetricsDto`, `PressureHealthMatrixDto`, `WorkPackageTelemetryDto`, `FlowBarcodeDayDto`, and string constant registries `WorkPackagePressureTiers` and `WorkPackageHealthStates` (Architecture CR-025 §4 TD-002, TD-003, TD-005).
- Implemented `IWorkPackageTelemetryCalculator` and `WorkPackageTelemetryCalculator` in `Cakra.Modules.WorkPackage.Services`:
  - Working days calculation excluding weekends (`ComputeWorkingDays`).
  - Elapsed business days calculation with exact fractional handling for dormancy (`ComputeBusinessDays`).
  - Required Daily Burn and Org Capacity Share % benchmarked to $C_{\text{org}}$ with safety floor.
  - Capacity Share tier mapping (`UNPLANNED`, `NOMINAL`, `ELEVATED`, `CRITICAL`, `IMPOSSIBLE`).
  - Observable Health Invariants evaluation with strict severity precedence (`DEADLINE_BREACHED`, `ACTIVE_BLOCKERS`, `DORMANT`, `WIP_STAGNANT`, `FLOWING`).
  - Flow inventory counters (Active WIP, Oldest In-Flight age, 14d Outflow count) and 14-day activity barcode generator.
  - Full matrix aggregator `BuildMatrix` and portfolio metrics calculator `CalculatePortfolioMetrics`.
- Registered `WorkPackageTelemetryCalculator` in `WorkPackageModule.cs`.
- Created comprehensive unit tests in `tests/backend/Cakra.Tests.Unit/WorkPackage/WorkPackageTelemetryCalculatorTests.cs` (27 tests passing) verifying all math formulas and edge cases.
- Changed files:
  - `cakra/src/backend/Cakra.Modules.WorkPackage/Models/OperationsCockpitDto.cs` (created)
  - `cakra/src/backend/Cakra.Modules.WorkPackage/Models/WorkPackageTelemetryDto.cs` (created)
  - `cakra/src/backend/Cakra.Modules.WorkPackage/Services/IWorkPackageTelemetryCalculator.cs` (created)
  - `cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageTelemetryCalculator.cs` (created)
  - `cakra/src/backend/Cakra.Modules.WorkPackage/WorkPackageModule.cs` (modified)
  - `cakra/tests/backend/Cakra.Tests.Unit/WorkPackage/WorkPackageTelemetryCalculatorTests.cs` (created)

---

## P2 - Application Query & REST API Endpoint

Implementation Status: IMPLEMENTED
Review Status: GO

### P2-S03

Title: Implement GetOperationsCockpitQuery and Handler

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement `GetOperationsCockpitQuery` and `GetOperationsCockpitQueryHandler` in `Cakra.Modules.WorkPackage.Services`. The handler loads active Work Packages, queries $C_{\text{org}}$ via `IRequestQueryService`, loads linked requests in bulk, executes `WorkPackageTelemetryCalculator`, and assembles `OperationsCockpitDto` including portfolio aggregate load and the 2D matrix distribution.

Depends On: P1-S01, P1-S02

Repository: cakra

Completion Criteria:
- Query record `GetOperationsCockpitQuery() : IRequest<OperationsCockpitDto>` defined.
- Handler executes bulk loading without N+1 query patterns.
- Returns fully populated `OperationsCockpitDto` with matrix counts matching total active packages.
- Unit tests in `Cakra.Tests.Unit` verify handler assembly and matrix distribution logic.

Notes:
- Defined `GetOperationsCockpitQuery(DateTime? AsOfDateUtc = null) : IRequest<OperationsCockpitDto>` in `WorkPackageQueries.cs` (Architecture CR-025 §4 TD-005).
- Added `CalculateTelemetry(WorkPackageDto, IReadOnlyCollection<RequestDto>, double, DateTime?)` overload and `ComputeTelemetry` alias methods in `IWorkPackageTelemetryCalculator` and `WorkPackageTelemetryCalculator` to support both domain models and DTO workflows while strictly filtering out removed requests (`scopeItem.IsActive`).
- Implemented `GetOperationsCockpitQueryHandler` in `Cakra.Modules.WorkPackage.Services`:
  - Queries empirical demonstrated throughput $C_{\text{org}}$ via `IRequestQueryService.GetOrgDemonstratedDailyThroughputAsync(30, cancellationToken)` with safety floor 1.0.
  - Queries active work packages in bulk via `_workPackageQueryService.ListWorkPackagesAsync("ACTIVE", ...)`.
  - For each active package, resolves constituent active requests and computes full telemetry via `_telemetryCalculator.ComputeTelemetry(wp, activeRequests, cOrg, asOfDateUtc)`.
  - Aggregates the 2D Pressure × Health triage matrix via `_telemetryCalculator.BuildMatrix(telemetryList)`.
  - Computes portfolio metrics (aggregate load %, deadline counts, invariant violations count) via `_telemetryCalculator.CalculatePortfolioMetrics(telemetryList, cOrg)`.
  - Assembles and returns complete `OperationsCockpitDto`.
- Registered `GetOperationsCockpitQueryHandler` and `IRequestHandler<GetOperationsCockpitQuery, OperationsCockpitDto>` in `WorkPackageModule.cs`.
- Created comprehensive unit tests in `tests/backend/Cakra.Tests.Unit/WorkPackage/GetOperationsCockpitQueryTests.cs` (10 tests passing) covering zero-state initialization, multi-cell matrix distributions, portfolio metrics calculations, removed request filtering, inactive package filtering, safety floor handling, and explicit query as-of timestamps.
- Changed files:
  - `src/backend/Cakra.Modules.WorkPackage/Services/IWorkPackageTelemetryCalculator.cs`
  - `src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageTelemetryCalculator.cs`
  - `src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageQueries.cs`
  - `src/backend/Cakra.Modules.WorkPackage/Services/GetOperationsCockpitQueryHandler.cs`
  - `src/backend/Cakra.Modules.WorkPackage/WorkPackageModule.cs`
  - `tests/backend/Cakra.Tests.Unit/WorkPackage/GetOperationsCockpitQueryTests.cs`

---

### P2-S04

Title: Expose REST Endpoint GET /api/v1/work-packages/operations-cockpit

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Add endpoint `GET /api/v1/work-packages/operations-cockpit` to `WorkPackagesController.cs` in `Cakra.Api`, returning `OperationsCockpitDto` with status 200 OK. Add integration test coverage.

Depends On: P2-S03

Repository: cakra

Completion Criteria:
- Endpoint `GET /api/v1/work-packages/operations-cockpit` defined on `WorkPackagesController`.
- Protected by `[Authorize]` attribute.
- Returns HTTP 200 OK with `OperationsCockpitDto` payload.
- Integration test in `Cakra.Tests.Integration` verifies endpoint routing and schema compliance.

Notes:
- Exposed `GET /api/v1/work-packages/operations-cockpit` in `WorkPackagesController.cs` (`Cakra.Api`), protected by `[Authorize]`.
- Added support for optional query parameter `asOfDate` / `asOfDateUtc`, dispatching `GetOperationsCockpitQuery(resolvedAsOfDate)` via `IMediator`.
- Returns `Ok(result)` with `[ProducesResponseType(typeof(OperationsCockpitDto), StatusCodes.Status200OK)]`.
- Added `PressureHealthMatrixJsonConverter` to `OperationsCockpitDto.cs` to preserve canonical uppercase keys (`DEADLINE_BREACHED`, `ACTIVE_BLOCKERS`, etc. and `UNPLANNED`, `NOMINAL`, etc.) across global ASP.NET Core camelCase `DictionaryKeyPolicy` and allow case-insensitive deserialization.
- Added integration test assertions to `tests/backend/Cakra.Tests.Integration/WorkPackage/WorkPackagesControllerTests.cs`:
  - Verified 401 Unauthorized when unauthenticated for `/api/v1/work-packages/operations-cockpit`.
  - Added `GetOperationsCockpit_returns_200_OK_with_valid_telemetry_matrix_and_metrics` testing zero-state initialization, active work package telemetry inclusion, 2D matrix distribution, portfolio metrics accuracy, and `?asOfDate` / `?asOfDateUtc` query parameter routing.
- All integration tests passing (12 passed in WorkPackage suite).
- Changed files:
  - `src/backend/Cakra.Api/Controllers/WorkPackagesController.cs` (modified)
  - `src/backend/Cakra.Modules.WorkPackage/Models/OperationsCockpitDto.cs` (modified)
  - `tests/backend/Cakra.Tests.Integration/WorkPackage/WorkPackagesControllerTests.cs` (modified)

---

## P3 - Frontend API Client & Executive Operations Cockpit (SCR-WP-002)

Implementation Status: IMPLEMENTED
Review Status: GO

### P3-S05

Title: Implement Frontend Operations Cockpit API Client & Types

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Define TypeScript interfaces (`OperationsCockpitDto`, `PortfolioMetricsDto`, `PressureHealthMatrixDto`, `WorkPackageTelemetryDto`, `FlowBarcodeDayDto`) and client function `getOperationsCockpit()` in `src/frontend/Cakra.Web/src/api/workpackages.ts`.

Depends On: P2-S04

Repository: cakra

Completion Criteria:
- TypeScript models matching backend DTOs exported from `workpackages.ts`.
- Exported function `getOperationsCockpit(): Promise<OperationsCockpitDto>`.
- Type-check passes via `vue-tsc --noEmit`.

Notes:
- Defined TypeScript interfaces and types in `src/frontend/Cakra.Web/src/api/workpackages.ts`:
  - `PressureTier`: `'UNPLANNED' | 'NOMINAL' | 'ELEVATED' | 'CRITICAL' | 'IMPOSSIBLE'` and constant `PRESSURE_TIERS`.
  - `HealthState`: `'DEADLINE_BREACHED' | 'ACTIVE_BLOCKERS' | 'DORMANT' | 'WIP_STAGNANT' | 'FLOWING'` and constant `HEALTH_STATES`.
  - `FlowBarcodeDayDto`: `date`, `closedCount`, `stateMutationCount`, `blockedCount`.
  - `WorkPackageTelemetryDto`: `id`, `name`, `objective`, `status`, `ownerPersonId`, `ownerName`, `customerId`, `customerName`, `productId`, `productName`, `deadline`, `totalComplexity`, `completedComplexity`, `remainingComplexity`, `totalRequestsCount`, `remainingRequestsCount`, `workingDaysRemaining`, `requiredDailyBurn`, `orgCapacityShare`, `pressureTier`, `healthState`, `deadlineBreached`, `blockedRequestsCount`, `dormantDays`, `isDormant`, `activeWipCount`, `oldestActiveWipDays`, `isWipStagnant`, `outflow14dCount`, `flowBarcode`.
  - `PressureHealthMatrixDto`: `cells`, `rowTotals`, `columnTotals`.
  - `PortfolioMetricsDto`: `totalActiveWorkPackages`, `withDeadlineCount`, `withoutDeadlineCount`, `orgDailyThroughput`, `portfolioAggregateLoadPercentage`, `totalInvariantViolationsCount`.
  - `OperationsCockpitDto`: `portfolioMetrics`, `matrix`, `packages`.
- Implemented and exported API client function `getOperationsCockpit(asOfDate?: string | null): Promise<OperationsCockpitDto>` querying `GET /work-packages/operations-cockpit` via `httpClient`.
- Exported `getOperationsCockpit` in default `workPackageService` object.
- Verified TypeScript and Vue build via `npm run build` (`vue-tsc --noEmit && vite build`) with zero errors.
- Changed files:
  - `src/frontend/Cakra.Web/src/api/workpackages.ts` (modified)

---

### P3-S06

Title: Implement Executive Operations Cockpit Screen (SCR-WP-002) & Navigation

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Create `src/frontend/Cakra.Web/src/views/OperationsCockpitView.vue` (`SCR-WP-002`) featuring the PageHeader with live telemetry ribbon, interactive clickable 2D Pressure × Health Triage Matrix with drillable cells, exception-filtered operational feed, and collapsible nominal section. Add route `/operations/cockpit` in `router/index.ts` and sidebar navigation link in `App.vue`.

Depends On: P3-S05

Repository: cakra

Completion Criteria:
- Route `/operations/cockpit` registered in `router/index.ts` with `screenId: 'SCR-WP-002'`.
- Sidebar menu in `App.vue` includes "Operations Cockpit" under Operations (`data-testid="nav-operations-cockpit-link"`).
- Top ribbon displays $C_{\text{org}}$, Portfolio Load %, Active Packages, and With/Without Deadline counts.
- Interactive 2D Matrix renders all 5 rows and 5 columns; clicking any cell filters the feed below to packages in that cell.
- Clear Filters button restores the full exception view.
- Cold factual reporting; no automated advice is shown.

Notes:
- Registered route `/operations/cockpit` (`operations-cockpit`) in `src/frontend/Cakra.Web/src/router/index.ts` with lazy loading `() => import('@/views/OperationsCockpitView.vue')`, `requiresAuth: true`, and `screenId: 'SCR-WP-002'`.
- Added "Operations Cockpit" link in `src/frontend/Cakra.Web/src/App.vue` sidebar navigation under Operations (`data-testid="nav-operations-cockpit-link"`) with speedometer icon, and updated `currentScreenTitle` mapping.
- Created `src/frontend/Cakra.Web/src/views/OperationsCockpitView.vue` (`SCR-WP-002`):
  - Telemetry ribbon (`PageHeader` with `StatusStrip` and `#stats` slot) displaying Org Daily Throughput ($C_{\text{org}}$), Portfolio Load % (highlighted if $> 100\%$), Total Active Packages, With Deadline count, Without Deadline (Unplanned) count, and Total Invariant Violations count.
  - Interactive 2D Pressure × Health Triage Matrix table (`data-testid="triage-matrix"`):
    - 5 Pressure columns (`UNPLANNED`, `NOMINAL (< 20%)`, `ELEVATED (20% - 50%)`, `CRITICAL (50% - 100%)`, `IMPOSSIBLE (> 100%)`) plus Total.
    - 5 Health invariant rows (`DEADLINE BREACHED`, `ACTIVE BLOCKERS`, `DORMANT`, `WIP STAGNANT`, `FLOWING`) plus Total.
    - Clickable drilldown cells (`data-testid="matrix-cell-[Row]-[Col]"`) toggling matrix filter with visual selection ring and urgency heatmap tints.
    - Active filter banner (`data-testid="matrix-filter-banner"`) with "Filtered by: [Row] × [Column]" and Clear Filter button (`data-testid="clear-matrix-filter-btn"`, `data-testid="clear-filter-btn"`).
  - Filtered Operational Feed (`data-testid="cockpit-operational-feed"`):
    - Exception view defaulting to all invariant violations + impossible pressure packages sorted by capacity share / deadline breach severity.
    - Dense factual cards (`data-testid="cockpit-wp-card-[id]"`) showing Package ID, Name/Objective, Owner, Customer, Product, Scope Demand badge (`pts/day`), Pressure Tier badge, Capacity Share % tier, Remaining Runway (working days left or Breached), and Health Invariant pills.
    - 14-day flow activity barcode pulse with daily color indicators (closed, mutated, blocked, idle) and outflow counts.
    - Strictly cold factual reporting with zero automated advice.
  - Collapsible Nominal & Flowing section (`data-testid="nominal-packages-section"`) with toggle button (`data-testid="nominal-section-toggle"`).
- Verified production build via `npm run build` (`vue-tsc --noEmit && vite build`) and backend tests via `dotnet test` with 100% passing tests.
- Changed files:
  - `src/frontend/Cakra.Web/src/router/index.ts` (modified)
  - `src/frontend/Cakra.Web/src/App.vue` (modified)
  - `src/frontend/Cakra.Web/src/views/OperationsCockpitView.vue` (created)

---

## P4 - Work Package Screen Modernization & Workbench (SCR-WP-001)

Implementation Status: IMPLEMENTED
Review Status: GO

### P4-S07

Title: Modernize Work Package Cards & 14-Day Flow Barcode on SCR-WP-001

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Update `WorkPackageView.vue` (`SCR-WP-001`) list table and cards to render Scope Demand badges (`UNPLANNED` or Capacity Share tier), Health Invariant pills (Blockers, Dormancy), Flow Inventory stats (Active WIP, Oldest In-Flight Age, 14d Outflow), and 14-day Flow Activity Barcodes.

Depends On: P3-S05

Repository: cakra

Completion Criteria:
- Cards/table rows on `SCR-WP-001` render Capacity Share tier badges (`UNPLANNED`, `NOMINAL`, `ELEVATED`, `CRITICAL`, `IMPOSSIBLE`).
- Health Invariant pills render observable states (e.g., `! 1 Blocked`, `Dormant: 7d`).
- 14-day Flow Activity Barcode renders daily event pulses.
- All existing test selectors on `WorkPackageView.vue` preserved intact.

Notes:
- Imported `getOperationsCockpit()` and types (`WorkPackageTelemetryDto`, `FlowBarcodeDayDto`) in `src/frontend/Cakra.Web/src/views/WorkPackageView.vue`.
- Added reactive operations telemetry map (`telemetryByPackageId`, `isLoadingTelemetry`) and `viewMode` toggle (`table` | `cards`).
- Enhanced `loadWorkPackages()` to fetch and merge live operations cockpit telemetry in parallel via `Promise.all([httpClient.get(...), fetchTelemetry()])`.
- Implemented Scope Demand badges with semantic styling (`UNPLANNED` grey/neutral, `NOMINAL` grey, `ELEVATED` blue, `CRITICAL` orange, `IMPOSSIBLE` red) and capacity share formatting (`[X% Org Output]`).
- Implemented Health Invariant pills rendering observable factual states: `! Deadline Breached`, `! X Blocked`, `Dormant: Xd`, `WIP: X`, `Flowing`.
- Implemented Flow Inventory counters (`Active WIP`, `Oldest In-Flight Age`, `14d Outflow`).
- Implemented 14-day Flow Activity Barcode strips rendering daily event segments (green for closed requests, blue for state mutations, red for blocked requests, grey for idle days) with interactive tooltips in both table rows and modern cards.
- Added comprehensive Operational Telemetry & Flow Invariants spotlight section to the Detail Workbench (`data-testid="detail-telemetry-spotlight"`).
- Strictly preserved all existing test selectors (`work-packages-table`, `work-package-row`, `work-package-objective-cell`, `work-package-id-link`, `wp-overdue-badge`, `work-package-status-badge`, etc.).
- Verified production build via `npm run build` (`vue-tsc --noEmit && vite build`) passing cleanly with 0 errors.

Changed files:
- `src/frontend/Cakra.Web/src/views/WorkPackageView.vue`

---

### P4-S08

Title: Implement Detail Drawer Request Aging & Interactive Decision Workbench

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Enhance the Detail Drawer in `WorkPackageView.vue` (`SCR-WP-001`) with request state aging, blocker tracking, and the interactive Decision Workbench allowing live browser simulation of de-scoping selected requests or extending the target deadline.

Depends On: P4-S07

Repository: cakra

Completion Criteria:
- Scope requests table displays Complexity rating, Status, and State Aging in days.
- Paused/blocked requests display blocker notes.
- Decision Workbench allows toggling requests to simulate de-scoping, live-updating Remaining Complexity and Org Capacity Share.
- Decision Workbench date picker simulates deadline extension, live-updating Required Daily Burn and Org Capacity Share.

Notes:
- Enhanced `WorkPackageRequestDto` and `WorkPackageScopeItem` models with `Complexity`, `BlockerNote`, and `Request` properties.
- Updated `requests.ts` with `RequestAssignmentDto` and `assignments` collection on `RequestDto`.
- Updated constituent linked requests list inside Detail Drawer on `WorkPackageView.vue`:
  * Rendered numerical Complexity rating badge (`data-testid="request-complexity-badge"`).
  * Rendered Status badge (`data-testid="request-status-badge"`), updated with distinct semantic styling for `PAUSED` (`text-bg-danger`) and `ASSIGNED` (`text-bg-info`).
  * Rendered observable State Aging in days (`data-testid="request-aging-badge"`), computing elapsed days since status transition (`updatedAt` or `createdAt`).
  * Rendered Blocker badge (`data-testid="request-blocker-badge"`) and Blocker Note (`data-testid="request-blocker-note"`) whenever request status is `PAUSED`.
  * Rendered interactive de-scope simulation checkboxes (`data-testid="descope-request-checkbox"`) on each uncompleted linked request.
- Implemented dedicated interactive Decision Workbench section (`data-testid="decision-workbench"`):
  * Integrated date picker to simulate target deadline extension (`data-testid="simulate-deadline-input"`), recalculating working days remaining, Required Daily Burn, Org Capacity Share, and Pressure Tier in real time.
  * Integrated de-scope simulation counter and request checklist (`data-testid="descope-request-checkbox"`), dynamically deducting deselected complexity from remaining complexity.
  * Rendered real-time simulation metric readouts: Remaining Complexity (`data-testid="simulated-remaining-complexity"`), Required Daily Burn (`data-testid="simulated-daily-burn"`), and Org Capacity Share (`data-testid="simulated-org-capacity-share"`).
  * Rendered live comparison display (`data-testid="decision-simulation-comparison"`): e.g. `Simulated: X pts (-Y pts descope) | Z pts/day -> W% Org Output [TIER]`, with baseline comparison comparison footnote.
  * Added Clear/Reset Simulation button (`data-testid="reset-simulation-button"`).
- Verified production build via `npm run build` (`vue-tsc --noEmit && vite build`) in `src/frontend/Cakra.Web` passing cleanly with 0 errors.
- Verified backend unit tests via `dotnet test` passing with 100% success rate (524 passed).

Changed files:
- `src/frontend/Cakra.Web/src/views/WorkPackageView.vue`
- `src/frontend/Cakra.Web/src/api/workpackages.ts`
- `src/frontend/Cakra.Web/src/api/requests.ts`
- `src/backend/Cakra.Modules.WorkPackage/Models/WorkPackageDto.cs`
- `docs/implementation-plan/CR-025-IMPLEMENTATION-PLAN.md`

---

## P5 - Full-Stack Verification & Regression Validation

Implementation Status: IMPLEMENTED
Review Status: GO

### P5-S09

Title: Full-Stack Test Suite Execution & Production Build Validation

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Execute the complete backend test suite (`dotnet test`) and frontend production build (`npm run build`) ensuring all unit/integration tests pass and frontend bundles compile cleanly without errors.

Depends On: P3-S06, P4-S08

Repository: cakra

Completion Criteria:
- `dotnet test Cakra.sln` completes with 100% pass rate.
- `npm run build` in `src/frontend/Cakra.Web` succeeds with 0 errors.
- System integrity preserved.

Notes:
- Executed complete backend test suite `dotnet test cakra/Cakra.sln`:
  - `Cakra.Tests.Unit`: 524 passed, 0 failed, 0 skipped (duration: 1s).
  - `Cakra.Tests.Integration`: 181 passed, 0 failed, 0 skipped (duration: 17s).
  - Total: 705 passed, 0 failed (100% pass rate).
- Executed frontend production build in `cakra/src/frontend/Cakra.Web` (`npm run build` running `vue-tsc --noEmit && vite build`):
  - TypeScript type checking (`vue-tsc --noEmit`): 0 errors.
  - Vite production bundling: 182 modules transformed, built in 1.18s with 0 errors.
- Confirmed full-stack system regression integrity across backend and frontend.

Changed files:
- `docs/implementation-plan/CR-025-IMPLEMENTATION-PLAN.md`

---

# 6. Change Log

- 2026-10-07: Initial implementation plan created and approved for `CR-025` with 5 phases and 9 execution slices.
