---
Title: Feasibility Assessment for Work Package Operational Visualization, Demand Density, and Executive Operations Cockpit (CR-025)
Code: CR-025
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-07
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Change being assessed: Implementation of a first-principles operational visualization system, demand density measurement (Scope Pressure benchmarked against Org Demonstrated Daily Throughput), observable operational health invariants (Deadline Breach, Active Blockers, Dormancy, WIP Stagnancy), and an Executive Operations Cockpit featuring an interactive Pressure × Health Triage Matrix, as formally captured in [CR-025-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-025-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-025-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-025-ISSUE.md)
- DOMAIN: [work-package-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/work-package-domain.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- ARCHITECTURAL SPECIFICATION: [work-package-operational-visualization-architecture.md](file:///C:/Users/drury/.gemini/antigravity/brain/ba7d9357-e8ff-4ef1-a8d2-4373df8bf458/work-package-operational-visualization-architecture.md)
- UI SPECIFICATION: [Work Package Screen (`SCR-WP-001`)](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue)

## Objective

Assess the feasibility, domain boundaries, schema impact, API contracts, cross-module telemetry queries, and UI/UX architecture to:

1. Calculate the empirical Organization 30-Day Demonstrated Daily Output ($C_{\text{org}}$) based on closed request complexity over a rolling window.
2. Formulate Work Package Scope Pressure as Required Daily Burn ($\sum \text{Remaining Complexity} / \text{Working Days Remaining}$) benchmarked as Org Capacity Share ($\% \text{ of } C_{\text{org}}$), handling dateless packages as `UNPLANNED`.
3. Evaluate observable Operational Health Invariants (Deadline Breach, Active Blockers, Dormancy, WIP Stagnancy, Flowing) derived purely from entity states and domain timestamps without subjective estimation.
4. Expose a unified Operations Cockpit query endpoint (`GET /api/v1/work-packages/operations-cockpit`) providing portfolio aggregate load, matrix cell distributions, and telemetry cards.
5. Create the Executive Operations Cockpit screen (`SCR-WP-002`) featuring the interactive 2D Pressure × Health Triage Matrix with drillable cells.
6. Modernize the Work Package Card on `SCR-WP-001` with Scope Pressure tiers, Health Invariant pills, Flow Inventory facts, and a 14-day Flow Activity Barcode.
7. Enhance the Work Package Detail Drawer on `SCR-WP-001` with request state aging, blocker tracking, and an interactive de-scoping and deadline simulator.

---

# 2. Current State

## Existing Behavior

1. **Domain Entities & Scope Relationship**:
   - `WorkPackage` aggregate root (`WorkPackage.cs`) tracks `Id`, `Name`, `Objective`, `OwnerPersonId`, `CustomerId`, `ProductId`, `Deadline` (introduced in CR-023), `Status` (`DRAFT`, `ACTIVE`, `CLOSED`), and linked `Requests` (`WorkPackageRequest.cs`).
   - `Request` aggregate root (`Request.cs`) tracks `Complexity` (integer 1–5, defaulting to 1), `Status` (`CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`, `COMPLETED`, `CANCELLED`), `OwnerPersonId`, `CustomerId`, `ProductId`, `WorkPackageId`, `Deadline`, `CreatedAt`, and `UpdatedAt`.
   - In `Request.cs`, active work is suspended via `PauseWork(...)` transitioning `IN_PROGRESS` $\to$ `PAUSED` with a note and raising `RequestWorkPaused`.
2. **Persistence & Data Access Layer**:
   - Database tables `[workpackage].[WorkPackages]`, `[workpackage].[WorkPackageRequests]`, and `[request].[Requests]` are partitioned into distinct database schemas per DDD module boundaries.
   - Cross-module queries are executed via decoupled application query services (`IRequestQueryService`, `IWorkPackageQueryService`).
   - `WorkPackageQueryService.cs` already executes `GetWorkPackageScopeAsync` and enriches linked requests using `_requestQueryService`.
3. **Application & API Layer (`Cakra.Api`)**:
   - `WorkPackagesController.cs` exposes CRUD and lifecycle endpoints (`/work-packages`, `/work-packages/{id}`, `/work-packages/{id}/scope`, `/work-packages/{id}/deadline`, `/work-packages/{id}/context`).
   - There is currently no endpoint or service method that computes aggregate organizational throughput ($C_{\text{org}}$) across completed requests.
   - There is currently no endpoint or read model providing aggregated Work Package telemetry (remaining complexity, dormant days, active WIP count, oldest active age, 14-day outflow, capacity share).
4. **Frontend Architecture (`Cakra.Web`)**:
   - `WorkPackageView.vue` (`SCR-WP-001`) renders a table of Work Packages and a side drawer with request reordering and metadata controls.
   - There is no executive triage cockpit, no 2D Pressure × Health matrix, and no flow activity visualization.
   - Cakra has existing modern UI primitives in `@/components/base/` (`PageHeader.vue`, `BaseCard.vue`, `BaseBadge.vue`, `BaseAvatar.vue`, `SlideOverDrawer.vue`, `StatusStrip.vue`), utilized in `WorkInProgressView.vue` (`SCR-REQ-006`).

## Existing Constraints

1. **Strict DDD Module Decoupling**: Modul `WorkPackage` and modul `Request` do not execute direct cross-schema database joins. Cross-module data enrichment must occur via clean query services (`IRequestQueryService` consumed by `WorkPackageQueryService`).
2. **Zero Start Date Constraint**: A Work Package has no start date. Time-based demand calculations must strictly compare the current date ($T_{\text{now}}$) against the Target Deadline.
3. **Unplanned / Optional Deadline Rule**: In Cakra, `Deadline` on a Work Package is optional (`null` allowed). If a deadline is not set, Scope Pressure cannot be computed mathematically; it must be represented as `UNPLANNED`.
4. **Factual Immutability**: Health signals must never be derived from subjective opinions or synthetic trend curves; they must be derived from logged operational events and entity states.
5. **No Automated Paternalism**: The executive cockpit must present cold operational facts and omit automated algorithmic recommendations, reserving operational judgment for the COO.

---

# 3. Gap Analysis

| ID | Severity | Gap |
|---|---|---|
| GAP-001 | CRITICAL | Modul `Request` lacks a query service method to compute Organization 30-Day Demonstrated Daily Output ($C_{\text{org}}$) across completed requests. |
| GAP-002 | CRITICAL | Modul `WorkPackage` lacks an operational telemetry service to compute Scope Pressure, Org Capacity Share, and UNPLANNED state for active packages. |
| GAP-003 | CRITICAL | Modul `WorkPackage` lacks logic to evaluate observable Operational Health Invariants (Deadline Breach, Active Blockers, Dormancy, WIP Stagnancy, Flowing). |
| GAP-004 | MAJOR | `WorkPackagesController.cs` lacks an endpoint `GET /api/v1/work-packages/operations-cockpit` returning portfolio macro metrics, matrix distributions, and telemetry cards. |
| GAP-005 | MAJOR | Frontend lacks an Executive Operations Cockpit screen (`SCR-WP-002` at `/operations/cockpit`) featuring the interactive Pressure × Health Triage Matrix. |
| GAP-006 | MAJOR | Work Package Card and table on `SCR-WP-001` lack Scope Pressure badges, Health Invariant pills, Flow Inventory facts, and 14-day flow barcodes. |
| GAP-007 | MAJOR | Work Package Detail Drawer on `SCR-WP-001` lacks request state aging, blocker identification, and an interactive de-scoping and deadline simulator. |

---

# 4. Open Questions

| ID | Question | Impact | Status |
|---|---|---|---|
| OQ-001 | Should progress be measured by Progress % or velocity? | Foundational metric paradigm. | CLOSED |
| OQ-002 | What is the benchmark anchor for Scope Pressure? | Interpretability of demand density. | CLOSED |
| OQ-003 | How should Work Packages without a Target Deadline be handled? | Matrix categorization and calculation nullability. | CLOSED |
| OQ-004 | Is "Thrashing" an operational state or a diagnosis? | Metric purity and objectivity. | CLOSED |
| OQ-005 | What visual mechanism drives the Executive Dashboard? | Dashboard navigation and executive ergonomics. | CLOSED |
| OQ-006 | How should the Executive Dashboard be integrated into Cakra's screen navigation? | Application architecture and routing. | CLOSED |

---

# 5. Assumptions

| ID | Assumption |
|---|---|
| ASM-001 | No database schema migration is required. Existing tables `[workpackage].[WorkPackages]`, `[workpackage].[WorkPackageRequests]`, and `[request].[Requests]` already contain all necessary attributes (`Complexity`, `Status`, `Deadline`, `CreatedAt`, `UpdatedAt`). |
| ASM-002 | Business days for runway calculation exclude Saturdays and Sundays (standard 5-day operational working week). If `Deadline < Today`, working days remaining is $\le 0$. |
| ASM-003 | Active Blockers are identified when a constituent Request has `Status == RequestStatus.Paused` (or a paused assignment note indicating an impediment). |
| ASM-004 | Dormancy is evaluated as the elapsed business days since the latest `UpdatedAt` timestamp among the Work Package and all its active constituent Requests. If $> 5$ business days, the package is flagged as DORMANT. |
| ASM-005 | WIP Stagnancy is evaluated as any constituent Request currently in `Status == RequestStatus.InProgress` with elapsed time in state $> 14$ calendar days without closure. |
| ASM-006 | If Organization 30-Day Demonstrated Daily Output ($C_{\text{org}}$) is zero (e.g. fresh installation or no closed requests in 30 days), a configurable fallback baseline (e.g. 1.0 pt/day) is used to prevent division-by-zero. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| RISK-001 | Cold start or small dataset yields very low $C_{\text{org}}$, causing nominal packages to appear as CRITICAL or IMPOSSIBLE. | High | Guard against division-by-zero with a configurable system floor for $C_{\text{org}}$ (e.g., minimum 1.0 pt/day) and display $C_{\text{org}}$ prominently in the cockpit header for transparency. |
| RISK-002 | Teams intentionally leave deadlines blank to hide packages in the UNPLANNED column. | Medium | The cockpit macro strip prominently reports `WITHOUT DEADLINE` counts, and the UNPLANNED column is placed first in the Triage Matrix with full drill-down visibility. |
| RISK-003 | High query latency when computing telemetry across hundreds of active Work Packages and thousands of requests. | Medium | Implement an optimized SQL projection in `RequestRepository` that aggregates counts and complexity sums grouped by `WorkPackageId` in a single query rather than loading full entities. |

---

# 7. Recommendations

## Option A: Dedicated Cockpit Screen (`SCR-WP-002`) + Modernized `SCR-WP-001` (Recommended)

Create a dedicated Executive Operations Cockpit screen (`SCR-WP-002`) at `/operations/cockpit` focused entirely on portfolio health, aggregate load, the 2D Pressure × Health Matrix, and the exception triage feed. Simultaneously modernize `SCR-WP-001` (`WorkPackageView.vue`) to include operational cards, flow barcodes, and the detail workbench.

### Advantages

- Clean separation of concerns: Executive triage on `/operations/cockpit` vs. daily operational work package management on `/work-packages`.
- Maximizes screen real estate for the 2D Matrix and exception feeds without cluttering package creation/editing workflows.
- Aligns with Cakra's actor model (Management / COO cockpit vs. Work Package Owner workbench).

### Disadvantages

- Requires adding a new route and navigation item in the sidebar.

## Option B: Single Screen Tabbed Mode inside `WorkPackageView.vue`

Embed the Operations Cockpit as a top-level tab inside `WorkPackageView.vue` (`SCR-WP-001`), toggling between "Executive Cockpit" and "Package Catalog".

### Advantages

- Keeps all work package features under a single route (`/work-packages`).

### Disadvantages

- Bloats `WorkPackageView.vue` (already ~2,400 lines) with complex cockpit state, filter synchronization, and matrix rendering.
- Merges disparate actor responsibilities (executive triage vs. package CRUD) into a single monolithic component.

---

# 8. Gap Closure

## GAP-001

### Decision
Add `Task<double> GetOrgDemonstratedDailyThroughputAsync(int windowDays = 30, CancellationToken cancellationToken = default)` to `IRequestQueryService` and `RequestQueryService.cs`. Compute total complexity of requests completed in the past `windowDays` divided by `windowDays`.

### Rationale
Provides an empirical, observable organizational capacity anchor ($C_{\text{org}}$) derived directly from historical completed work.

### Impact
Enables objective calculation of Org Capacity Share without speculative estimates.

### Architecture Impact
Extends `IRequestQueryService` in `Cakra.Modules.Request` without violating module isolation.

### Resolved By
User & Analyst Alignment

### Resolved Date
2026-10-07

---

## GAP-002

### Decision
Implement `WorkPackageTelemetryService` in `Cakra.Modules.WorkPackage` to calculate Required Daily Burn ($\sum \text{Complexity} / \text{Working Days}$) and Org Capacity Share ($(\text{Daily Burn} / C_{\text{org}}) \times 100\%$). If `Deadline == null`, assign `PressureTier = UNPLANNED`.

### Rationale
Ensures demand density is mathematically grounded when a deadline exists, while cleanly representing uncommitted work as UNPLANNED without fabricated pressure values.

### Impact
Establishes standardized capacity share tiers: NOMINAL (<20%), ELEVATED (20-50%), CRITICAL (50-100%), IMPOSSIBLE (>100%), and UNPLANNED.

### Architecture Impact
Introduces `WorkPackageTelemetryDto` read model and calculation service in `Cakra.Modules.WorkPackage`.

### Resolved By
User & Analyst Alignment

### Resolved Date
2026-10-07

---

## GAP-003

### Decision
Evaluate Operational Health Invariants strictly as factual boolean conditions:
- `DeadlineBreached`: `Deadline != null && WorkingDaysRemaining <= 0 && RemainingRequestsCount > 0`
- `HasActiveBlockers`: `PausedRequestsCount > 0`
- `IsDormant`: `ElapsedBusinessDaysSinceLastEvent > 5`
- `IsWipStagnant`: `OldestActiveWipDays > 14`
- `IsFlowing`: `!DeadlineBreached && !HasActiveBlockers && !IsDormant && !IsWipStagnant`

### Rationale
Completely eliminates subjective diagnostic labels (like "Thrashing") and naked percentages. Every health state is an indisputable operational fact.

### Impact
Provides cold, audit-backed health classifications for every active Work Package.

### Architecture Impact
Health evaluation logic encapsulated in telemetry read models.

### Resolved By
User & Analyst Alignment

### Resolved Date
2026-10-07

---

## GAP-004

### Decision
Add endpoint `GET /api/v1/work-packages/operations-cockpit` in `WorkPackagesController.cs`, returning `OperationsCockpitDto` containing:
- `PortfolioMetrics`: TotalActive, AggregateLoadPercentage, WithDeadlineCount, WithoutDeadlineCount, OrgDailyThroughput.
- `Matrix`: 2D cell counts for each (HealthState, PressureTier) combination.
- `Packages`: List of `WorkPackageTelemetryDto` items.

### Rationale
Provides a high-performance, single-roundtrip query for the executive dashboard.

### Impact
Frontend cockpit loads instantly with complete matrix distribution and exception feed data.

### Architecture Impact
Exposes read-only telemetry endpoint in `Cakra.Api`.

### Resolved By
User & Analyst Alignment

### Resolved Date
2026-10-07

---

## GAP-005

### Decision
Create `OperationsCockpitView.vue` (`SCR-WP-002`) routed at `/operations/cockpit`. Implement the clickable 2D Pressure × Health Matrix as the primary navigation and filtering tool. Present an exception-filtered operational feed without automated recommendations. Add "Operations Cockpit" to the sidebar menu under Operations.

### Rationale
Gives the COO an immediate, exception-driven operational radar answering "Where do I look?" while honoring the principle that executive judgment belongs to the COO.

### Impact
Enables immediate executive triage across hundreds of packages.

### Architecture Impact
New Vue screen component and route in `Cakra.Web`.

### Resolved By
User & Analyst Alignment

### Resolved Date
2026-10-07

---

## GAP-006

### Decision
Modernize Work Package cards and list rendering on `SCR-WP-001` (`WorkPackageView.vue`) to show Capacity Share tier badges, Health Invariant pills (Blockers, Dormancy), Flow Inventory stats (Active WIP, Oldest Age, 14d Outflow), and a 14-day Flow Activity Barcode.

### Rationale
Provides operational visibility directly within the primary work package management screen.

### Impact
Operators and owners see the same operational reality as the COO.

### Architecture Impact
Frontend template enhancement on `SCR-WP-001`.

### Resolved By
User & Analyst Alignment

### Resolved Date
2026-10-07

---

## GAP-007

### Decision
Enhance the Work Package Detail Drawer on `SCR-WP-001` with:
- Constituent requests table displaying individual Complexity, Status, and State Aging.
- Interactive Decision Workbench allowing operators to simulate de-scoping requests or extending the deadline, instantly recalculating the resulting Org Capacity Share.

### Rationale
Provides concrete decision levers to resolve pace deficits before deadlines breach.

### Impact
Empowers teams and management to test corrective actions quantitatively.

### Architecture Impact
Frontend workbench component in `WorkPackageView.vue`.

### Resolved By
User & Analyst Alignment

### Resolved Date
2026-10-07

---

## OQ-001

### Decision
Progress % and Velocity are rejected as primary metrics. Primary measurement is derived from Scope Pressure (Demand Density) and observable Operational Health Invariants.

### Rationale
Progress % masks stagnation and risk; velocity has no dedicated mass in fluid capacity teams.

### Impact
Eliminates artificial metrics from all screens and APIs.

### Architecture Impact
No velocity or progress % storage required.

### Resolved By
User & Analyst Alignment

### Resolved Date
2026-10-07

---

## OQ-002

### Decision
Benchmark Scope Pressure against Org 30-Day Demonstrated Daily Output ($C_{\text{org}}$) as Org Capacity Share %.

### Rationale
Provides a tangible, company-wide capacity context that requires zero subjective guessing.

### Impact
Establishes clear, interpretable capacity bands (Nominal, Elevated, Critical, Impossible).

### Architecture Impact
Cross-module query contract for org throughput.

### Resolved By
User & Analyst Alignment

### Resolved Date
2026-10-07

---

## OQ-003

### Decision
Work Packages without a target deadline are assigned to the `UNPLANNED` pressure class.

### Rationale
Preserves the critical operational truth that pressure is unknown, not low.

### Impact
Cockpit matrix includes a dedicated UNPLANNED column.

### Architecture Impact
Nullable deadline properly represented in telemetry DTOs.

### Resolved By
User & Analyst Alignment

### Resolved Date
2026-10-07

---

## OQ-004

### Decision
Replace the diagnostic label "Thrashing" with observable Flow Inventory facts: Active WIP Count, Max In-Flight Age, and Recent Outflow.

### Rationale
Maintains operational purity; the dashboard reports verifiable facts, not editorial diagnoses.

### Impact
Dashboards show raw inventory metrics without controversy.

### Architecture Impact
Read models expose discrete inventory counters.

### Resolved By
User & Analyst Alignment

### Resolved Date
2026-10-07

---

## OQ-005

### Decision
Use the interactive 2D Pressure × Health Triage Matrix as the primary top-level visual. Every cell is clickable to filter the exception list. Pie charts, burndowns, and KPI widgets are strictly omitted.

### Rationale
Directs executive attention immediately to invariant violations and concentration risks.

### Impact
Compact, dense, clutter-free executive experience.

### Architecture Impact
Matrix data structure included in cockpit response DTO.

### Resolved By
User & Analyst Alignment

### Resolved Date
2026-10-07

---

## OQ-006

### Decision
Deploy Option A: Create a dedicated Operations Cockpit view (`SCR-WP-002` at `/operations/cockpit`) while updating `SCR-WP-001` with operational tiles and workbench tools.

### Rationale
Prevents component bloat and respects actor role separation.

### Impact
Clean navigation structure in `Cakra.Web`.

### Architecture Impact
New route in `router/index.ts` and sidebar entry in `App.vue`.

### Resolved By
User & Analyst Alignment

### Resolved Date
2026-10-07

---

# 9. Architecture Applicability

## Decision

**ARCHITECTURE-REQUIRED**

## Rationale

This change introduces:
1. Cross-module operational throughput calculation contracts between `Cakra.Modules.Request` and `Cakra.Modules.WorkPackage`.
2. New telemetry read models (`OperationsCockpitDto`, `WorkPackageTelemetryDto`) and aggregate portfolio load mathematics.
3. A new executive operational screen (`SCR-WP-002`, `OperationsCockpitView.vue`) and router/navigation additions.
4. Deep operational UI enhancements to `SCR-WP-001` (Flow Barcodes, Decision Workbench, Capacity Share gauges).
5. Cross-cutting operational invariants (Dormancy thresholds, WIP stagnancy rules, Blocker detection).

A formal target architecture is required to specify DTO schemas, query optimization strategies, cross-module service interfaces, and frontend component hierarchies before implementation planning begins.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved
- [x] All required decisions recorded
- [x] All blocking open questions resolved
- [x] Architecture can be finalized or updated

## Status

**READY-FOR-PLANNING**

## Notes

All functional gaps and open questions have been closed through the `/grill-me` intake alignment. The Architect has evaluated the closed gap resolutions and formally granted `READY-FOR-PLANNING` to proceed with Architecture creation (`CR-025-ARCHITECTURE.md`).

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-025-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-025-ISSUE.md)
- DOMAIN: [work-package-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/work-package-domain.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- ARCHITECTURAL SPECIFICATION: [work-package-operational-visualization-architecture.md](file:///C:/Users/drury/.gemini/antigravity/brain/ba7d9357-e8ff-4ef1-a8d2-4373df8bf458/work-package-operational-visualization-architecture.md)

Referenced codebase locations:

- `cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackage.cs`
- `cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageQueryService.cs`
- `cakra/src/backend/Cakra.Modules.Request/Services/RequestQueryService.cs`
- `cakra/src/backend/Cakra.Api/Controllers/WorkPackagesController.cs`
- `cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue`
- `cakra/src/frontend/Cakra.Web/src/router/index.ts`
- `cakra/src/frontend/Cakra.Web/src/App.vue`
