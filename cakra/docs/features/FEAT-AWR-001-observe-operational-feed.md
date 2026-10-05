# Feature

## Identity

ID: FEAT-AWR-001
Name: Observe Operational Feed
Type: Query

## Purpose

Enables operational actors to maintain continuous operational awareness by presenting a chronological stream of active operational posts (system-generated from operational events such as Request creation and lifecycle transitions, alongside preserved historical posts), displaying summaries of requests and exceptions, comment previews, and reaction counts, and providing direct navigation links from post references to structured request records.

## User Outcome

Operational participants (Implementators, Management, Request Owners) discover current operational activities, spot new requests and state changes, monitor operational exceptions (such as escalations and stalled items), and transition seamlessly from high-level feed posts to authoritative request details.

## Traceability

### Domains
- Post
- Request

### Scenarios
- SC-AWR-001
- SC-AWR-002
- SC-AWR-003
- SC-FCOL-004

### Use Cases
- UC-AWR-001
- UC-AWR-002
- UC-AWR-003
- UC-FCOL-004

### User Journeys
- UJ-AWR-001
- UJ-AWR-002
- UJ-AWR-003
- UJ-FCOL-004

### Screens
- SCR-FEED-001
- SCR-POST-001
- CreateRequestModal (SCR-FEED-001 modal affordance)

## Preconditions

- The actor is authenticated and authorized to access the Operational Feed.
- System contains active operational Posts or Request event logs.

## Capability

The system renders an interactive, reverse-chronological continuous feed stream on `SCR-FEED-001: Operational Feed` and detailed thread views on `SCR-POST-001: Post Detail`. The feed stream renders author identity, timestamps, event content summaries, referenced operational objects (Requests, Customers, Products), preview comments, and structured reaction badges. It highlights operational exceptions (escalations, stalled requests) and provides interactive hyperlink navigation from referenced operational objects directly to `SCR-REQ-003: Request Detail`. The feed automatically fetches and appends incremental batches as the user scrolls toward the bottom using an IntersectionObserver sentinel. Additionally, `SCR-FEED-001` provides a direct '+ Create Request' header action opening `CreateRequestModal`, allowing operational actors to record new customer requests directly from the feed interface; upon submission, the newly generated post is prepended directly to the top of the feed stream (index 0) without displacing scroll position, alongside a dismissible success banner with a link to `SCR-REQ-003: Request Detail`.

## Business Rules

- The Feed is not an independent domain; it is a presentation projection derived from Post state and referenced domain entities (Post Domain Rule 33; Manifesto Principle 16).
- Only Posts with lifecycle status `ACTIVE` and visibility `VISIBLE` appear in the normal feed stream (Post Domain Rules 4, 5, 27).
- Archived and hidden posts are excluded from the active feed view (Post Domain Rules 27, 28, 479, 507).
- System-generated posts must display the source operational event (e.g., `RequestCreated`, `RequestAssigned`, `RequestEscalated`, `RequestResolved`) and anchor to the source operational object (Post Domain Rules 7, 336).
- A Post Reference only records contextual association; it does not transfer ownership or alter the lifecycle of the referenced object (Post Domain Rules 9, 10, 340).
- A Post remains valid and visible in the feed even if its referenced operational object is archived or closed (Post Domain Rules 13, 14, 348).
- Clicking a post reference link to an associated Request transitions the user directly to the authoritative Request Detail screen (`SCR-REQ-003`) (UI Layout 10-scr-feed-001, line 65; UC-FCOL-004).
- The Feed header provides a direct request creation mechanism (`CreateRequestModal`) that records a Request in `CAPTURED` state and prepends the new post to the top of the stream deterministically upon completion (CR-003, CR-013).
- The feed stream continuously appends items via automated scroll detection, rendering an "all caught up" indicator when all available items are loaded, and an inline retry option upon batch fetch failure (CR-013).

## Success Result

The actor views an up-to-date, continuous stream of active operational posts, perceives current operational activity and exceptions without waiting for status meetings, follows reference links to detailed records, scrolls seamlessly through historical operational events, and creates new requests directly from within the feed.

## Failure Conditions

- Actor is unauthenticated or lacks viewer authority (authority failure).
- Referenced operational object is no longer accessible (post remains displayed as an independent record with an orphaned reference indicator).
- No active posts are available (system displays an informative empty feed state indicating no recent operational activity).

## Acceptance Criteria

- [ ] Continuous, reverse-chronological stream of active, visible posts is rendered on `SCR-FEED-001`.
- [ ] Subsequent batches of posts load and append automatically as the user scrolls toward the bottom.
- [ ] End of feed displays an "all caught up" milestone indicator when all posts have been loaded.
- [ ] Incremental batch fetch failures display an inline retry control without discarding previously loaded feed items.
- [ ] System-generated posts display author/system source, timestamp, event summary, and referenced objects.
- [ ] Operational exceptions (escalations, blockers, stalled items) are visually emphasized in the feed.
- [ ] Reaction summary counts and recent comment previews are visible on each feed card.
- [ ] Selecting a referenced Request link navigates directly to `SCR-REQ-003: Request Detail`.
- [ ] Selecting a post card navigates to `SCR-POST-001: Post Detail`.
- [ ] Hidden and archived posts do not appear in the active feed stream.
- [ ] Operational actors can initiate request creation directly from `SCR-FEED-001` header via '+ Create Request', opening `CreateRequestModal`.
- [ ] Successfully submitting a request from the feed modal immediately prepends the post to the top of the feed stream and displays a success alert with a link to the created request.

## Implementation Notes

Subsumes feed discovery (`UC-AWR-002`), exception observation (`UC-AWR-003`), and post reference navigation (`UC-FCOL-004`) as presentation affordances over the Post Aggregate.

