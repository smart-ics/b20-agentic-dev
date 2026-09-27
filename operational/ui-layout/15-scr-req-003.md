# Request Detail

## Purpose

The authoritative, structured operational record for a Request. This screen is accessed when deep context, historical audit, structured state transitions, or detailed operational information is required that cannot be effectively consumed within the Operational Feed.

## Primary Actors

Implementator, Request Owner, Management

## Entry Points

* `SCR-FEED-001: Operational Feed` (via Feed Item reference)
* `SCR-REQ-001: Request List` (via selection)
* `SCR-REQ-004: Request Search & History` (via search result)
* `SCR-REQ-005: My Assigned Requests` (via selection)
* `SCR-MGT-*`: Management Screens (via selection)

## Related Use Cases

* UC-REQ-002 to UC-REQ-008
* UC-COL-001, UC-COL-003
* UC-MGT-001

## Related User Journeys

* UJ-REQ-002 through UJ-REQ-008
* UJ-COL-001, UJ-COL-003, UJ-MGT-001

## Information Sections

### Section: Request Core Attributes

Purpose: Displays the fundamental definition and current operational status.

Displays:
* Request ID and Title
* Current Status and Phase
* Description
* Assigned Roles (Owner, Implementator)

### Section: Context Linkages

Purpose: Shows the operational entities this request impacts or stems from.

Displays:
* Customer Details
* Product Details
* Work Package details

### Section: Operational Audit & State

Purpose: Provides structured historical tracking and state transition data.

Displays:
* Timestamps (Created, Updated, Resolved)
* Authority history (Who approved what and when)
* Status change log

### Section: Feed Integration (Associated Feed Stream)

Purpose: Displays a filtered view of the Operational Feed showing only feed items generated from this specific Request.

Displays:
* Embedded stream of related feed items (Comments, Updates, and System Events)

## Available Actions

* **Update Request Status**
  * Actor: Implementator, Request Owner
  * Outcome: Transitions the request state and generates a system feed item.
* **Edit Core Attributes**
  * Actor: Request Owner
  * Outcome: Updates the authoritative record.
* **Assign / Reassign**
  * Actor: Management, Request Owner
  * Outcome: Changes responsibility and generates a system feed item.

## Navigation Destinations

* Originating Screen (Back/Return)

## Layout Sketch

+--------------------------------------------------+
| < Back                                           |
+--------------------------------------------------+
| Request #123: Server Outage             [Update] |
| Status: ACTIVE                 Owner: Alice      |
+--------------------------------------------------+
| Details                                          |
| Description: The main web server is unreachable. |
| Customer: Acme Corp        Product: WebApp       |
+--------------------------------------------------+
| State & Audit                                    |
| Created: 2026-09-26 10:00 by Bob                 |
| Last Change: Assigned to Alice at 11:00          |
+--------------------------------------------------+
| Associated Feed Stream                           |
|                                                  |
| [Bob] - System: Created Request #123             |
| [Alice] - "I'm looking into the logs now."       |
|   [Reaction: Thumbs Up 2]                        |
|                                                  |
| [Write a comment...]                   [Comment] |
+--------------------------------------------------+
