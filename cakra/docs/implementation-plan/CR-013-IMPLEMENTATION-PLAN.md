---
Title: Continuous Infinite Scroll Timeline for Operational Feed Implementation Plan
Code: CR-013
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-05
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement the continuous infinite scroll timeline stream for the Operational Feed ([`FeedView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue) / `SCR-FEED-001`), replacing discrete page pagination with native `IntersectionObserver` sentinel auto-loading, incremental batch appending with ID deduplication, seamless stream prepending for newly created requests without viewport displacement, end-of-stream milestone ("You're all caught up"), inline error recovery retry, debounced search filtering (300ms), and cleansed sidebar summary metrics in accordance with [CR-013-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-013-ARCHITECTURE.md), [CR-013-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-013-FEASIBILITY-ASSESSMENT.md), and [CR-013-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-013-ISSUE.md).

**Planning Mode:** FEATURE-PLANNING

**Referenced artifacts:**
- ISSUE: [CR-013-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-013-ISSUE.md)
- FEASIBILITY-ASSESSMENT: [CR-013-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-013-FEASIBILITY-ASSESSMENT.md) (Planning Readiness: `READY-FOR-PLANNING`)
- FEATURE: [FEAT-AWR-001-observe-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-AWR-001-observe-operational-feed.md)
- FEATURE: [FEAT-FCOL-005-filter-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-FCOL-005-filter-operational-feed.md)
- ARCHITECTURE: [CR-013-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-013-ARCHITECTURE.md)
- UI Layout Spec: [10-scr-feed-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/10-scr-feed-001.md)

**Architecture Applicability:** ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers CSS styling tokens for the infinite scroll stream sentinel, end milestone, and retry states; refactoring `FeedView.vue` with `IntersectionObserver` auto-fetching, batch deduplication, dual-phase loading flags, debounced search filtering, and sidebar metrics adjustments; and end-to-end frontend build verification.

Scope breakdown:
- **Design System & Styling (`main.css`)**:
  - Add CSS classes for the feed sentinel target (`.op-feed-sentinel`), end-of-stream milestone card (`.op-feed-end-milestone`), bottom spinner animation, and inline error retry container.
- **Continuous Scroll Implementation (`FeedView.vue`)**:
  - Implement `IntersectionObserver` sentinel lifecycle (`setupObserver`, `disconnectObserver`, `sentinelRef`) with a `250px` rootMargin.
  - Implement dual loading states: `isLoadingFeed` (initial/filter loads) and `isLoadingMore` (background incremental batch fetching).
  - Implement `loadNextBatch()` with offset increments (`filters.offset += filters.pageSize`) and Set-based ID deduplication (`postId` / `feedItemId`).
  - Replace discrete pagination controls (`feed-pagination`, `feed-pagination-prev`, `feed-pagination-next`) with bottom loading spinner, "You're all caught up" milestone card (`feed-end-milestone`), and inline error retry button (`feed-load-more-error`).
  - Implement debounced search input watcher/handler (300ms delay) that automatically resets offset to 0 and reloads from top.
  - Implement real-time dropdown filter changes that immediately reset offset to 0 and reload.
  - Update `handleRequestSaved` to prepend the newly created request/post directly to index 0 of `feedItems` and increment `totalCount` without jumping the user's scroll position.
  - Cleanse the right sidebar summary card by removing the obsolete "Current Page" row, retaining Total Stream Events, Exceptions Loaded, and Stream Filter Status.
- **Verification & Build**:
  - Execute `npm run build` in `cakra/src/frontend/Cakra.Web` to verify clean TypeScript compilation and asset bundling.

---

# 3. Dependencies

**External Dependencies:** None.

**Slice Dependencies:** Listed in `Depends On` field for each slice below.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---------|---------|---------|---------|
| P1 - Continuous Scroll & Milestone Styling | IMPLEMENTED | GO | 1/1 |
| P2 - Infinite Scroll & Interactivity in FeedView | IMPLEMENTED | GO | 1/1 |
| P3 - Verification & Build | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Continuous Scroll & Milestone Styling

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P1-S01

**Title:** Add Continuous Scroll Sentinel and Milestone Styles in `main.css`

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Define CSS rules in [main.css](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/assets/main.css) for the feed sentinel target, end-of-stream milestone card, bottom loading spinner, and inline error retry container.

**Depends On:** None

**Repository:** Cakra.Web

**Completion Criteria:**
- In `main.css`, class `.op-feed-sentinel` is defined with minimal layout footprint for IntersectionObserver tracking.
- Class `.op-feed-end-milestone` is defined with clean card styling, subtle border, and muted text for the "You're all caught up" state.
- Class `.op-feed-retry-box` is defined for inline error recovery at the bottom of the feed stream.

**Implementation Notes:**
- Defined `.op-feed-sentinel` with zero visibility, 2px height, disabled pointer events, and full width for seamless IntersectionObserver detection.
- Defined `.op-feed-end-milestone` with dashed borders, light background, centered alignment, and hover state.
- Defined `.op-feed-retry-box` with alert danger styling for inline retry actions.
- Defined `.op-feed-bottom-loading` for bottom background batch loader layout.

**Changed Files:**
- `cakra/src/frontend/Cakra.Web/src/assets/main.css`

---

## P2 - Infinite Scroll & Interactivity in FeedView

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P2-S02

**Title:** Implement Continuous Infinite Scroll Stream in `FeedView.vue`

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Refactor [FeedView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue) to replace discrete page pagination with native `IntersectionObserver` infinite scrolling, incremental batch appending with ID deduplication, stream prepending for newly created posts, debounced search filtering (300ms), and cleansed sidebar summary metrics.

**Depends On:** P1-S01

**Repository:** Cakra.Web

**Completion Criteria:**
- `FeedView.vue` implements `IntersectionObserver` observing `<div ref="sentinelRef">` with a `250px` rootMargin, properly disconnecting on unmount.
- Distinguishes between `isLoadingFeed` (initial full loading state) and `isLoadingMore` (bottom background batch spinner).
- `loadNextBatch()` fetches the next batch (`pageSize: 20`, `offset: nextOffset`), deduplicates incoming items by ID against `feedItems`, and appends them.
- Replaces `<nav class="feed-pagination">` controls with:
  - Bottom loading spinner (`data-testid="feed-bottom-loading"`) when `isLoadingMore` is true.
  - "You're all caught up" milestone card (`data-testid="feed-end-milestone"`) when `!hasMore` and `feedItems.length > 0`.
  - Inline error card (`data-testid="feed-load-more-error"`) with retry button when a background batch fetch fails.
- `handleRequestSaved` prepends the newly created request/post to index 0 of `feedItems`, increments `totalCount`, and displays the success alert without resetting the viewport.
- Search input automatically applies with a 300ms debounce while retaining explicit "Apply Filters" and "Reset" buttons.
- Dropdown filters (Customer, Product, Exception Type, Exceptions Only) immediately reset offset to 0 and reload.
- Right sidebar summary card removes the "Current Page" row and displays Total Stream Events, Exceptions Loaded, and Stream Filter Status.

**Implementation Notes:**
- Integrated native browser `IntersectionObserver` monitoring sentinel ref element (`.op-feed-sentinel`) with 250px rootMargin and automatic cleanup on unmount or when `hasMore` is false.
- Structured dual loading flags (`isLoadingFeed` and `isLoadingMore`) preventing race conditions and rendering distinct loading spinners.
- Implemented `loadNextBatch()` fetching 20 items per incremental batch, deduplicating incoming items against existing `feedItems` by key (`postId` / `feedItemId`).
- Replaced discrete page pagination controls with bottom loader (`feed-bottom-loading`), milestone card (`feed-end-milestone`), and retry box (`feed-load-more-error`).
- Updated `handleRequestSaved` to prepend newly created request items directly to index 0 and increment `totalCount` without viewport displacement.
- Wired 300ms debounce timer for search input and instant reload triggers for customer, product, and exception dropdown filters.
- Cleansed sidebar summary card by removing obsolete Current Page row and displaying dynamic Exceptions Loaded count and Filtered/Unfiltered status.

**Changed Files:**
- `cakra/src/frontend/Cakra.Web/src/views/FeedView.vue`

---

## P3 - Verification & Build

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P3-S03

**Title:** Frontend TypeScript Compilation and Build Verification

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Verify that the frontend compiles cleanly without TypeScript errors, Vue template errors, or bundling issues.

**Depends On:** P1-S01, P2-S02

**Repository:** Cakra.Web

**Completion Criteria:**
- `npm run build` in `cakra/src/frontend/Cakra.Web` executes successfully with exit code 0.
- No type errors or broken component bindings in `FeedView.vue` or `FeedTimelineCard.vue`.

**Implementation Notes:**
- Successfully executed `vue-tsc --noEmit && vite build` in `cakra/src/frontend/Cakra.Web`.
- Verified clean TypeScript compilation and asset bundling with 0 errors and clean exit code 0.

**Changed Files:**
- None (verification slice)

---

# 6. Change Log

- 2026-10-05: Initial creation of CR-013-IMPLEMENTATION-PLAN with 3 phases and 3 continuous slices. Execution Approval set to APPROVED.
