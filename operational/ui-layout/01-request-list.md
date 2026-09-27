# SCR-REQ-001: Request List

## Purpose

Provide Implementators and Request Owners with a single destination to view and locate Requests within the system.

This screen supports locating Requests that need ownership assignment, locating Requests awaiting completion review, and monitoring Request progress.

## Primary Actors

* Implementator
* Request Owner

## Entry Points

* Global Navigation → `Requests > All Requests`

## Related Use Cases

* UC-REQ-002: Assign Request Owner
* UC-REQ-008: Review Request Completion
* UC-COL-003: Track Request Progress

## Related User Journeys

* UJ-REQ-002: Assign Request Owner
* UJ-REQ-008: Review Request Completion
* UJ-COL-003: Track Request Progress

## Information Sections

### Section: Request Table

Primary content of the screen. Displays a tabular list of Requests accessible to the current actor.

Displays:

* Request ID
* Title
* Request Status (CAPTURED, VALIDATED, ACTIVE, CLOSED)
* Request Owner (Person name, or unassigned indicator)
* Customer name (if associated)
* Product name (if associated)
* Request Type
* Created date
* Last updated date

**Traceability:** Supports UJ-REQ-002 (locating Requests requiring ownership), UJ-REQ-008 (locating completed Requests for review), UJ-COL-003 (viewing current status of Requests).

### Section: Filters and Sorting

Enables actors to narrow and organize the Request list.

Displays:

* Filter by Request Status
* Filter by Request Owner
* Filter by Customer
* Filter by Product
* Filter by Request Type
* Filter by date range (created, updated)
* Sort by any displayed column

**Traceability:** Supports UJ-REQ-002 step 1 (locating a Request requiring ownership), UJ-REQ-008 step 1 (locating completed Requests), UJ-COL-003 step 1 (locating Requests to track).

### Section: Summary Indicators

Provide at-a-glance awareness of the operational state of visible Requests.

Displays:

* Count of Requests by status (CAPTURED, VALIDATED, ACTIVE, CLOSED)
* Count of Requests without an assigned owner

**Traceability:** Supports UJ-REQ-002 (awareness of unassigned Requests), UJ-COL-003 (understanding current state distribution).

## Available Actions

### Action: Navigate to Create Request

* **Actor:** Implementator
* **Expected Outcome:** Actor navigates to `SCR-REQ-002: Create Request`.
* **Traceability:** Entry point for UJ-REQ-001.

### Action: Select Request

* **Actor:** Implementator, Request Owner
* **Expected Outcome:** Actor navigates to `SCR-REQ-003: Request Detail` for the selected Request.
* **Traceability:** Supports UJ-REQ-002, UJ-REQ-008, UJ-COL-003.

## Navigation Destinations

* `SCR-REQ-002: Create Request` (action button)
* `SCR-REQ-003: Request Detail` (select a Request row)

## Layout Sketch

```
+--------------------------------------------------+
| Global Navigation Bar                            |
+--------------------------------------------------+

+--------------------------------------------------+
| Summary Indicators                               |
| [By Status Counts] [Unassigned Count]            |
+--------------------------------------------------+

+--------------------------------------------------+
| Filters and Sorting Bar                          |
| [Status] [Owner] [Customer] [Product] [Type]     |
| [Date Range]                          [+ New]    |
+--------------------------------------------------+

+--------------------------------------------------+
| Request Table                                    |
| ID | Title | Status | Owner | Customer | Product |
|    |       |        |       | Type | Created     |
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
