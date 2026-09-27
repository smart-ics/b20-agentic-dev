# SCR-MGT-002: Programmer Performance Review

## Purpose

Allow Management to evaluate the volume, completion rates, and outcomes of Requests handled by a specific Programmer, identifying performance patterns and operational concerns.

## Primary Actors

* Management

## Entry Points

* Global Navigation → `Management > Programmer Performance`

## Related Use Cases

* UC-MGT-003: Review Programmer Request Performance

## Related User Journeys

* UJ-MGT-003: Review Programmer Request Performance

## Information Sections

### Section: Programmer Selector

Allows Management to identify and select the target Programmer.

Displays:

* Person search/select control (from Organization domain — Persons with Programmer role)
* Selected Programmer name
* Organizational context (Team membership, Role assignments)

**Traceability:** UJ-MGT-003 step 1 (selects the target Programmer). Information Needed: Programmer identity.

### Section: Time Period Selector

Allows Management to define the review period.

Displays:

* Time period selection (e.g., current month, last 3 months, last 6 months, custom range)

**Traceability:** UJ-MGT-003 alternative path (filtering by timeframe to observe recent versus historical performance).

### Section: Performance Summary

Provides quantitative overview of the Programmer's Request handling.

Displays:

* Total Requests handled in period
* Requests completed (resolved successfully)
* Requests rejected
* Requests currently active
* Completion rate (completed / total)

**Traceability:** UJ-MGT-003 step 2 (reviews request processing history, completion rates, and resolution outcomes). Information Needed: Handled Requests and resolution outcomes.

### Section: Request History Table

Displays the detailed list of Requests handled by the selected Programmer.

Displays:

* Request ID
* Title
* Request Status
* Customer name (if associated)
* Product name (if associated)
* Resolution outcome (if resolved)
* Created date
* Closed date (if closed)

**Traceability:** UJ-MGT-003 alternative path (inspecting specific outcomes to understand resolution context).

## Available Actions

### Action: Select Request

* **Actor:** Management
* **Expected Outcome:** Actor navigates to `SCR-REQ-003: Request Detail` for the selected Request.
* **Traceability:** UJ-MGT-003 alternative path (examining individual completed or rejected Requests).

## Navigation Destinations

* `SCR-REQ-003: Request Detail` (select a Request row)

## Layout Sketch

```
+--------------------------------------------------+
| Global Navigation Bar                            |
+--------------------------------------------------+

+--------------------------------------------------+
| Screen Title: Programmer Performance Review      |
+--------------------------------------------------+

+--------------------------------------------------+
| Programmer Selector                              |
| Programmer: [___________ search/select]          |
| Team: Development | Role: Programmer             |
+--------------------------------------------------+

+--------------------------------------------------+
| Time Period: [Last 3 Months v]                   |
+--------------------------------------------------+

+--------------------------------------------------+
| Performance Summary                              |
| Total: N | Completed: n | Rejected: n            |
| Active: n | Completion Rate: n%                  |
+--------------------------------------------------+

+--------------------------------------------------+
| Request History Table                            |
| ID | Title | Status | Customer | Product         |
|    | Resolution | Created | Closed                |
| ------------------------------------------------ |
| Row 1                                            |
| Row 2                                            |
| Row 3                                            |
| ...                                              |
+--------------------------------------------------+

+--------------------------------------------------+
| Pagination                                       |
+--------------------------------------------------+
```
