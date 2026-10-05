---
Title: Architecture Specification for Continuous Infinite Scroll Timeline for Operational Feed
Code: CR-013
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-05
---

# 1. Overview

This architecture artifact defines the technical realization for transitioning the Operational Feed screen ([`FeedView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue) / `SCR-FEED-001`) from traditional discrete page pagination to a modern, continuous infinite scroll timeline stream (similar to Facebook's feed stream), as requested in [CR-013-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-013-ISSUE.md) and assessed in [CR-013-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-013-FEASIBILITY-ASSESSMENT.md).

The target architecture introduces:
1. **Automated Infinite Scroll via IntersectionObserver**: Automatically monitors a sentinel element anchored at the bottom of the feed card list. As the user scrolls near the bottom of the viewport, subsequent batches of feed items are fetched asynchronously via `GET /api/v1/feed` with incrementing offsets and appended seamlessly to the active feed stream.
2. **Seamless Stream Prepending & Zero Scroll Displacement**: When new operational posts or requests are created (e.g. through `CreateRequestModal.vue`), the newly created post is prepended directly to index 0 of `feedItems`, and total stream counters increment immediately without displacing or resetting the user's active scroll position.
3. **End-of-Stream & Error Recovery UI**: Replaces obsolete pagination button controls with a bottom loading indicator (`isLoadingMore`), an "All Caught Up" milestone banner when all items are loaded (`!hasMore`), and an inline retry mechanism ("Failed to load more — Retry") if background batch fetching fails.
4. **Real-time & Debounced Filtering**: Dynamic dropdown selectors (Customer, Product, Exception Type, Exceptions Only) update the stream immediately upon selection, while search input automatically updates with a 300ms debounce, resetting offset to 0 and scrolling to top.
5. **Contextual Panel Metrics Alignment**: Cleanses the right sidebar "Operational Stream Summary" card by removing the obsolete "Current Page" row while retaining Total Stream Events, Exceptions Loaded, and Stream Filter Status.

---

# 2. Architectural Basis

## Business Context

- ISSUE: [CR-013-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-013-ISSUE.md)
- FEATURE: [FEAT-AWR-001-observe-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-AWR-001-observe-operational-feed.md)
- FEATURE: [FEAT-FCOL-005-filter-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-FCOL-005-filter-operational-feed.md)
- UI Layout Spec: [10-scr-feed-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/10-scr-feed-001.md)
- System Architecture: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

## Analysis Input

- FEASIBILITY-ASSESSMENT: [CR-013-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-013-FEASIBILITY-ASSESSMENT.md)
  - Approved Decisions: `GAP-001` through `GAP-006`, `OQ-001` through `OQ-005`.
  - Architecture Applicability: `ARCHITECTURE-REQUIRED`.

```text
CR-013-ISSUE + CR-013-FEASIBILITY-ASSESSMENT
                     ↓
           CR-013-ARCHITECTURE
```

---

# 3. Scope

## Included

1. **Refactoring of Operational Feed View ([`FeedView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue))**:
   - Continuous scroll stream state management: `feedItems`, `filters.pageSize` (20), `filters.offset`, `totalCount`, `hasMore`, `isLoadingFeed` (initial/filter load), `isLoadingMore` (background batch append), `loadMoreError` (incremental fetch failure flag).
   - IntersectionObserver lifecycle wiring with `<div ref="sentinelRef">` at the bottom of the feed card list.
   - Batch append and ID deduplication logic (`postId` / `feedItemId`) to prevent duplicate cards during real-time activity or pagination transitions.
   - Prepending newly created requests/posts to index 0 in `handleRequestSaved` without scroll jumps.
   - Debounced search query watcher (300ms) with automatic stream reset to offset 0 and scroll to top.
   - Dropdown filter change handlers resetting stream to offset 0 and reloading immediately.
   - Replacement of `<nav class="feed-pagination">` controls with:
     - Bottom loading spinner for `isLoadingMore`.
     - End-of-stream "You're all caught up" milestone card.
     - Inline "Failed to load more — Retry" button.
   - Removal of the obsolete "Current Page" row from the right sidebar summary card.
