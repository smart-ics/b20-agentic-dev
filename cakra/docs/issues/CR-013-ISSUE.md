# ISSUE

## Metadata

ID: CR-013
Type: CHANGE-REQUEST
Status: OPEN
Title: Continuous Infinite Scroll Timeline for Operational Feed

## Source

Reported By: User
Reported Date: 2026-10-05

## Description

The user requested changing the Operational Feed (`SCR-FEED-001`) navigation from traditional discrete page pagination (Previous / Next buttons, page numbers) to a continuous, infinite scroll timeline experience similar to Facebook's feed stream. As users scroll through the timeline, additional operational feed items should load automatically in continuous batches without requiring manual page navigation clicks. Newly created items should integrate seamlessly at the top of the feed stream, and end-of-stream and fetch failure recovery must be handled gracefully.

## Desired Outcome

1. The Operational Feed displays a continuous, seamless timeline stream where additional items are automatically fetched and appended as the user scrolls toward the bottom.
2. Discrete pagination controls (Previous / Next buttons, page number indicators) are replaced by a modern continuous scroll stream.
3. A subtle loading indicator appears at the bottom while subsequent batches of feed items are being loaded in the background without layout shifts or interrupting user interaction.
4. When new posts or requests are created (e.g., via the Create Request modal), the newly created items are prepended directly to the top of the timeline and total counters update immediately without resetting the user's current scroll position.
5. Filter selectors (Customer, Product, Exception Type, Exceptions Only) update the stream dynamically upon selection, and search queries apply automatically (with debouncing) while maintaining reset and reload capabilities from the top.
6. When all available feed items have been retrieved, a clear "all caught up" milestone indicator is displayed at the bottom of the timeline.
7. If an incremental batch fetch fails due to network issues, existing items remain intact and an inline retry mechanism allows users to re-attempt loading more items.
8. The right sidebar "Operational Stream Summary" card reflects the continuous stream model by showing total stream events, loaded exceptions count, and filter status without obsolete page number metrics.

## Current Situation

1. In `FeedView.vue` (`SCR-FEED-001`), the operational feed utilizes discrete offset-based page pagination with Previous and Next buttons.
2. Feed items are replaced entirely per page rather than appended continuously.
3. The bottom of the feed displays page range text (e.g., "Showing 1–20 of X items (Page 1 of Y)") and pagination buttons (`feed-pagination-prev`, `feed-pagination-next`).
4. The right sidebar summary card includes a "Current Page" row showing page numbers (`Page X / Y`).
5. Refreshing or creating a new request requires full-page reloading or page jumping rather than seamless timeline stream prepending.

## Evidence

- User Request:
  - "Operational Feed should be displayed as continues scroll (unlimited scroll?) like facbook timeline, not pagination."
- Alignment Interview (/grill-me) decisions:
  - Automatic infinite scroll triggered by scrolling near the bottom (IntersectionObserver sentinel) with bottom loading indicator.
  - New posts (e.g. from Create Request) prepend directly to top of feed items list without losing scroll position; manual refresh or filter change resets to top.
  - End of stream displays "You're all caught up" badge; background fetch failures display inline retry button without discarding loaded items.
  - Sidebar summary card removes "Current Page" row and retains Total Stream Events, Exceptions Loaded, and Stream Filter Status.
  - Dropdown filter selections apply immediately, and search input triggers automatically with 300ms debounce while retaining explicit form action buttons.
- Related files:
  - Operational Feed View: [FeedView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue)
  - Feed Timeline Card: [FeedTimelineCard.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/FeedTimelineCard.vue)
  - Feed API Controller: [FeedController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/FeedController.cs)

## Notes

This artifact formally captures the intake request for Continuous Infinite Scroll Timeline on the Operational Feed (CR-013) according to Knowledge-Centric SDLC standards. Detailed feasibility assessment, domain and feature updates, architecture design, and implementation planning belong to downstream analysis and architecture stages.
