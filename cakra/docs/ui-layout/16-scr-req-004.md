# Request Search & History

## Purpose

Enables users to search, filter, and retrieve historical or specific Request records based on structured criteria.

## Primary Actors

Implementator

## Entry Points

* Global Navigation (`Requests > Search & History`)
* `SCR-REQ-001: Request List` (via advanced search link)

## Related Use Cases

* UC-COL-002: Retrieve Historical Request Context

## Related User Journeys

* UJ-COL-002

## Information Sections

### Section: Search Criteria Form

Purpose: Allow complex querying against the authoritative Request records.

Displays:
* Date Range filters
* Customer / Product dropdowns
* Status multiselect
* Keyword / Full-text search input
* Actor filters (Creator, Assignee)

### Section: Search Results Grid

Purpose: Display matching structured records.

Displays:
* Tabular view of matching Requests (ID, Title, Status, Date)

## Available Actions

* **Execute Search**
  * Actor: Implementator
  * Outcome: Populates the results grid.
* **Open Request Detail**
  * Actor: Implementator
  * Outcome: Navigates to `SCR-REQ-003: Request Detail` for the selected record.

## Navigation Destinations

* `SCR-REQ-003: Request Detail`

## Layout Sketch

+--------------------------------------------------+
| Search & History                                 |
+--------------------------------------------------+
| Keyword: [                 ] Date: [       ]     |
| Customer: [ Select v ]       Status: [     ]     |
|                                        [ Search] |
+--------------------------------------------------+
| Results (4 found)                                |
|--------------------------------------------------|
| ID   | Title           | Status  | Date          |
|--------------------------------------------------|
| #089 | Legacy Bug Fix  | Closed  | 2026-01-10    |
| #042 | Setup Account   | Closed  | 2025-11-05    |
| ...                                              |
+--------------------------------------------------+
