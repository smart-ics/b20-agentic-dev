# Post Detail

## Purpose

Provides a dedicated view for an individual operational post, allowing users to see deep context, read the entire comment thread, and participate in a focused discussion without the noise of the main feed.

## Primary Actors

All Actors (Management, Implementator, Request Owner)

## Entry Points

* `SCR-FEED-001: Operational Feed` (Selecting a Post)

## Related Use Cases

* UC-FCOL-001: Comment on Post
* UC-FCOL-002: React to Post
* UC-FCOL-004: Navigate from Post to Request

## Related User Journeys

* UJ-FCOL-001, UJ-FCOL-002, UJ-FCOL-004

## Information Sections

### Section: Post Content

Purpose: Displays the full original post or system event details.

Displays:
* Author and Timestamp
* Full Post Text
* Embedded Media or Attachments
* Referenced Operational Objects (Request, Work Package, Customer, Product)

### Section: Reactions

Purpose: Shows the operational signals and acknowledgments on the post.

Displays:
* Aggregated Reaction counts by type
* List of users who reacted (on hover or click)

### Section: Comment Thread

Purpose: Displays the entire history of replies and discussion related to the post.

Displays:
* Chronological list of Comments
* Comment Author and Timestamp
* Comment Content
* Individual Comment Reactions

## Available Actions

* **React to Post/Comment**
  * Actor: All Actors
  * Outcome: Records reaction as an operational signal.
* **Reply to Post**
  * Actor: All Actors
  * Outcome: Appends a new Comment to the discussion thread.
* **Navigate to Referenced Object**
  * Actor: All Actors
  * Outcome: Opens the associated Request Detail.

## Navigation Destinations

* `SCR-FEED-001: Operational Feed` (Back / Return)
* `SCR-REQ-003: Request Detail` (via Post Reference link)

## Layout Sketch

+--------------------------------------------------+
| < Back to Feed                                   |
+--------------------------------------------------+
| Post Content                                     |
| [Author] - [Date/Time]                           |
|                                                  |
| Full text of the operational post or system      |
| event notification goes here.                    |
|                                                  |
| References: [Request #123] [Customer A]          |
|                                                  |
| Reactions: [Thumbs Up 4] [Eyeballs 2]            |
+--------------------------------------------------+
| Comments                                         |
|                                                  |
| [User C] - [Time]                                |
| This is a comment on the post.                   |
| Reactions: [Check 1]               [Reply]       |
|                                                  |
| [User D] - [Time]                                |
| Following up on User C's comment.                |
| Reactions: [+]                     [Reply]       |
+--------------------------------------------------+
| [ Write a comment...                   ] [Send]  |
+--------------------------------------------------+