2. **Styling & CSS Enhancements ([`main.css`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/assets/main.css))**:
   - End-of-stream milestone card styling (`.op-feed-end-milestone`).
   - Bottom spinner and retry button layout rules.

## Excluded

1. Backend database schema modifications or SQL migration scripts (existing database structure and queries in `Cakra.Modules.Post` are fully compatible).
2. Backend REST API changes in `FeedController.cs` or `FeedQueryService.cs` (existing `GET /api/v1/feed` with `pageSize` and `offset` query parameters is fully capable).
3. Modifications to `FeedTimelineCard.vue` internal comment and reaction logic (card component is reused as a modular timeline item).

---

# 4. Technical Decisions

## TD-001: IntersectionObserver Sentinel Architecture

An `IntersectionObserver` instance is mounted to watch a sentinel `<div>` element placed immediately following the `<FeedTimelineCard>` list.

```text
[FeedView.vue]
  ├── Feed Header & Actions
  ├── Feed Filter Alert / Error Alerts
  ├── Feed Stream Container
  │     ├── <FeedTimelineCard :item="item1" />
  │     ├── <FeedTimelineCard :item="item2" />
  │     ├── ...
  │     ├── [Bottom Loading Spinner] (v-if="isLoadingMore")
  │     ├── [Inline Retry Control]   (v-if="loadMoreError")
  │     ├── [End of Feed Milestone]  (v-if="!hasMore && feedItems.length > 0")
  │     └── <div ref="sentinelRef" class="op-feed-sentinel" /> (Observed Target)
  └── Sticky Sidebar Context Panel
```

**Observer Lifecycle**:
```typescript
let observer: IntersectionObserver | null = null

function setupObserver(): void {
  disconnectObserver()
  if (!sentinelRef.value) return

  observer = new IntersectionObserver(
    (entries) => {
      const entry = entries[0]
      if (entry?.isIntersecting && canLoadMore.value) {
        void loadNextBatch()
      }
    },
    { root: null, rootMargin: '250px', threshold: 0.1 }
  )

  observer.observe(sentinelRef.value)
}

function disconnectObserver(): void {
  if (observer) {
    observer.disconnect()
    observer = null
  }
}
```

- `rootMargin: '250px'`: Starts fetching the next batch 250px before the user hits the exact bottom for seamless zero-wait scrolling.
- `canLoadMore`: Evaluated as `!isLoadingFeed.value && !isLoadingMore.value && hasMore.value && !loadMoreError.value`.

## TD-002: Batch Append & ID Deduplication

When `loadNextBatch()` is triggered:
1. Set `isLoadingMore = true` and `loadMoreError = null`.
2. Compute next offset: `nextOffset = filters.offset + filters.pageSize`.
3. Query `GET /api/v1/feed` with `pageSize: filters.pageSize` and `offset: nextOffset`.
4. On success:
   - Extract `newItems` from response (`response.data.items ?? response.data.feedItems ?? []`).
   - Deduplicate incoming items against existing `feedItems`:
     ```typescript
     const existingKeys = new Set(feedItems.value.map(resolveFeedItemKey))
     const uniqueNewItems = newItems.filter(item => !existingKeys.has(resolveFeedItemKey(item)))
     feedItems.value.push(...uniqueNewItems)
     ```
   - Update `filters.offset = nextOffset`.
   - Update `totalCount` and `hasMore` from response data.
5. On error:
   - Set `loadMoreError = extractErrorMessage(err, 'Failed to load more items')`.
   - Keep existing `feedItems` intact.
6. In `finally`:
   - Set `isLoadingMore = false`.

## TD-003: Stream Prepending for Newly Created Requests

