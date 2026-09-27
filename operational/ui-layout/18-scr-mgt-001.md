# Customer Progress Review

## Purpose

Provides management with an aggregated view of operational progress and request status grouped by Customer, facilitating oversight and relationship management.

## Primary Actors

Management

## Entry Points

* Global Navigation (`Management > Customer Progress`)

## Related Use Cases

* UC-MGT-002: Review Customer Delivery Progress

## Related User Journeys

* UJ-MGT-002

## Information Sections

### Section: Customer Selector

Purpose: Choose which customer's portfolio to review.

Displays:
* Dropdown or list of active Customers

### Section: Portfolio Health Summary

Purpose: High-level metrics for the selected customer.

Displays:
* Total Active Requests
* Requests past due or blocked
* Recent resolution count

### Section: Customer Request Grid

Purpose: Detailed list of requests for the selected customer.

Displays:
* Request ID, Title, Status, Assignee, Last Update

## Available Actions

* **Select Customer**
  * Actor: Management
  * Outcome: Refreshes the metrics and grid for the chosen customer.
* **Open Request Detail**
  * Actor: Management
  * Outcome: Navigates to `SCR-REQ-003: Request Detail` to investigate a specific request.

## Navigation Destinations

* `SCR-REQ-003: Request Detail`

## Layout Sketch

+--------------------------------------------------+
| Customer Progress Review                         |
+--------------------------------------------------+
| Select Customer: [ Acme Corp v ]                 |
+--------------------------------------------------+
| Health: 3 Active, 1 Needs Attention, 12 Resolved |
+--------------------------------------------------+
| Active Requests                                  |
|--------------------------------------------------|
| ID   | Title           | Status  | Assignee      |
|--------------------------------------------------|
| #123 | Server Outage   | Active  | Alice         |
| #126 | Add New User    | Active  | Bob           |
| #130 | Report Bug      | Blocked | Charlie       |
+--------------------------------------------------+
