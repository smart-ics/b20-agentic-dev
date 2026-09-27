# Create Request

## Purpose

Allows users to formalize a business need, issue, or change request into a structured operational record.

## Primary Actors

Implementator

## Entry Points

* Global Navigation (`Requests > Create`)
* `SCR-REQ-001: Request List` (via "Create" action)

## Related Use Cases

* UC-REQ-001: Submit New Request

## Related User Journeys

* UJ-REQ-001

## Information Sections

### Section: Request Definition

Purpose: Capture the core details and objective of the request.

Displays:
* Title / Summary Input
* Detailed Description Input
* Request Type Selector (e.g., Bug, Feature, Support)

### Section: Operational Context Links

Purpose: Anchor the request to existing business entities.

Displays:
* Customer Selection
* Product Selection
* Work Package Selection (optional)

## Available Actions

* **Submit Request**
  * Actor: Implementator
  * Outcome: Creates the authoritative Request record, generates an initial system Post in the Operational Feed, and navigates to the resulting Request Detail or back to the list.
* **Cancel Creation**
  * Actor: Implementator
  * Outcome: Discards input and returns to the originating screen.

## Navigation Destinations

* `SCR-REQ-001: Request List` (On Cancel or Return)
* `SCR-REQ-003: Request Detail` (On successful creation)
* `SCR-FEED-001: Operational Feed` (Implicitly, via system event notification)

## Layout Sketch

+--------------------------------------------------+
| Create New Request                               |
+--------------------------------------------------+
| Request Details                                  |
| Title: [                                       ] |
| Type:  [ Dropdown v ]                            |
| Description:                                     |
| +----------------------------------------------+ |
| |                                              | |
| |                                              | |
| +----------------------------------------------+ |
|                                                  |
| Link Context                                     |
| Customer: [ Select Customer... v ]               |
| Product:  [ Select Product...  v ]               |
|                                                  |
+--------------------------------------------------+
|                                [Cancel] [Submit] |
+--------------------------------------------------+
