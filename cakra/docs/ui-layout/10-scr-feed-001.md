# Operational Feed

## Purpose

The primary workspace for operational awareness and collaboration. It is a projection of operational activity. Each feed item is generated from a Request or Request-related operational event. It enables stakeholders to observe Request progression, collaborate around operational work, and access structured records when needed.

## Primary Actors

All Actors (Management, Implementator, Request Owner)

## Entry Points

* System Open (Default Landing Page)
* Global Navigation (`Feed`)

## Related Use Cases

* UC-AWR-001: Observe Operational Feed
* UC-AWR-002: Discover Request via Feed
* UC-AWR-003: Monitor Operational Exceptions via Feed
* UC-FCOL-001: Comment on Feed Item
* UC-FCOL-002: React to Feed Item
* UC-FCOL-005: Filter Operational Feed

## Related User Journeys

* UJ-AWR-001, UJ-AWR-002, UJ-AWR-003
* UJ-FCOL-001, UJ-FCOL-002, UJ-FCOL-005

## Information Sections

### Section: Feed Filters

Purpose: Allows users to focus the feed on specific operational contexts.

Displays:
* Filter toggles (Customer, Product, Team)
* Search/Filter input

### Section: Feed Stream

Purpose: Displays a chronological list of operational feed items. Each feed item is generated from a Request or Request-related operational event (Updates, State Changes, Escalations, Comments).

Displays:
* Feed Item Actor and Timestamp
* Request Event Details / System Event Summary
* Referenced Operational Objects (e.g., Request ID, Customer Name)
* Recent Comments preview
* Reaction summary (counts and types)

## Available Actions

* **Filter Feed**
  * Actor: All Actors
  * Outcome: Updates Feed Stream to show only matching feed items.
* **React to Feed Item**
  * Actor: All Actors
  * Outcome: Records reaction as an operational signal and updates Reaction summary.
* **Comment on Feed Item**
  * Actor: All Actors
  * Outcome: Adds a comment inline, attached to the Request.

## Navigation Destinations

* `SCR-REQ-003: Request Detail` (via clicking a referenced Request)

## Layout Sketch

+--------------------------------------------------+
| Global Header / Navigation                       |
+--------------------------------------------------+
| Feed Filters: [Customer] [Product] [Team]        |
+--------------------------------------------------+
| Feed Stream                                      |
|                                                  |
| +----------------------------------------------+ |
| | [System] - [Time]                            | |
| | System Event: Request #123 Escalated         | |
| | Reference: [Request #123] [Customer A]       | |
| | [Reaction: Alert 2]  [Comment]               | |
| +----------------------------------------------+ |
|                                                  |
| +----------------------------------------------+ |
| | [Implementator] - [Time]                     | |
| | Updated Design Document for Authentication   | |
| | Reference: [Request #124]                    | |
| |                                              | |
| |   [User B]: Looks good!                      | |
| | [Reaction: Thumbs Up 4]  [Reply]             | |
| +----------------------------------------------+ |
+--------------------------------------------------+