When a user creates a request via `CreateRequestModal.vue` (`handleRequestSaved`):
1. Query the newly created post or synthesize a transient `FeedItem` representing the created request.
2. If `GET /api/v1/feed` is queried for the single latest post (or `feedItems` prepending):
   - Prepend the item: `feedItems.value.unshift(newItem)`.
   - Ensure deduplication in case background refresh runs.
   - Increment `totalCount.value += 1`.
   - Set `createdRequestAlert.value = { id, title }`.
   - **Do not reset scroll position** (viewport remains stable where the user was reading).

## TD-004: End of Feed & Incremental Error Recovery States

**End of Feed (`!hasMore && feedItems.length > 0`)**:
Render a clean Bootstrap card milestone:
```html
<div v-if="!hasMore && feedItems.length > 0" class="card border-0 bg-light text-center py-3 my-2" data-testid="feed-end-milestone">
  <div class="card-body py-2">
    <i class="bi bi-check2-circle text-success fs-5 d-block mb-1" aria-hidden="true"></i>
    <span class="small fw-semibold text-secondary">You're all caught up</span>
    <p class="text-muted fs-11 mb-0">All {{ totalCount }} operational feed events have been loaded.</p>
  </div>
</div>
```

**Incremental Error State (`loadMoreError`)**:
```html
<div v-if="loadMoreError" class="card border-danger border-opacity-25 bg-danger bg-opacity-10 text-center py-2 my-2" data-testid="feed-load-more-error">
  <div class="card-body py-1 d-flex align-items-center justify-content-between">
    <span class="small text-danger"><i class="bi bi-exclamation-circle me-1"></i>{{ loadMoreError }}</span>
    <button type="button" class="btn btn-outline-danger btn-sm py-0.5 px-2 fs-11" @click="loadNextBatch">
      <i class="bi bi-arrow-clockwise me-1"></i>Retry
    </button>
  </div>
</div>
```

## TD-005: Debounced Search & Instant Filter Updates

1. **Customer / Product / Exception Selects**:
   - `@change="handleFilterChange"` immediately clears `filters.offset = 0`, calls `loadFeed()`, and scrolls top if necessary.
2. **Search Input**:
   - Wrapped in a debounced watcher or input handler (300ms delay):
     ```typescript
     let searchDebounceTimer: number | null = null
     function onSearchInput(): void {
       if (searchDebounceTimer) clearTimeout(searchDebounceTimer)
       searchDebounceTimer = window.setTimeout(() => {
         applyFilters()
       }, 300)
     }
     ```
   - Explicit `Apply Filters` button and `Reset` button remain functional.

## TD-006: Contextual Sidebar Cleansing

In the right sidebar "Operational Stream Summary" card:
- Remove: `Current Page (Page X / Y)` row.
- Retain:
  - `Total Stream Events`: `{{ totalCount }}`
  - `Exceptions Loaded`: `{{ exceptionCount }}` (dynamic count of exception items currently loaded in `feedItems`)
  - `Stream Filter Status`: `Filtered` / `Unfiltered` badge.

---

# 5. Component Responsibilities

| Component / File | Responsibility |
|------------------|----------------|
| [FeedView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue) | Screen `SCR-FEED-001`: Manages continuous infinite scroll state, `IntersectionObserver` sentinel, batch appends, deduplication, stream prepending, debounced search, dropdown filtering, end-of-stream milestone, and sidebar metrics. |
| [FeedTimelineCard.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/FeedTimelineCard.vue) | Renders individual timeline card, lazy comments, comment expansion window, inline comment submission, floating reaction palette, and modal trigger events. |
| [main.css](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/assets/main.css) | Defines CSS rules for `.op-feed-sentinel`, `.op-feed-end-milestone`, bottom spinner animations, and error retry layouts. |

---

# 6. Integration Design

