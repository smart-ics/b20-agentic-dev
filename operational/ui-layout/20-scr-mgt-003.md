# Programmer Workload Review

## Purpose

Provides management with a real-time view of active Request assignments distributed across the implementation team, enabling resource allocation and bottleneck identification.

## Primary Actors

Management

## Entry Points

* Global Navigation (`Management > Programmer Workload`)

## Related Use Cases

* UC-MGT-004: Monitor Team Workload

## Related User Journeys

* UJ-MGT-004

## Information Sections

### Section: Team Overview

Purpose: Visual summary of current load distribution.

Displays:
* List of Programmers with active assignment counts
* Alert indicators for overloaded team members

### Section: Individual Active Queue

Purpose: Detailed view of a selected programmer's current responsibilities.

Displays:
* Request ID, Title, Status, Priority, Customer

## Available Actions

* **Select Programmer**
  * Actor: Management
  * Outcome: Shows the specific active queue for that individual.
* **Open Request Detail**
  * Actor: Management
  * Outcome: Navigates to `SCR-REQ-003: Request Detail` (e.g., to reassign the request).

## Navigation Destinations

* `SCR-REQ-003: Request Detail`

## Layout Sketch

+--------------------------------------------------+
| Programmer Workload Review                       |
+--------------------------------------------------+
| Team Distribution:                               |
| [Alice: 2 Active] [Bob: 5 Active!] [Charlie: 0]  |
+--------------------------------------------------+
| Bob's Active Queue                               |
|--------------------------------------------------|
| ID   | Title           | Status  | Priority      |
|--------------------------------------------------|
| #115 | API Timeout     | Blocked | High          |
| #116 | Export Feature  | Active  | Normal        |
| #118 | Data Sync       | Active  | Normal        |
| #121 | Update Docs     | Active  | Low           |
| #125 | UI Tweaks       | Active  | Low           |
+--------------------------------------------------+
