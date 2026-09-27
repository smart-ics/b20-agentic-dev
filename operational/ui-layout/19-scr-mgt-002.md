# Programmer Performance Review

## Purpose

Provides management with a historical view of Request outcomes and performance metrics associated with a specific Programmer (Implementator).

## Primary Actors

Management

## Entry Points

* Global Navigation (`Management > Programmer Performance`)

## Related Use Cases

* UC-MGT-003: Review Programmer Performance

## Related User Journeys

* UJ-MGT-003

## Information Sections

### Section: Programmer Selector

Purpose: Choose the individual to review.

Displays:
* Dropdown or list of Programmers / Implementators

### Section: Performance Metrics

Purpose: Aggregate historical data.

Displays:
* Total Requests Completed (Timeframe filterable)
* Average time to resolution
* Escalation / Rework metrics

### Section: Historical Request Grid

Purpose: Provide drill-down capability into past work.

Displays:
* Completed Request ID, Title, Customer, Completion Date, Outcome

## Available Actions

* **Select Programmer**
  * Actor: Management
  * Outcome: Refreshes the metrics and historical data.
* **Open Historical Request Detail**
  * Actor: Management
  * Outcome: Navigates to `SCR-REQ-003: Request Detail` to audit past execution.

## Navigation Destinations

* `SCR-REQ-003: Request Detail`

## Layout Sketch

+--------------------------------------------------+
| Programmer Performance Review                    |
+--------------------------------------------------+
| Select Programmer: [ Alice v ]   Period: [YTD v] |
+--------------------------------------------------+
| Metrics: 45 Completed, 2 Days Avg Resolution     |
+--------------------------------------------------+
| Completed Work                                   |
|--------------------------------------------------|
| ID   | Title           | Customer | Date Closed  |
|--------------------------------------------------|
| #090 | Fix Login UI    | Internal | 2026-08-15   |
| #082 | DB Migration    | Acme     | 2026-08-01   |
| ...                                              |
+--------------------------------------------------+
