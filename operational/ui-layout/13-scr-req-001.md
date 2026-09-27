# Request List

## Purpose

Provides a structured view of Requests categorized by their operational state, allowing users to find and access authoritative operational records when deep context is needed.

## Primary Actors

Implementator, Request Owner

## Entry Points

* Global Navigation (`Requests > List`)
* `SCR-REQ-002: Create Request` (Return after creation)
* `SCR-REQ-003: Request Detail` (Return via back navigation)

## Related Use Cases

* UC-REQ-002: View Request List
* UC-REQ-008: Monitor Request Status
* UC-COL-003: Review Request History

## Related User Journeys

* UJ-REQ-002, UJ-REQ-008, UJ-COL-003

## Information Sections

### Section: Filter and Triage Controls

Purpose: Allow users to segment the request list by status, urgency, or tags.

Displays:
* Status Filters (e.g., Active, Needs Attention, Blocked, Resolved)
* Quick Text Search

### Section: Request Data Grid

Purpose: Present structured metadata for multiple requests to facilitate scanning.

Displays:
* Request ID
* Title / Summary
* Current Status
* Assigned Implementator
* Associated Customer
* Last Updated Timestamp

## Available Actions

* **Filter List**
  * Actor: Implementator, Request Owner
  * Outcome: Narrows the displayed requests in the data grid.
* **Initiate Request Creation**
  * Actor: Implementator
  * Outcome: Navigates to `SCR-REQ-002: Create Request`.
* **Open Request Detail**
  * Actor: Implementator, Request Owner
  * Outcome: Navigates to `SCR-REQ-003: Request Detail` for the selected record.

## Navigation Destinations

* `SCR-REQ-002: Create Request`
* `SCR-REQ-003: Request Detail`

## Layout Sketch

+--------------------------------------------------+
| Requests                                [Create] |
+--------------------------------------------------+
| Filters: [Active] [Blocked] [Resolved]  [Search] |
+--------------------------------------------------+
| ID   | Title           | Status  | Customer      |
|--------------------------------------------------|
| #101 | Server Outage   | Active  | Acme Corp     |
| #102 | Feature Request | Blocked | Globex        |
| #103 | Bug Report      | Active  | Initech       |
| ...                                              |
+--------------------------------------------------+
