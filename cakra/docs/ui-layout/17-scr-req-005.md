# My Assigned Requests

## Purpose

Provides a personalized view for an actor to see only the Requests currently assigned to them for execution or ownership.

## Primary Actors

Implementator, Request Owner

## Entry Points

* Global Navigation (`Requests > My Assigned`)

## Related Use Cases

* UC-COL-004: Manage Personal Workload

## Related User Journeys

* UJ-COL-004

## Information Sections

### Section: Workload Summary

Purpose: Quick glance at current responsibilities.

Displays:
* Count of Active assigned requests
* Count of Blocked or Needs Attention assigned requests

### Section: Assigned Request List

Purpose: The detailed queue of the user's work.

Displays:
* Data grid or card list of assigned Requests (ID, Title, Priority, SLA/Due Date, Customer)

## Available Actions

* **Open Request Detail**
  * Actor: Implementator, Request Owner
  * Outcome: Navigates to `SCR-REQ-003: Request Detail` to begin work or update status.

## Navigation Destinations

* `SCR-REQ-003: Request Detail`

## Layout Sketch

+--------------------------------------------------+
| My Assigned Requests                             |
+--------------------------------------------------+
| Summary: 5 Active, 1 Needs Attention             |
+--------------------------------------------------+
| Due Soon                                         |
| +----------------------------------------------+ |
| | #110 - Fix checkout bug (High Priority)      | |
| | Customer: RetailCo      Status: Active       | |
| +----------------------------------------------+ |
|                                                  |
| Active Queue                                     |
| +----------------------------------------------+ |
| | #112 - Implement SSO login                   | |
| | Customer: Internal      Status: Active       | |
| +----------------------------------------------+ |
| | #123 - Server Outage                         | |
| | Customer: Acme Corp     Status: Active       | |
| +----------------------------------------------+ |
+--------------------------------------------------+
