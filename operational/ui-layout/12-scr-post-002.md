# Create Post

## Purpose

Provides a full authoring environment for creating a new operational communication or update that will be broadcast to the Operational Feed.

## Primary Actors

Implementator

## Entry Points

* `SCR-FEED-001: Operational Feed` (via "Create Post" expanded action)

## Related Use Cases

* UC-FCOL-003: Create Operational Post

## Related User Journeys

* UJ-FCOL-003

## Information Sections

### Section: Composition Area

Purpose: Where the user authors the content of their post.

Displays:
* Rich text editor for Post Content
* Media/Attachment upload area

### Section: Operational Context

Purpose: Allows the user to link the post to authoritative operational records, ensuring traceability.

Displays:
* Search/Select for Request
* Search/Select for Customer
* Search/Select for Product
* Search/Select for Work Package

## Available Actions

* **Draft Post Content**
  * Actor: Implementator
  * Outcome: Updates the post content in memory.
* **Attach References**
  * Actor: Implementator
  * Outcome: Links structured operational objects to the post.
* **Publish Post**
  * Actor: Implementator
  * Outcome: Commits the post, publishes it to the feed, and triggers operational awareness notifications.
* **Cancel**
  * Actor: Implementator
  * Outcome: Discards the draft and returns to the previous screen.

## Navigation Destinations

* `SCR-FEED-001: Operational Feed` (On Publish or Cancel)
* `SCR-POST-001: Post Detail` (Alternative on Publish)

## Layout Sketch

+--------------------------------------------------+
| Create New Post                            [ X ] |
+--------------------------------------------------+
| Content                                          |
| +----------------------------------------------+ |
| | Share an update, ask a question, or report   | |
| | an operational event...                      | |
| |                                              | |
| |                                              | |
| |                                              | |
| +----------------------------------------------+ |
|                                                  |
| Link Operational Context                         |
| Request: [ Search Requests...      ]             |
| Customer: [ Search Customers...    ]             |
| Product: [ Search Products...      ]             |
|                                                  |
+--------------------------------------------------+
|                                [Cancel] [Publish]|
+--------------------------------------------------+
