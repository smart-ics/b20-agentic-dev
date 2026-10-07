---
Title: Work Package Operational Visualization, Demand Density, and Executive Operations Cockpit Architecture (CR-025)
Code: CR-025
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-07
---

# 1. Overview

This architecture defines the technical realization for Change Request `CR-025`: Work Package Operational Visualization, Demand Density (Scope Pressure benchmarked against Org Demonstrated Daily Throughput), Observable Operational Health Invariants, and the Executive Operations Cockpit (`SCR-WP-002`) in Cakra.

It consumes and realizes the approved feasibility decisions from [CR-025-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-025-FEASIBILITY-ASSESSMENT.md), establishing:

1. Cross-module empirical throughput query contract in `Cakra.Modules.Request` computing Organization 30-Day Demonstrated Daily Output ($C_{\text{org}}$) across completed requests.
2. Structural demand density engine in `Cakra.Modules.WorkPackage` computing Scope Pressure (Required Daily Burn) and Org Capacity Share ($\% \text{ of } C_{\text{org}}$), with strict classification of dateless packages as `UNPLANNED`.
3. Observable Operational Health Invariants evaluation engine deriving factual states (Deadline Breach, Active Blockers, Dormancy, WIP Stagnancy, Flowing) without subjective estimation.
4. Unified Operations Cockpit REST API endpoint `GET /api/v1/work-packages/operations-cockpit` in `WorkPackagesController.cs` returning portfolio macro metrics, 2D matrix cell counts, and Work Package telemetry models.
5. Executive Operations Cockpit screen `SCR-WP-002` (`OperationsCockpitView.vue` at `/operations/cockpit`) featuring the interactive 2D Pressure × Health Triage Matrix with drillable cells.
6. Modernized Work Package Card on `SCR-WP-001` (`WorkPackageView.vue`) displaying Capacity Share tiers, Health Invariant pills, Flow Inventory facts, and 14-day Flow Activity Barcodes.
7. Enhanced Work Package Detail Workbench on `SCR-WP-001` providing request state aging, blocker visibility, and an interactive de-scoping and deadline simulator.
8. Zero database schema migrations: all required entities, complexity ratings, statuses, and timestamps already exist in the database.

---

# 2. Architectural Basis

## Business Context