```text
[User Scrolling Feed Stream]
       │
       ▼ (Sentinel enters viewport 250px before bottom)
[IntersectionObserver Callback]
       │
       ├── Check: !isLoadingFeed && !isLoadingMore && hasMore && !loadMoreError
       ▼
[loadNextBatch()]
       │
       ├── Set isLoadingMore = true
       ├── GET /api/v1/feed?pageSize=20&offset=N ──► [FeedController]
       │                                                    │
       │◄── Return FeedPageResultDto { items, hasMore, ... }
       │
       ├── Deduplicate & feedItems.push(...newItems)
       ├── Update offset, totalCount, hasMore
       └── Set isLoadingMore = false
```

---

# 7. Data Ownership

| Data Object | Owner Component | Lifecycle & Storage |
|-------------|-----------------|---------------------|
| Cumulative Feed Items (`feedItems`) | `FeedView.vue` | Component ref array, initialized on load and appended incrementally. |
| Pagination state (`offset`, `pageSize`, `hasMore`, `totalCount`) | `FeedView.vue` | Component reactive state, synchronized with backend responses. |
| Dual Loading States (`isLoadingFeed`, `isLoadingMore`) | `FeedView.vue` | Component reactive flags preventing concurrent fetch collisions. |
| Filter & Search criteria (`filters`) | `FeedView.vue` | Reactive filter object driving query parameters. |
| Active User Identity & Token | `useAuthStore` | Global Pinia store. |

---

# 8. Database Design

## New Tables
*None*.

## Modified Tables
*None*.

## Migration Considerations
*None required*. Operates 100% on existing database schema and APIs.

---

# 9. Cross-Cutting Concerns

1. **Observer Cleanup & Memory Safety**:
   - `IntersectionObserver` is disconnected on `onUnmounted` or when `hasMore` becomes false, preventing background memory leaks.
2. **Scroll Continuity**:
   - Appending items does not alter the current scroll offset.
   - Prepending new items at index 0 preserves viewport stability.
   - Applying filters or manual refresh resets offset to 0 and scrolls to top.
3. **Concurrency & Throttling**:
   - `isLoadingMore` flag prevents multiple simultaneous API requests if the user scrolls vigorously.
4. **Network Glitch Resilience**:
   - Failure to load a subsequent batch leaves previously loaded items untouched and shows an inline retry button.

---

# 10. Implementation Constraints

1. **Vue 3 Composition API**: Must use `<script setup lang="ts">` with standard reactive primitives (`ref`, `reactive`, `computed`, `watch`, `onMounted`, `onUnmounted`).
2. **Browser Native Observer**: Must use standard native browser `IntersectionObserver` API without adding third-party npm packages.
3. **Bootstrap 5 UI Conformity**: Must utilize Bootstrap 5 utility classes and Bootstrap Icons (`bi-*`).
4. **Zero Backend Schema Changes**: Must strictly consume existing `GET /api/v1/feed` parameters (`pageSize`, `offset`, `customerId`, `productId`, `isException`, `exceptionType`, `searchTerm`).

---

# 11. Acceptance Conditions

1. **Continuous Scroll**: Scrolling down near the bottom of the feed automatically loads and appends the next batch of 20 items without page reloading or clicking buttons.
2. **Loading States**: A bottom loading spinner renders while subsequent batches are loading (`isLoadingMore`).
3. **End of Stream Milestone**: When all items are retrieved (`!hasMore`), a clean "You're all caught up" milestone card appears at the bottom.
4. **Inline Retry**: If an incremental fetch fails, existing items remain visible and an inline retry button allows re-fetching the batch.
5. **Stream Prepending**: Creating a new request via `CreateRequestModal.vue` prepends the new post to the top of `feedItems` and increments `totalCount` without jumping the scroll position.
6. **Debounced Search**: Typing into the search input auto-applies filtering after a 300ms debounce.
7. **Instant Dropdown Filters**: Selecting a Customer, Product, or Exception filter immediately resets to offset 0 and refreshes the feed stream.
8. **Sidebar Summary Cleansed**: The right sidebar "Operational Stream Summary" card displays Total Stream Events, Exceptions Loaded, and Stream Filter Status without any "Current Page" row.
9. **Zero Build or TypeScript Errors**: Frontend passes `npm run build` or typecheck cleanly.
