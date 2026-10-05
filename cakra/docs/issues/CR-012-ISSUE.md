# ISSUE

## Metadata

ID: CR-012
Type: CHANGE-REQUEST
Status: OPEN
Title: Timeline-Style Operational Feed with Default-Expanded Comments and Inline Interaction

## Source

Reported By: User
Reported Date: 2026-10-05

## Description

The user requested transforming the operational feed (`SCR-FEED-001`) into a rich, social timeline interaction model (similar to Facebook timeline). Comments under each feed card must be expanded by default. If an operational post has more than 2 comments, only the last 2 comments should be shown initially, with an option to expand and view previous comments inline. Users must be able to write and submit comments directly on each feed card without opening a modal dialog. Furthermore, operational reactions must be accessible and toggled directly on the feed card without requiring a modal popup.

## Desired Outcome

1. Comments on operational feed cards are expanded and visible by default.
2. When a post has more than 2 comments, the card displays the 2 most recent comments and provides a "View previous comments (X)" control that dynamically reveals all earlier comments directly inline.
3. An inline comment input box is permanently anchored at the bottom of each feed card, enabling users to enter and submit comments directly (supporting both Enter key submission and an explicit Send button) without opening a modal.
4. Operational reactions (Seen, Experienced, Have Idea, Similar Issue, Duplicate, Need Clarification) can be viewed, quick-toggled, and selected directly from each feed card via a Facebook-style reaction palette without opening a modal.
5. Engagement summary displays aggregate reaction counts and reaction badges alongside comment counts directly on the feed card.
6. Feed card background remains stable and non-navigating during comment/reaction interactions, while retaining explicit access to full thread details and metadata via post titles and dedicated "View full thread" actions.

## Current Situation

1. In `FeedView.vue` (`SCR-FEED-001`), feed cards only display summary counters (`CommentCount`, `ReactionCount`) and a single static `LatestCommentExcerpt` snippet.
2. Entire comments lists and comment composition are only accessible through the `PostDetailModal.vue` (`SCR-POST-001`) dialog, requiring users to open a modal window to view conversation history or submit new comments.
3. Reactions can only be chosen and submitted within `PostDetailModal.vue`, with the feed view only rendering passive badge counts.
4. The entire feed card is clickable to open `PostDetailModal.vue`, which causes modal popups when clicking anywhere on the card.

## Evidence

- User request:
  - Operational feed comments expanded by default.
  - If more than 2 comments, show only the last 2 comments with inline expansion.
  - User can add comments directly in the operational feeds without showing modal form.
  - User can give reactions directly in the feeds without showing modal form.
  - Timeline interaction model resembling Facebook timeline.
- Alignment interview (/grill-me) decisions:
  - Inline expandable: "View previous comments (X)" button/link reveals earlier comments inline within the card.
  - Facebook-style reaction picker: quick-toggle default reaction (`SEEN`) + floating reaction palette for all 6 operational reaction types (`SEEN`, `EXPERIENCED`, `HAVE_IDEA`, `SIMILAR_ISSUE`, `DUPLICATE`, `NEED_CLARIFICATION`).
  - Enter to send (Shift+Enter for newline) + explicit Send button.
  - Always visible inline comment input box anchored at bottom of each card.
  - Explicit modal triggers: Post title and "View full thread" button open `PostDetailModal` for deep references and audit trails, while card body is non-navigating.
- Related files:
  - Operational Feed View: [FeedView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue)
  - Post Detail Modal: [PostDetailModal.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/PostDetailModal.vue)
  - Feed & Post Controllers / Endpoints: [FeedController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/FeedController.cs), [PostsController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/PostsController.cs)

## Notes

This artifact formally captures the intake request for the Timeline-Style Operational Feed change request (CR-012) in a solution-neutral manner according to Knowledge-Centric SDLC standards. Detailed feasibility assessment, domain and feature updates, architecture design, and implementation planning belong to downstream analysis and architecture stages.
