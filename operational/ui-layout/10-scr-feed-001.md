# Operational Feed

## Purpose

The primary workspace for operational awareness and collaboration. It behaves as a social activity stream, displaying a chronological projection of operational activity, enabling users to observe activity, join discussions, collaborate, and access structured records when needed.

## Primary Actors

All Actors (Management, Implementator, Request Owner)

## Entry Points

* System Open (Default Landing Page)
* Global Navigation (`Feed`)

## Related Use Cases

* UC-AWR-001: Observe Operational Feed
* UC-AWR-002: Discover Request via Feed
* UC-AWR-003: Monitor Operational Exceptions via Feed
* UC-FCOL-001: Comment on Post
* UC-FCOL-002: React to Post
* UC-FCOL-005: Filter Operational Feed

## Related User Journeys

* UJ-AWR-001, UJ-AWR-002, UJ-AWR-003
* UJ-FCOL-001, UJ-FCOL-002, UJ-FCOL-005

## Information Sections

### Section: Feed Composer

Purpose: Provides a quick entry point for users to author new operational communications.

Displays:
* Input area for post content
* Tools for referencing operational objects (Request, Customer, Product)

### Section: Feed Filters

Purpose: Allows users to focus the feed on specific operational contexts.

Displays:
* Filter toggles (Customer, Product, Team)
* Search/Filter input

### Section: Activity Stream

Purpose: Displays a chronological list of operational activities (Posts, Updates, Escalations).

Displays:
* Post Author and Timestamp
* Post Content / System Event Summary
* Referenced Operational Objects (e.g., Request ID, Customer Name)
* Recent Comments preview
* Reaction summary (counts and types)

## Available Actions

* **Filter Feed**
  * Actor: All Actors
  * Outcome: Updates Activity Stream to show only matching Posts.
* **Create Quick Post**
  * Actor: Implementator
  * Outcome: Publishes a new Post to the Activity Stream.
* **React to Post**
  * Actor: All Actors
  * Outcome: Records reaction and updates Reaction summary.
* **Comment on Post**
  * Actor: All Actors
  * Outcome: Adds a comment to the Post inline.

## Navigation Destinations

* `SCR-POST-001: Post Detail` (via selecting a Post)
* `SCR-POST-002: Create Post` (via expanded creation flow)
* `SCR-REQ-003: Request Detail` (via clicking a referenced Request)

## Layout Sketch

+--------------------------------------------------+
| Global Header / Navigation                       |
+--------------------------------------------------+
| Feed Filters: [Customer] [Product] [Team]        |
+--------------------------------------------------+
| Feed Composer: [ What's happening?       ] [Post]|
+--------------------------------------------------+
| Activity Feed                                    |
|                                                  |
| +----------------------------------------------+ |
| | [Author] - [Time]                            | |
| | System Event: Request #123 Escalated         | |
| | Reference: [Request #123] [Customer A]       | |
| | [Reaction: Alert 2]  [Comment]               | |
| +----------------------------------------------+ |
|                                                  |
| +----------------------------------------------+ |
| | [Author] - [Time]                            | |
| | Just finished the initial design for X.      | |
| | Reference: [Request #124]                    | |
| |                                              | |
| |   [User B]: Looks good!                      | |
| | [Reaction: Thumbs Up 4]  [Reply]             | |
| +----------------------------------------------+ |
+--------------------------------------------------+
