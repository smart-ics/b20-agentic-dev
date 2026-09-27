# SCR-MGT-003: Programmer Workload Review

## Purpose

Allow Management to assess the current active assignment load of a specific Programmer, determining whether they have capacity or are facing workload imbalance.

## Primary Actors

* Management

## Entry Points

* Global Navigation → `Management > Programmer Workload`

## Related Use Cases

* UC-MGT-004: Review Programmer Workload

## Related User Journeys

* UJ-MGT-004: Review Programmer Workload

## Information Sections

### Section: Programmer Selector

Allows Management to identify and select the target Programmer.

Displays:

* Person search/select control (from Organization domain — Persons with Programmer role)
* Selected Programmer name
* Organizational context (Team membership, Role assignments)

**Traceability:** UJ-MGT-004 step 1 (selects the target Programmer). Information Needed: Programmer identity.

### Section: Workload Summary

Provides a quantitative overview of the Programmer's current assignment load.

Displays:

* Total count of active Request assignments
* Count of active Requests by status (CAPTURED, VALIDATED, ACTIVE)

**Traceability:** UJ-MGT-004 step 2 (inspects active request assignments and current workload status). UJ-MGT-004 step 3 (assesses capacity or workload imbalance). Information Needed: Active Request assignments and current status.

### Section: Active Assignment Table

Displays the Programmer's current active Request assignments.

Displays:

* Request ID
* Title
* Request Status
* Customer name (if associated)
* Product name (if associated)
* Request Type
* Created date
* Last updated date

**Traceability:** UJ-MGT-004 step 2 (inspects active request assignments). UJ-MGT-004 alternative path (reviewing across team members to compare capacity).

## Available Actions

### Action: Select Request

* **Actor:** Management
* **Expected Outcome:** Actor navigates to `SCR-REQ-003: Request Detail` for the selected Request. From Request Detail, Management can perform reassignment (UC-MGT-001).
* **Traceability:** UJ-MGT-004 alternative path (initiating rebalancing by reassigning Requests). UJ-MGT-001 (reassign request ownership).

## Navigation Destinations

* `SCR-REQ-003: Request Detail` (select a Request row)

## Layout Sketch

```
+--------------------------------------------------+
| Global Navigation Bar                            |
+--------------------------------------------------+

+--------------------------------------------------+
| Screen Title: Programmer Workload Review         |
+--------------------------------------------------+

+--------------------------------------------------+
| Programmer Selector                              |
| Programmer: [___________ search/select]          |
| Team: Development | Role: Programmer             |
+--------------------------------------------------+

+--------------------------------------------------+
| Workload Summary                                 |
| Total Active: N | CAPTURED: n | VALIDATED: n     |
| ACTIVE: n                                        |
+--------------------------------------------------+

+--------------------------------------------------+
| Active Assignment Table                          |
| ID | Title | Status | Customer | Product | Type  |
|    | Created | Updated                            |
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
