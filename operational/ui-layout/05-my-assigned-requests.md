# SCR-REQ-005: My Assigned Requests

## Purpose

Provide Implementators and Request Owners with a personal work queue showing all Requests for which they are currently responsible, enabling them to organize and prioritize their operational work.

## Primary Actors

* Implementator
* Request Owner

## Entry Points

* Global Navigation → `Requests > My Assigned`

## Related Use Cases

* UC-COL-004: Review Assigned Requests

## Related User Journeys

* UJ-COL-004: Review Assigned Requests

## Information Sections

### Section: Assigned Request Table

Displays Requests for which the current user is the assigned Request Owner.

Displays:

* Request ID
* Title
* Request Status (CAPTURED, VALIDATED, ACTIVE, CLOSED)
* Customer name (if associated)
* Product name (if associated)
* Request Type
* Created date
* Last updated date

**Traceability:** UJ-COL-004 step 2 (reviews list of Requests for which they are currently responsible along with current statuses, priorities, and deadlines). Information Needed: list of Requests with identifier, customer, summary, status.

### Section: Filters and Sorting

Enables the actor to organize their personal work queue.

Displays:

* Filter by Request Status
* Filter by Customer
* Filter by Product
* Filter by Request Type
* Sort by any displayed column

**Traceability:** UJ-COL-004 alternative path (filtering and grouping workload by urgency, customer, product, or lifecycle stage).

### Section: Workload Summary

Provides at-a-glance awareness of the actor's current assignment load.

Displays:

* Total count of assigned Requests
* Count of assigned Requests by status

**Traceability:** UJ-COL-004 step 3 (assesses which Requests require immediate action).

## Available Actions

### Action: Select Request

* **Actor:** Implementator, Request Owner
* **Expected Outcome:** Actor navigates to `SCR-REQ-003: Request Detail` for the selected Request.
* **Traceability:** UJ-COL-004 step 4 (selects a specific Request to begin or continue active work).

## Navigation Destinations

* `SCR-REQ-003: Request Detail` (select a Request row)

## Layout Sketch

```
+--------------------------------------------------+
| Global Navigation Bar                            |
+--------------------------------------------------+

+--------------------------------------------------+
| Screen Title: My Assigned Requests               |
+--------------------------------------------------+

+--------------------------------------------------+
| Workload Summary                                 |
| Total: N | [By Status Counts]                    |
+--------------------------------------------------+

+--------------------------------------------------+
| Filters and Sorting Bar                          |
| [Status] [Customer] [Product] [Type]             |
+--------------------------------------------------+

+--------------------------------------------------+
| Assigned Request Table                           |
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
