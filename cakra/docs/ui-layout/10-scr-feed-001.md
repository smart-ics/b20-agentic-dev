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

### Section: Create Request Modal (Affordance)

Purpose: Modal dialog interface launched directly from the feed header to capture and record a new Request without navigating away from the operational stream.

Displays:
* Modal Title and Close Button
* Input fields: Title (text, required), Description (textarea, required), Customer dropdown (active lookups), Product dropdown (active lookups), Request Type (select, default GENERAL), Priority (select, default NORMAL)
* Validation error summary / alert feedback
* Action buttons: Cancel (`btn-outline-secondary`) and Submit Request (`btn-primary`)

## Available Actions

* **Create Request**
  * Actor: All Actors (Implementator, Request Owner, Management)
  * Outcome: Opens `CreateRequestModal`. Upon valid submission, creates a new Request record via backend API, immediately refreshes the feed stream to display the new system post at index 0, and displays a dismissible success banner with a link to the created Request.
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

* `SCR-REQ-003: Request Detail` (via clicking a referenced Request in a feed card or via the post-creation success alert banner link)

## Layout Sketch

+------------------------------------------------------------------------+
| Global Header / Navigation                                             |
+------------------------------------------------------------------------+
| Operational Feed Header: [Operational Feed Title]   [Refresh] [+ Create]|
+------------------------------------------------------------------------+
| [Success Alert: Request #REQ-XXX recorded. View Request Detail ->] [X] |
+------------------------------------------------------------------------+
| Feed Filters: [Customer] [Product] [Team]                              |
+------------------------------------------------------------------------+
| Feed Stream                                                            |
|                                                                        |
| +--------------------------------------------------------------------+ |
| | [System] - [Just now]                                              | |
| | System Event: Request #125 Recorded                                | |
| | Reference: [Request #125] [Customer A]                             | |
| | [Reaction: Alert 0]  [Comment]                                     | |
| +--------------------------------------------------------------------+ |
|                                                                        |
| +--------------------------------------------------------------------+ |
| | [Implementator] - [Time]                                           | |
| | Updated Design Document for Authentication                         | |
| | Reference: [Request #124]                                          | |
| |                                                                    | |
| |   [User B]: Looks good!                                            | |
| | [Reaction: Thumbs Up 4]  [Reply]                                   | |
| +--------------------------------------------------------------------+ |
+------------------------------------------------------------------------+
| Modal Overlay: Create Request Dialog (CreateRequestModal)              |
| +--------------------------------------------------------------------+ |
| | Header: Create New Request                                     [X] | |
| | Title: [__________________________________]                        | |
| | Description: [__________________________________]                  | |
| | Customer: [Select Customer v]   Product: [Select Product v]        | |
| | Type: [GENERAL v]               Priority: [NORMAL v]               | |
| |                                     [Cancel]  [Create Request]     | |
| +--------------------------------------------------------------------+ |
+------------------------------------------------------------------------+
