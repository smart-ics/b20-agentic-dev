# SCR-MGT-001: Customer Progress Review

## Purpose

Allow Management to monitor the operational progress and request status for a specific Customer, identifying blocked, delayed, or critical Requests requiring attention.

## Primary Actors

* Management

## Entry Points

* Global Navigation → `Management > Customer Progress`

## Related Use Cases

* UC-MGT-002: Review Customer Request Progress

## Related User Journeys

* UJ-MGT-002: Review Customer Request Progress

## Information Sections

### Section: Customer Selector

Allows Management to identify and select the target Customer.

Displays:

* Customer search/select control (from Customer domain)
* Selected Customer name and status
* HasActiveMaintenanceContract indicator

**Traceability:** UJ-MGT-002 step 1 (identifies and selects the target Customer). Information Needed: Customer identity.

### Section: Customer Request Summary

Provides an overview of the selected Customer's Request situation.

Displays:

* Total count of Requests for the selected Customer
* Count of Requests by status (CAPTURED, VALIDATED, ACTIVE, CLOSED)
* Count of Requests with no assigned owner

**Traceability:** UJ-MGT-002 step 2 (reviews current status and progress of Requests associated with the Customer).

### Section: Customer Request Table

Displays the detailed list of Requests associated with the selected Customer.

Displays:

* Request ID
* Title
* Request Status
* Request Owner (Person name)
* Product name (if associated)
* Request Type
* Created date
* Last updated date

**Traceability:** UJ-MGT-002 step 3 (identifies blocked, delayed, or critical Requests). Information Needed: Associated Requests and current progress.

### Section: Filters

Enables Management to focus on specific subsets of the Customer's Requests.

Displays:

* Filter by Request Status
* Filter by Request Owner
* Filter by date range

**Traceability:** UJ-MGT-002 alternative path (filtering requests by status or date range).

## Available Actions

### Action: Select Request

* **Actor:** Management
* **Expected Outcome:** Actor navigates to `SCR-REQ-003: Request Detail` for the selected Request.
* **Traceability:** UJ-MGT-002 step 3 (identifies a Request requiring attention) and UJ-MGT-001 (Management may intervene from detail). Supports UJ-MGT-002 alternative path (intervening on delayed requests).

## Navigation Destinations

* `SCR-REQ-003: Request Detail` (select a Request row)

## Layout Sketch

```
+--------------------------------------------------+
| Global Navigation Bar                            |
+--------------------------------------------------+

+--------------------------------------------------+
| Screen Title: Customer Progress Review           |
+--------------------------------------------------+

+--------------------------------------------------+
| Customer Selector                                |
| Customer: [___________ search/select]            |
| Status: Active | Maintenance Contract: Yes       |
+--------------------------------------------------+

+--------------------------------------------------+
| Customer Request Summary                         |
| Total: N | CAPTURED: n | VALIDATED: n | ACTIVE: n|
| CLOSED: n | Unassigned: n                        |
+--------------------------------------------------+

+--------------------------------------------------+
| Filters                                          |
| [Status] [Owner] [Date Range]                    |
+--------------------------------------------------+

+--------------------------------------------------+
| Customer Request Table                           |
| ID | Title | Status | Owner | Product | Type     |
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