- ISSUE: [CR-025-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-025-ISSUE.md)
- DOMAIN SPECIFICATION: [work-package-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/work-package-domain.md)
- DOMAIN SPECIFICATION: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- ARCHITECTURAL SPECIFICATION: [work-package-operational-visualization-architecture.md](file:///C:/Users/drury/.gemini/antigravity/brain/ba7d9357-e8ff-4ef1-a8d2-4373df8bf458/work-package-operational-visualization-architecture.md)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-025-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-025-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

This architecture consumes and realizes the approved decisions:
- `GAP-001`: Empirical Organization Throughput ($C_{\text{org}}$) calculation via `IRequestQueryService`.
- `GAP-002`: Scope Pressure and Org Capacity Share calculations with `UNPLANNED` handling.
- `GAP-003`: Observable Operational Health Invariants evaluation engine.
- `GAP-004`: Cockpit query endpoint `GET /api/v1/work-packages/operations-cockpit`.
- `GAP-005`: Executive Operations Cockpit screen (`SCR-WP-002`) and route `/operations/cockpit`.
- `GAP-006`: Modernized Work Package card indicators and 14-day flow barcode on `SCR-WP-001`.
- `GAP-007`: Detail drawer request state aging and interactive decision workbench on `SCR-WP-001`.
- Closed decisions `OQ-001` through `OQ-006`: Rejection of progress % and velocity, Org Capacity Share benchmark, UNPLANNED pressure class, flow inventory facts over "thrashing" diagnosis, Pressure × Health Matrix as navigation, and dedicated cockpit screen (Option A).

---

# 3. Scope

## Included

1. **Request Module (`Cakra.Modules.Request`)**:
   - Query method `GetOrgDemonstratedDailyThroughputAsync(int windowDays = 30, CancellationToken cancellationToken = default)` on `IRequestQueryService` and `RequestQueryService.cs`.
   - Optimized SQL aggregation query in `RequestRepository.cs` summing completed request complexity over rolling 30 days.
2. **Work Package Module (`Cakra.Modules.WorkPackage`)**:
   - Models: `OperationsCockpitDto`, `WorkPackageTelemetryDto`, `PortfolioMetricsDto`, `PressureHealthMatrixDto`, `FlowBarcodeDayDto`.
   - Query: `GetOperationsCockpitQuery` and handler `GetOperationsCockpitQueryHandler`.
   - Telemetry computation service: `WorkPackageTelemetryCalculator` evaluating Scope Pressure, Capacity Share tiers, Health Invariants, Flow Inventory counters, and 14-day barcode activity.
3. **API Layer (`Cakra.Api`)**:
   - REST endpoint `GET /api/v1/work-packages/operations-cockpit` in `WorkPackagesController.cs`.
4. **Frontend Architecture (`Cakra.Web`)**:
   - API helper functions and TypeScript interfaces in `src/frontend/Cakra.Web/src/api/workpackages.ts`:
     - `getOperationsCockpit(): Promise<OperationsCockpitDto>`
     - Types: `OperationsCockpitDto`, `WorkPackageTelemetryDto`, `PortfolioMetricsDto`, `PressureHealthMatrixDto`.
   - Screen `OperationsCockpitView.vue` (`SCR-WP-002`):
     - Route `/operations/cockpit` in `src/frontend/Cakra.Web/src/router/index.ts`.
     - Navigation link in `App.vue` under "Operations" menu.
     - Portfolio macro metrics ribbon (`StatusStrip`).
     - Interactive 2D Pressure × Health Triage Matrix with clickable cell filters.
     - Filtered exception feed of Work Packages with operational invariant violations.
     - Collapsible list for nominal/flowing packages.
   - Screen `WorkPackageView.vue` (`SCR-WP-001`) Modernization:
     - Work Package Card: Scope Demand badge, Org Capacity Share tier, Health Invariant pills, Flow Inventory facts, 14-day Flow Activity Barcode.
     - Detail Drawer: Request state aging, blocker tracking, and interactive Decision Workbench (de-scoping and deadline simulator).

## Excluded

- Modifying the underlying database schema (all tables and columns already exist).
- Storing calculated telemetry in database tables (all telemetry is calculated on read).
- Modifying Request lifecycle mutation rules or Work Package mutation rules.
- Implementing automated algorithmic decision recommendations.

---

# 4. Technical Decisions

## TD-001: Empirical Organization Throughput Contract ($C_{\text{org}}$)

To ground demand density in observable reality without subjective estimates, `IRequestQueryService` is extended with:

```csharp
public interface IRequestQueryService
{
    // Existing query methods...
    
    /// <summary>
    /// Computes the empirical organization-wide daily throughput (C_org) across all completed requests
    /// within the specified rolling window (Architecture CR-025 TD-001).
    /// </summary>
    /// <param name="windowDays">Rolling window in calendar days (defaults to 30).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Demonstrated daily complexity burn rate (minimum safety floor of 1.0).</returns>
    Task<double> GetOrgDemonstratedDailyThroughputAsync(
        int windowDays = 30,
        CancellationToken cancellationToken = default);
}
```

### Implementation Logic in `RequestQueryService`:
1. Executes a single Dapper scalar query:
   ```sql
   SELECT COALESCE(SUM(Complexity), 0)
   FROM [request].[Requests]
   WHERE Status = 'COMPLETED'
     AND UpdatedAt >= DATEADD(day, -@WindowDays, @UtcNow);
   ```
2. Divides the sum by `windowDays`.
3. If the computed throughput is $\le 0.0$ (e.g. fresh environment), returns a system safety floor of `1.0` pt/day to prevent division-by-zero while preserving mathematical sanity.

---

## TD-002: Scope Pressure & Org Capacity Share Calculation

Work Package Scope Pressure represents required daily work density and is benchmarked against $C_{\text{org}}$:

### Mathematical Definition:
1. **Unplanned Work Packages (`Deadline == null`)**:
   - `WorkingDaysRemaining = null`
   - `RequiredDailyBurn = null`
   - `OrgCapacityShare = null`
   - `PressureTier = "UNPLANNED"`
2. **Dated Work Packages (`Deadline != null`)**:
   - `WorkingDaysRemaining = ComputeWorkingDays(TodayUtc, Deadline.Value.Date)`
     *(Standard 5-day business week excluding Saturdays and Sundays)*.
   - If `Deadline < TodayUtc`: `WorkingDaysRemaining = 0` (Breached).
   - `RemainingComplexity = sum of Complexity of constituent requests with Status != 'COMPLETED' and Status != 'CANCELLED'`.
   - `RequiredDailyBurn`:
     - If `WorkingDaysRemaining <= 0`: `RequiredDailyBurn = (double)RemainingComplexity`
     - If `WorkingDaysRemaining > 0`: `RequiredDailyBurn = (double)RemainingComplexity / WorkingDaysRemaining`
   - `OrgCapacityShare = (RequiredDailyBurn / C_org) * 100.0`
   - `PressureTier`:
     - `< 20.0%`: `NOMINAL`
     - `20.0% - 50.0%`: `ELEVATED`
     - `50.0% - 100.0%`: `CRITICAL`
     - `> 100.0%`: `IMPOSSIBLE`

---

## TD-003: Observable Operational Health Invariants Engine

Every Work Package is evaluated against five factual conditions derived strictly from entity states and domain timestamps:

```text
+---------------------------------------------------------------------------------------------------------+
|                                  OPERATIONAL HEALTH INVARIANTS                                          |
+---------------------------------------------------------------------------------------------------------+
| 1. DEADLINE BREACHED  | Deadline != null && WorkingDaysRemaining <= 0 && RemainingRequestsCount > 0    |
+-----------------------+---------------------------------------------------------------------------------+
| 2. ACTIVE BLOCKERS    | Count of constituent Requests with Status == 'PAUSED' > 0                       |
+-----------------------+---------------------------------------------------------------------------------+
| 3. DORMANT            | Business days since latest (WP.UpdatedAt, max(Requests.UpdatedAt)) > 5 days     |
+-----------------------+---------------------------------------------------------------------------------+
| 4. WIP STAGNANT       | Any constituent Request in Status == 'IN_PROGRESS' with age in state > 14 days  |
+-----------------------+---------------------------------------------------------------------------------+
| 5. FLOWING            | !DeadlineBreached && !ActiveBlockers && !Dormant && !WipStagnant                |
+---------------------------------------------------------------------------------------------------------+
```

### Primary Health State Classification:
To assign a package to a single row in the 2D Triage Matrix, the most severe invariant violation takes precedence:
1. `DEADLINE_BREACHED` (Highest severity)
2. `ACTIVE_BLOCKERS`
3. `DORMANT`
4. `WIP_STAGNANT`
5. `FLOWING` (Nominal state)

---

## TD-004: Flow Inventory Facts & 14-Day Activity Barcode

The system discards speculative trendlines and subjective labels ("Thrashing") in favor of cold inventory metrics and a 14-day event barcode:

1. **Active WIP Count**: Count of constituent requests currently in `Status == 'IN_PROGRESS'`.
2. **Oldest In-Flight Age**: Maximum elapsed calendar days among active `IN_PROGRESS` requests.
3. **Recent Outflow (14d)**: Count of constituent requests transitioned to `Status == 'COMPLETED'` in the past 14 days.
4. **14-Day Activity Barcode**:
   - An array of 14 day records (from $T - 13$ days to Today).
   - Each day contains:
     - `Date`: formatted string (`YYYY-MM-DD`).
     - `ClosedCount`: number of requests completed on that date.
     - `StateMutationCount`: number of requests updated or state-transitioned on that date.
     - `BlockedCount`: number of requests paused on that date.

---

## TD-005: Operations Cockpit Read Model & API Endpoint

A dedicated query endpoint `GET /api/v1/work-packages/operations-cockpit` provides complete portfolio telemetry in a single roundtrip.

### DTO Schemas (`Cakra.Modules.WorkPackage.Models`):

```csharp
public sealed record OperationsCockpitDto
{
    public PortfolioMetricsDto PortfolioMetrics { get; init; } = new();
    public PressureHealthMatrixDto Matrix { get; init; } = new();
    public IReadOnlyList<WorkPackageTelemetryDto> Packages { get; init; } = Array.Empty<WorkPackageTelemetryDto>();
}

public sealed record PortfolioMetricsDto
{
    public int TotalActiveWorkPackages { get; init; }
    public int WithDeadlineCount { get; init; }
    public int WithoutDeadlineCount { get; init; }
    public double OrgDailyThroughput { get; init; }
    public double PortfolioAggregateLoadPercentage { get; init; }
    public int TotalInvariantViolationsCount { get; init; }
}

public sealed record PressureHealthMatrixDto
{
    // Dictionary mapping Row (HealthState) -> Column (PressureTier) -> Count
    // Rows: DEADLINE_BREACHED, ACTIVE_BLOCKERS, DORMANT, WIP_STAGNANT, FLOWING
    // Cols: UNPLANNED, NOMINAL, ELEVATED, CRITICAL, IMPOSSIBLE
    public Dictionary<string, Dictionary<string, int>> Cells { get; init; } = new();
    public Dictionary<string, int> RowTotals { get; init; } = new();
    public Dictionary<string, int> ColumnTotals { get; init; } = new();
}

public sealed record WorkPackageTelemetryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Objective { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid OwnerPersonId { get; init; }
    public string? OwnerName { get; init; }
    public Guid? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public Guid? ProductId { get; init; }
    public string? ProductName { get; init; }
    public DateTime? Deadline { get; init; }
    
    // Scope & Pressure
    public int TotalComplexity { get; init; }
    public int CompletedComplexity { get; init; }
    public int RemainingComplexity { get; init; }
    public int TotalRequestsCount { get; init; }
    public int RemainingRequestsCount { get; init; }
    public int? WorkingDaysRemaining { get; init; }
    public double? RequiredDailyBurn { get; init; }
    public double? OrgCapacityShare { get; init; }
    public string PressureTier { get; init; } = "UNPLANNED"; // UNPLANNED, NOMINAL, ELEVATED, CRITICAL, IMPOSSIBLE

    // Operational Health Invariants
    public string HealthState { get; init; } = "FLOWING"; // DEADLINE_BREACHED, ACTIVE_BLOCKERS, DORMANT, WIP_STAGNANT, FLOWING
    public bool DeadlineBreached { get; init; }
    public int BlockedRequestsCount { get; init; }
    public double DormantDays { get; init; }
    public bool IsDormant { get; init; }
    public int ActiveWipCount { get; init; }
    public double OldestActiveWipDays { get; init; }
    public bool IsWipStagnant { get; init; }
    public int Outflow14dCount { get; init; }

    // 14-day flow barcode
    public IReadOnlyList<FlowBarcodeDayDto> FlowBarcode { get; init; } = Array.Empty<FlowBarcodeDayDto>();
}

public sealed record FlowBarcodeDayDto
{
    public string Date { get; init; } = string.Empty;
    public int ClosedCount { get; init; }
    public int StateMutationCount { get; init; }
    public int BlockedCount { get; init; }
}
```

---

## TD-006: Dedicated Executive Screen (`SCR-WP-002`, `OperationsCockpitView.vue`)

A dedicated Vue 3 screen component is implemented at `/operations/cockpit`:

1. **Route Definition (`src/frontend/Cakra.Web/src/router/index.ts`)**:
   ```typescript
   {
     path: '/operations/cockpit',
     name: 'operations-cockpit',
     component: () => import('@/views/OperationsCockpitView.vue'),
     meta: {
       requiresAuth: true,
       screenId: 'SCR-WP-002',
     },
   }
   ```
2. **Sidebar Navigation (`src/frontend/Cakra.Web/src/App.vue`)**:
   - Placed in the "Operations" section of the left sidebar alongside `Operational Feed`, `Work in Progress`, `My Requests`, and `Work Packages`.
   - Test selector: `data-testid="nav-operations-cockpit-link"`.
3. **Core Screen Structure**:
   - **PageHeader & Ribbon**: Shows $C_{\text{org}}$, Portfolio Load % (alert style if $> 100\%$), Active Package Count, and With/Without Deadline counts.
   - **Interactive 2D Matrix Component**:
     - Columns: `UNPLANNED`, `NOMINAL (<20%)`, `ELEVATED (20-50%)`, `CRITICAL (50-100%)`, `IMPOSSIBLE (>100%)`.
     - Rows: `DEADLINE BREACHED`, `ACTIVE BLOCKERS`, `DORMANT (>5d)`, `WIP STAGNANT (>14d)`, `FLOWING`.
     - Cells render integer counts. Clicking any cell toggles a filter filtering the feed below to packages in that cell.
   - **Filtered Operational Feed**:
     - Lists packages matching the active filter, sorted by Capacity Share descending.
     - Displays dense cards highlighting Invariant Violations and Flow Inventory facts.
     - Pure factual reporting; no automated "advice" is rendered.

---

## TD-007: Modernized Work Package Card & Detail Workbench on `SCR-WP-001`

1. **Card Modernization**:
   - Displays Scope Demand badge: `5.8 pts/day | 112% Org Output [IMPOSSIBLE]` (or `[UNPLANNED]`).
   - Displays Health Invariant pills: `! 1 Blocked`, `Dormant: 3.5d`, `WIP: 4 (Oldest: 11d)`.
   - Displays 14-day Flow Barcode: visual strip of 14 segments representing daily activity.
2. **Detail Drawer Decision Workbench**:
   - Request table shows: Title, Complexity rating, Status, and Days in Current Status.
   - Interactive Decision Simulator:
     - Checkbox next to each remaining request: checking simulates removing that request from scope and recalculates Remaining Complexity, Required Daily Burn, and Org Capacity Share in real time.
     - Date input to simulate target deadline extension: recalculates Required Daily Burn and Org Capacity Share in real time.

---

## TD-008: Zero Database Schema Migrations

Inspection of existing tables confirms that:
- `[workpackage].[WorkPackages]` already contains `[Id]`, `[Name]`, `[Objective]`, `[Status]`, `[OwnerPersonId]`, `[CustomerId]`, `[ProductId]`, `[Deadline]`, `[CreatedAt]`, `[UpdatedAt]`.
- `[workpackage].[WorkPackageRequests]` already contains `[WorkPackageId]`, `[RequestId]`, `[SortOrder]`, `[AddedAt]`, `[RemovedAt]`.
- `[request].[Requests]` already contains `[Complexity]`, `[Status]`, `[CreatedAt]`, `[UpdatedAt]`, `[OwnerPersonId]`.

No SQL table alterations, column additions, or database migrations are required. All metrics and matrices are calculated in application query handlers using read-model aggregations.

---

# 5. Component Responsibilities

| Component | Responsibility |
|---|---|
| `IRequestQueryService` / `RequestQueryService` | Computes 30-day aggregate organizational throughput ($C_{\text{org}}$) across completed requests. |
| `RequestRepository` | Executes optimized scalar Dapper query for completed complexity sum within rolling time window. |
| `WorkPackageTelemetryCalculator` | Calculates working days, Required Daily Burn, Org Capacity Share, Health Invariants, Flow Inventory, and 14-day barcodes. |
| `GetOperationsCockpitQueryHandler` | Orchestrates queries across Work Package repository and Request query service, assembling `OperationsCockpitDto`. |
| `WorkPackagesController` | Exposes REST endpoint `GET /api/v1/work-packages/operations-cockpit`. |
| `workpackages.ts` | Frontend API client providing `getOperationsCockpit()` and TypeScript data models. |
| `OperationsCockpitView.vue` (`SCR-WP-002`) | Renders executive dashboard, portfolio macro ribbon, 2D clickable Matrix, and filtered exception feed. |
| `WorkPackageView.vue` (`SCR-WP-001`) | Renders modernized operational cards, 14-day flow barcodes, request state aging, and the interactive Decision Workbench. |
| `App.vue` & `router/index.ts` | Configures route `/operations/cockpit` and renders sidebar navigation link under Operations. |

---

# 6. Integration Design

```text
[OperationsCockpitView.vue] (SCR-WP-002)
        │
        │ HTTP GET /api/v1/work-packages/operations-cockpit
        ▼
[WorkPackagesController]
        │
        │ Send(GetOperationsCockpitQuery)
        ▼
[GetOperationsCockpitQueryHandler]
        │
        ├──► [IWorkPackageRepository.ListAsync(Status: "ACTIVE")]
        │
        ├──► [IRequestQueryService.GetOrgDemonstratedDailyThroughputAsync(30)]
        │           │
        │           ▼
        │    [RequestRepository (SQL Sum on [request].[Requests])]
        │
        ├──► [IRequestQueryService.GetRequestsByWorkPackagesAsync(activeWpIds)]
        │
        ▼
[WorkPackageTelemetryCalculator]
        │
        ▼
Assemble OperationsCockpitDto (PortfolioMetrics, 2D Matrix, Telemetry Cards)
        │
        ▼
[OperationsCockpitView.vue] (Interactive 2D Matrix Filtering & Exception Triage)
```

---

# 7. Data Ownership

| Data Concept | Authoritative Owner | Read Projection Consumer |
|---|---|---|
| Request Complexity & Status | `Cakra.Modules.Request` (`[request].[Requests]`) | `WorkPackageTelemetryCalculator` |
| Organization Throughput ($C_{\text{org}}$) | `Cakra.Modules.Request` (Completed Request history) | `GetOperationsCockpitQueryHandler` |
| Work Package Metadata & Deadline | `Cakra.Modules.WorkPackage` (`[workpackage].[WorkPackages]`) | `WorkPackagesController`, `WorkPackageView.vue` |
| Scope Membership & Ordering | `Cakra.Modules.WorkPackage` (`[workpackage].[WorkPackageRequests]`) | `WorkPackageQueryService` |
| Operations Cockpit Read Model | `Cakra.Modules.WorkPackage` (Transient Calculation) | `OperationsCockpitView.vue` (`SCR-WP-002`) |

---

# 8. Database Design

## New Tables
None.

## Modified Tables
None.

## Relationships
None (Preserves strict DDD decoupled schema boundaries between `[workpackage]` and `[request]`).

## Migration Considerations
Zero schema migrations required.

---

# 9. Cross-Cutting Concerns

1. **Security & Authorization**:
   - `GET /api/v1/work-packages/operations-cockpit` requires standard authentication (`[Authorize]`).
   - Accessible by all authenticated operational and management roles (`Programmer`, `Team Lead`, `Manager`, `Administrator`).
2. **Performance & Query Optimization**:
   - The cockpit query must execute in a single roundtrip.
   - Active work packages and their constituent requests are loaded in bulk using existing batch queries rather than N+1 queries.
   - $C_{\text{org}}$ throughput is computed via a single indexed aggregation query on `[request].[Requests]`.
3. **Observability & Logging**:
   - Handlers log telemetry calculations at `Debug` level and macro load anomalies at `Information` level:
     `"Computed Operations Cockpit: {ActiveWpCount} packages, C_org: {OrgThroughput}, Portfolio Load: {PortfolioLoad}%"`.

---

# 10. Implementation Constraints

1. **No External Charting Libraries**: The 2D Pressure × Health Triage Matrix and Flow Activity Barcodes must be built using native Vue 3 and Bootstrap 5 utilities (HTML tables, CSS flexbox, semantic badges), preserving zero-dependency lightweight performance.
2. **Factual Purity**: The UI must never output automated algorithmic recommendations ("COO Decision"). It must only present observable operational facts.
3. **Safe Mathematical Guards**: All division operations ($C_{\text{org}}$, working days) must incorporate strict guards against division-by-zero, returning safe fallbacks.
4. **Preserve Test Selectors**: All existing automated test selectors on `WorkPackageView.vue` (`SCR-WP-001`) must be retained intact. New selectors (`data-testid="operations-cockpit-matrix"`, etc.) must follow project conventions.

---

# 11. Acceptance Conditions

1. **Backend Throughput**: Calling `GetOrgDemonstratedDailyThroughputAsync` returns the actual average daily completed complexity over the past 30 days, or `1.0` if no requests were closed.
2. **Backend Cockpit API**: Endpoint `GET /api/v1/work-packages/operations-cockpit` returns HTTP 200 with accurate `PortfolioMetrics`, 2D `Matrix` counts matching total active packages, and `Packages` telemetry.
3. **UNPLANNED Handling**: Active Work Packages with `Deadline == null` are categorized with `PressureTier = "UNPLANNED"` and populated into the UNPLANNED column in the matrix.
4. **Health Invariants Accuracy**:
   - Overdue active packages are classified as `DEADLINE_BREACHED`.
   - Packages with paused requests are classified as `ACTIVE_BLOCKERS`.
   - Packages inactive $> 5$ business days are classified as `DORMANT`.
   - Packages with requests in-progress $> 14$ days are classified as `WIP_STAGNANT`.
   - Packages with zero violations are classified as `FLOWING`.
5. **Executive Screen (`SCR-WP-002`)**:
   - Navigating to `/operations/cockpit` loads the cockpit.
   - Sidebar contains "Operations Cockpit" under "Operations".
   - Clicking any matrix cell filters the package feed to packages in that cell; clicking "Clear Filter" restores the full exception list.
6. **Work Package Modernization on `SCR-WP-001`**:
   - Cards display Demand Density badges, Health Invariant pills, and 14-day flow barcodes.
   - Detail drawer displays request state aging and the interactive Decision Simulator.
