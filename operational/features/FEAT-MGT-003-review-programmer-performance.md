# Feature

## Identity

ID: FEAT-MGT-003
Name: Review Programmer Request Performance
Type: Analytics

## Purpose

Provides management with a historical view of request volume, resolution outcomes, and completion metrics for an individual programmer or implementator over a specified timeframe.

## User Outcome

Management evaluates the historical throughput, resolution outcomes (resolved vs. rejected), and turnaround patterns of a programmer to support mentoring, performance evaluation, or organizational adjustments.

## Traceability

### Domains
- Request
- Organization

### Scenarios
- SC-MGT-003

### Use Cases
- UC-MGT-003

### User Journeys
- UJ-MGT-003

### Screens
- SCR-MGT-002

## Preconditions

- The actor holds the `Management` organizational role.
- Target individual is a Person holding the `Programmer` or `Implementator` role in the Organization Domain.

## Capability

The system provides a programmer selector on `SCR-MGT-002: Programmer Performance Review` with timeframe filters (e.g., Year-to-Date, custom period). It aggregates completed request records, computes performance metrics (total completed requests, average resolution turnaround, outcome breakdown), and renders a historical request grid with direct drill-down links to `SCR-REQ-003: Request Detail` to audit past execution.

## Business Rules

- Person identity and role assignments are authoritative in the Organization Domain (Organization Domain Section 5, Rules 2, 8).
- Performance metrics derive from authoritative Request and Resolution states (`Outcome`, `ResolvedAt`, `ResolvedBy`) in closed requests (Request Domain Section 5, 8; Rules 14, 15, 16).
- Performance metrics are derived analytical projections and must not create synthetic domain state (Manifesto Principles 2, 3, 16).
- Inactive persons and completed requests remain retrievable for historical audit without data loss (Organization Domain Rule 13; Request Domain Rule 16).
- Selecting a completed request navigates to `SCR-REQ-003: Request Detail` (UI Layout 19-scr-mgt-002, lines 53-56).

## Success Result

Management reviews historical throughput, completion rates, and resolution outcomes for the selected programmer, and audits specific completed requests to understand performance patterns.

## Failure Conditions

- Actor lacks the `Management` organizational role (authority failure).
- Selected programmer has zero completed requests in the chosen timeframe (grid displays empty state with zeroed metrics).

## Acceptance Criteria

- [ ] Screen `SCR-MGT-002` is restricted to authorized Management actors.
- [ ] Dropdown lists programmers and implementators from the Organization Domain.
- [ ] System computes total completed requests and average resolution time for the selected period.
- [ ] Historical grid displays closed requests with ID, Title, Customer, Date Closed, and Outcome.
- [ ] Selecting a historical request navigates to `SCR-REQ-003: Request Detail`.
- [ ] Historical audit data is preserved even if the programmer is now inactive in the organization.

## Implementation Notes

Historical analytics projection aggregating closed Request Aggregate records filtered by assignee and timeframe.
