---
Title: Feasibility Assessment for Continuous Infinite Scroll Timeline for Operational Feed
Code: CR-013
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-05
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Feature being assessed: Continuous Infinite Scroll Timeline for Operational Feed, per request in [CR-013-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-013-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-013-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-013-ISSUE.md)
- FEATURE: [FEAT-AWR-001-observe-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-AWR-001-observe-operational-feed.md)
- FEATURE: [FEAT-FCOL-005-filter-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-FCOL-005-filter-operational-feed.md)
- ARCHITECTURE (Reference): [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- UI Layout Spec (Reference): [10-scr-feed-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/10-scr-feed-001.md)

## Objective

Assess the technical feasibility, baseline codebase state, interaction gaps, architectural impact, and planning readiness to:

1. Transform the Operational Feed ([`FeedView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue) / `SCR-FEED-001`) from traditional discrete pagination into a continuous, infinite scroll timeline stream (similar to Facebook timeline).
2. Automatically trigger fetching and appending of subsequent feed item batches when scrolling near the bottom using an `IntersectionObserver` sentinel.
3. Prepend newly created posts or requests directly to the top of the feed stream with updated counts without resetting the user's active scroll position.
4. Render a subtle bottom loading indicator during incremental fetches, a milestone "You're all caught up" badge when reaching the end of the stream, and an inline retry button upon batch fetch failures.
5. Provide real-time dropdown filter updates and debounced search query filtering (300ms), resetting to offset 0 and reloading from top.
6. Remove obsolete "Current Page" metrics from the right sidebar summary card while retaining Total Stream Events, Exceptions Loaded, and Stream Filter Status.

---

# 2. Current State

Summarize relevant findings from current artifacts and codebase. This section contains facts only.

## Existing Behavior

1. **Operational Feed Screen ([`FeedView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue))**:
   - `FeedView.vue` queries `GET /api/v1/feed` with `pageSize: 20` and `offset: 0`.
   - On response, `feedItems.value` is completely replaced by the returned batch.
   - Discrete pagination controls (`feed-pagination`, `feed-pagination-prev`, `feed-pagination-next`) are rendered at the bottom of the feed stream.
   - When users click `Next` or `Prev`, `filters.offset` is adjusted by $\pm 20$ and `loadFeed()` replaces the visible items entirely.
   - When a request is created via `CreateRequestModal.vue`, `handleRequestSaved` re-runs `loadFeed()`, resetting the feed to the initial page.
   - The right sidebar summary card contains a "Current Page" row displaying `Page X / Y`.
2. **Backend Feed Query API ([`FeedController.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/FeedController.cs), [`FeedQueryService.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Post/Services/FeedQueryService.cs))**:
   - `GET /api/v1/feed` accepts `pageSize`, `offset`, `customerId`, `productId`, `isException`, `exceptionType`, and `searchTerm`.
   - Returns a `FeedPageResultDto` containing `Items`, `TotalCount`, `Page`, `PageSize`, `Offset`, `TotalPages`, and `HasMore`.
   - Supports offset-based pagination seamlessly without requiring new backend endpoints or schema migrations.

## Existing Constraints

1. **Memory & DOM Performance**: Continuous scrolling appends items to the reactive `feedItems` array. High item volumes (hundreds of cards) must render efficiently without memory leaks or excessive re-renders.
2. **Deduplication & Prepending**: Prepending items created locally or loading items during active database insertions could cause duplicate IDs if not deduplicated by `postId` / `feedItemId`.
3. **Scroll Jumps & User Disruption**: Prepending items or fetching next batches must avoid sudden jumpy layout shifts or unexpected resets of the user's viewport.
4. **Observer Cleanup**: `IntersectionObserver` instances must be disconnected cleanly when the component unmounts or when no more items exist (`hasMore === false`).

---

# 3. Gap Analysis

Identify gaps between the requested change and the current system.

| ID | Severity | Gap |
|------|------|------|
| GAP-001 | CRITICAL | `FeedView.vue` replaces `feedItems` entirely on each query rather than appending incremental batches for continuous infinite scroll. |
| GAP-002 | CRITICAL | `FeedView.vue` lacks an `IntersectionObserver` sentinel to detect when the user scrolls near the bottom and automatically fetch the next batch. |
| GAP-003 | MAJOR | `FeedView.vue` contains discrete pagination controls (`feed-pagination`, `feed-pagination-prev`, `feed-pagination-next`) that must be replaced by continuous scroll states (bottom spinner, end-of-feed milestone, inline retry). |
| GAP-004 | MAJOR | Newly created requests/posts trigger full feed reload from offset 0 instead of prepending directly to the top of the feed stream without scroll disruption. |
| GAP-005 | MINOR | The right sidebar summary card contains an obsolete "Current Page" row that does not apply to a continuous scrolling timeline. |
| GAP-006 | MINOR | Search input requires explicit form submission rather than debounced auto-filtering. |

---

# 4. Open Questions

Identify unresolved questions and their impact on design and architecture.

| ID | Question | Impact |
|------|------|------|
| OQ-001 | How should additional feed items be triggered as the user scrolls down? | Determines whether to use IntersectionObserver automatic infinite scrolling, manual "Load More", or a hybrid model. |
| OQ-002 | How should new posts or newly created requests be integrated into the timeline while scrolling? | Determines whether to prepend items locally or reset and reload the stream from the top. |
| OQ-003 | How should the end of the feed stream and incremental fetch failures be presented at the bottom of the list? | Determines the UI indicators for "all caught up" milestone and inline error retry button. |
| OQ-004 | How should the right sidebar "Operational Stream Summary" card reflect continuous scrolling? | Determines whether to remove or reformat the "Current Page" metric row. |
| OQ-005 | How should search and filter inputs interact with the continuous feed stream? | Determines whether filters apply immediately and whether search uses input debouncing. |

---

# 5. Assumptions

Document assumptions made during the assessment.

| ID | Assumption |
|------|------|
| ASM-001 | Existing backend endpoint `GET /api/v1/feed` fully supports `offset`, `pageSize`, `hasMore`, and `totalCount`, requiring zero backend modifications. |
| ASM-002 | Modern browser support for `IntersectionObserver` is ubiquitous across target operating environments. |
| ASM-003 | Feed items have unique identifiers (`feedItemId` / `postId`) that can be used for reliable Set/Map-based deduplication during incremental appends and prepends. |

---

# 6. Risks

Document identified risks, impacts, and mitigations.

| ID | Risk | Impact | Mitigation |
|------|------|------|------|
| RISK-001 | Rapid scrolling could trigger duplicate concurrent batch requests. | Moderate | Guard incremental fetching with an `isLoadingMore` flag and throttle sentinel observation triggers. |
| RISK-002 | Creating items while scrolled deep in the stream might create duplicates when scrolling resumes. | Low | Use ID-based deduplication when merging new batches into `feedItems`. |
| RISK-003 | Large feed stream DOM size could impact rendering performance if scrolled excessively. | Low | Default batch size of 20 keeps DOM light; Vue's virtual DOM efficiently reconciles appended items. |

---

# 7. Recommendations

Recommend possible approaches for addressing major gaps.

## Option A (Recommended)

1. **Implement `IntersectionObserver` Infinite Scroll in [`FeedView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue)**:
   - Introduce a bottom sentinel element (`<div ref="sentinelRef">`) monitored by an `IntersectionObserver`.
   - Maintain `isLoadingFeed` for initial/filter loads and `isLoadingMore` for incremental background batches.
   - Increment `filters.offset += filters.pageSize` and append newly fetched items into `feedItems` with ID deduplication.
   - When `hasMore === false` or `feedItems.length >= totalCount`, display a clean "You're all caught up" milestone.
   - If an incremental fetch fails, set `loadMoreError` and render an inline "Failed to load more — Retry" button without wiping out already loaded items.
2. **Prepend Newly Created Posts to Stream**:
   - In `handleRequestSaved`, synthesize or fetch the new post and prepend it (`feedItems.value.unshift(newItem)`), increment `totalCount`, and display the success banner without altering scroll position.
3. **Refactor Search & Sidebar Summary**:
   - Debounce search input (300ms) with `watch` or input handler, resetting `filters.offset = 0` and reloading from top.
   - Dropdowns trigger `applyFilters()` immediately on change.
   - Remove "Current Page" row from sidebar summary; retain "Total Stream Events", "Exceptions Loaded", and "Stream Filter Status".

### Advantages

- Full social timeline (Facebook-style) user experience.
- Seamless continuous browsing with zero manual pagination clicks.
- Robust error recovery with inline retry.
- Zero backend API changes required.

### Disadvantages

- Requires managing observer lifecycles and scroll/fetch concurrency guards in frontend component.

## Option B

Implement a manual "Load More" button at the bottom of the feed instead of automatic `IntersectionObserver` triggers.

### Advantages

- Slightly simpler lifecycle management without `IntersectionObserver`.

### Disadvantages

- Does not fulfill user's explicit request for continuous/unlimited scroll like Facebook timeline.

---

# 8. Gap Closure

Record resolutions for gaps and open questions based on the alignment interview.

## GAP-001 & GAP-002 & OQ-001: Automatic Infinite Scroll via IntersectionObserver

### Decision
Implement automatic continuous scrolling using an `IntersectionObserver` sentinel placed at the bottom of the feed card list. When the sentinel enters the viewport, the next batch of 20 items is fetched and appended to `feedItems` if `hasMore` is true and `!isLoadingMore`.

### Rationale
Provides a smooth, modern timeline browsing experience that eliminates manual pagination friction.

### Impact
Significantly improves user engagement and fluidity when scanning operational activities.

### Architecture Impact
Frontend-only change in `FeedView.vue`: add observer lifecycle (`onMounted`, `onUnmounted`), `isLoadingMore` state, and array append logic.

### Resolved By
User (/grill-me interview alignment)

### Resolved Date
2026-10-05

---

## GAP-003 & OQ-003: End-of-Stream Milestone & Inline Error Recovery

### Decision
Replace discrete pagination buttons (`feed-pagination`) with:
1. Bottom loading spinner during incremental fetching (`isLoadingMore`).
2. "You're all caught up" milestone card when all items have been loaded (`!hasMore` / `feedItems.length >= totalCount`).
3. Inline "Failed to load more feed items — Retry" button if a background fetch fails, preserving already loaded feed items.

### Rationale
Ensures clear visual feedback for stream boundaries and provides resilient error recovery without disrupting the user's reading flow.

### Impact
Prevents confusion about stream status and improves fault tolerance.

### Architecture Impact
Frontend template update in `FeedView.vue`.

### Resolved By
User (/grill-me interview alignment)

### Resolved Date
2026-10-05

---

## GAP-004 & OQ-002: Seamless Prepending of New Posts

### Decision
When a new request or post is created (e.g. from `CreateRequestModal.vue`), the new item is prepended directly to the top of `feedItems` and `totalCount` is incremented, preserving the user's current scroll position. Full reload from offset 0 occurs only on explicit manual Refresh or filter changes.

### Rationale
Matches social timeline conventions by immediately showing the user's authored content without resetting their reading progress.

### Impact
Improves authoring workflow and immediate visual confirmation.

### Architecture Impact
Update `handleRequestSaved` in `FeedView.vue` to prepend items and manage ID deduplication.

### Resolved By
User (/grill-me interview alignment)

### Resolved Date
2026-10-05

---

## GAP-005 & OQ-004: Sidebar Summary Metric Adaptation

### Decision
Remove the "Current Page" row from the right sidebar "Operational Stream Summary" card. Retain "Total Stream Events", "Exceptions Loaded", and "Stream Filter Status".

### Rationale
Page numbers are obsolete and contradictory in a continuous infinite scroll model.

### Impact
Maintains accurate, clean summary metrics in the contextual panel.

### Architecture Impact
Update sidebar template in `FeedView.vue`.

### Resolved By
User (/grill-me interview alignment)

### Resolved Date
2026-10-05

---

## GAP-006 & OQ-005: Real-time Selects & Debounced Search Filtering

### Decision
Dropdown filter changes (Customer, Product, Exception Type, Exceptions Only) apply immediately on selection. Search query input triggers automatically with a 300ms debounce while retaining explicit "Apply Filters" and "Reset" buttons. Applying filters resets offset to 0 and reloads from the top.

### Rationale
Delivers responsive filtering without requiring extra button clicks for every minor interaction.

### Impact
Speeds up operational filtering and discovery.

### Architecture Impact
Add 300ms debounce handling to search input in `FeedView.vue`.

### Resolved By
User (/grill-me interview alignment)

### Resolved Date
2026-10-05

---

# 9. Architecture Applicability

Record the Architecture Applicability decision.

## Decision

**ARCHITECTURE-REQUIRED**

## Rationale

Transitioning from discrete pagination to continuous infinite scrolling alters the presentation architecture of `SCR-FEED-001`, introducing `IntersectionObserver` lifecycles, dual-phase loading states (`isLoadingFeed` vs `isLoadingMore`), item deduplication and stream prepending, debounced search bindings, and end-of-stream / retry error handling. An architecture update and structured implementation plan are required to ensure robust execution.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved
- [x] All required decisions recorded
- [x] All blocking open questions resolved
- [x] Architecture can be finalized or updated

## Status

**READY-FOR-PLANNING**

## Notes

All functional gaps, UX flows, and open questions have been fully closed during the `/grill-me` alignment interview. In accordance with Knowledge-Centric SDLC rules, the Analyst maintains Status as NOT-READY. The gate to `READY-FOR-PLANNING` will be granted by `ica-architect` upon technical architecture review and update.

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-013-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-013-ISSUE.md)
- FEATURE: [FEAT-AWR-001-observe-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-AWR-001-observe-operational-feed.md)
- FEATURE: [FEAT-FCOL-005-filter-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-FCOL-005-filter-operational-feed.md)
- UI Layout Spec: [10-scr-feed-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/10-scr-feed-001.md)
- System Architecture: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

Referenced codebase locations:

- Operational Feed View: [FeedView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue)
- Feed Timeline Card: [FeedTimelineCard.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/FeedTimelineCard.vue)
- Feed API Controller: [FeedController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/FeedController.cs)
- Feed Query Service: [FeedQueryService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Post/Services/FeedQueryService.cs)
