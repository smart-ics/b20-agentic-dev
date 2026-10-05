# ISSUE

## Metadata

ID: CR-014
Type: CHANGE-REQUEST
Status: OPEN
Title: Universal Search Textbox for Operational Feed

## Source

Reported By: User
Reported Date: 2026-10-05

## Description

The user requested consolidating the Operational Feed (`SCR-FEED-001`) search and filtering into a single universal search textbox placed prominently on top of the operational feed stream. The universal search must allow users to query across multiple operational dimensions within a single unified search box: Product Name, Customer Name, User Name (post authors and commenters), Request Text (request title and description), and Comment Text (across all active comments). The universal search replaces the existing multi-field filter card in the sidebar as the sole filtering mechanism for the operational feed.

## Desired Outcome

1. A full-width universal search textbox is placed prominently at the top of the operational feed stream, directly below the screen header and above the feed timeline cards.
2. The search operates as a unified search across five operational dimensions:
   - Product Name
   - Customer Name
   - Relevant User Name(s) (post author and comment authors)
   - Request Text (request title and description)
   - Comment Text (all active comments)
3. The search box provides a responsive search-as-you-type experience with 300ms debouncing, accompanied by a clear button (`x`) to instantly reset the search.
4. The active search query is synchronized with the browser URL query parameter (e.g. `?q=...`) to support bookmarking, sharing, and browser refresh persistence.
5. The legacy right-sidebar filter card (Customer dropdown, Product dropdown, Exception Type dropdown, Exceptions Only toggle, and legacy search input) is removed, establishing universal search as the primary operational feed filter.
6. The right sidebar retains the "Operational Stream Summary" metrics card (Total Stream Events, Exceptions Loaded, Stream Filter Status).
7. If a search query produces no results, an empty state is displayed detailing the active query and providing a one-click button to clear the search.
8. Multi-word search queries match seamlessly across target fields using prefix and term matching, supporting partial word matching while typing.

## Current Situation

1. In `FeedView.vue` (`SCR-FEED-001`), search and filtering are located inside a "Feed Filters" card in the right sidebar.
2. The current search input in the sidebar executes SQL `LIKE` wildcard matching against only `Title`, `ContentExcerpt`, `CustomerName`, `ProductName`, and `ReferenceDisplay`.
3. The current search does not search across all comments, comment author names, or full request descriptions.
4. Users must interact with discrete filter controls (Customer dropdown, Product dropdown, Exception Type dropdown, Exceptions-only toggle, and search input) rather than a single unified search textbox.
5. The search term is not reflected in the browser URL query parameters.

## Evidence

- User Request:
  - "Change the search / filter to universal search textbox on top of operational feed."
  - "The search includes: Product Name, Customer Name, User Name, Request Text, Comment Text."
  - "I propose to use FullTextSearch (SQL Server)."
- Alignment Interview (/grill-me) decisions:
  - Replace all sidebar filters entirely with the top universal search bar (search becomes the sole filtering mechanism).
  - Dedicated full-width search bar placed directly below the screen header and above the stream cards (with search icon, clear button, and 300ms debounced search-as-you-type). Keep the Operational Stream Summary in the right sidebar.
  - Multi-word queries match as word prefixes (Prefix-AND matching).
  - Consolidated read-model search projection (`SearchContent NVARCHAR(4000)`) on `[post].[FeedItems]` indexed with SQL Server Full-Text Index, maintained automatically by projection handlers (re-aggregating on `CommentAdded`), with dual-mode fallback when FTS is not installed.
  - Synchronize search term with URL query parameter (`?q=...`).
- Related files:
  - Operational Feed View: [FeedView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue)
  - Feed Timeline Card: [FeedTimelineCard.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/FeedTimelineCard.vue)
  - Feed API Controller: [FeedController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/FeedController.cs)
  - Feed Query Service: [FeedQueryService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Post/Services/FeedQueryService.cs)
  - Feed Projection Handler: [FeedProjectionHandler.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Post/Services/FeedProjectionHandler.cs)
  - Feed Items Table Migration: [0009_feed_items_table.sql](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Migrations/Scripts/0009_feed_items_table.sql)

## Notes

This artifact formally captures the intake request for Universal Search for the Operational Feed (CR-014) according to Knowledge-Centric SDLC standards. Detailed feasibility assessment, domain and feature updates, architecture design, and implementation planning belong to downstream analysis and architecture stages.
