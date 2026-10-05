---
Title: Timeline-Style Operational Feed with Default-Expanded Comments and Inline Interaction Implementation Plan
Code: CR-012
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-05
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement the Facebook-style social timeline operational feed with default-expanded comments (showing the last 2 comments with an inline expansion toggle for $> 2$ comments), permanently anchored inline comment input (<kbd>Enter</kbd> to submit, <kbd>Shift</kbd>+<kbd>Enter</kbd> for newline, and Send button), Facebook-style floating reaction picker (quick toggle for `SEEN` and popup palette for all 6 operational reactions), and encapsulated component architecture in accordance with [CR-012-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-012-ARCHITECTURE.md) and [CR-012-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-012-FEASIBILITY-ASSESSMENT.md).

**Planning Mode:** FEATURE-PLANNING

**Referenced artifacts:**
- ISSUE: [CR-012-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-012-ISSUE.md)
- FEASIBILITY-ASSESSMENT: [CR-012-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-012-FEASIBILITY-ASSESSMENT.md) (Planning Readiness: `READY-FOR-PLANNING`)
- ARCHITECTURE: [CR-012-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-012-ARCHITECTURE.md)
- UI Layout Spec: [10-scr-feed-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/10-scr-feed-001.md)
- Post Detail Spec: [09-scr-post-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/09-scr-post-001.md)

**Architecture Applicability:** ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers styling tokens for timeline components, creation of `FeedTimelineCard.vue` encapsulating per-card comment and reaction lifecycles, refactoring of `FeedView.vue` to integrate the timeline card while preserving two-column layouts and filter capabilities, and verification through clean frontend TypeScript build.

Scope breakdown:
- **Design System & Styling (`main.css`)**:
  - Add CSS classes for floating reaction palette (`.op-reaction-palette`), action bar buttons (`.op-action-btn`), comment bubbles (`.op-comment-bubble`), and user avatar initials (`.op-avatar-circle`).
- **New Component (`FeedTimelineCard.vue`)**:
  - Encapsulate per-post state: `comments`, `showAllComments`, `isLoadingComments`, `newCommentText`, `isSubmittingComment`, `reactionCounts`, `activeReactionType`, and `showReactionPalette`.
  - Asynchronously fetch comments via `GET /api/v1/posts/${postId}/comments` when `item.commentCount > 0`.
  - Compute 2-comment windowing: display last 2 comments by default and render `"View previous comments (X)"` button when $> 2$ comments exist to expand all comments inline.
  - Render anchored inline comment input box with <kbd>Enter</kbd> to submit (<kbd>Shift</kbd>+<kbd>Enter</kbd> for newline) and Send button calling `POST /api/v1/posts/${postId}/comments`.
  - Render Facebook-style action bar with quick-toggle `SEEN` reaction and floating palette for all 6 operational reactions calling `POST` / `DELETE` reaction endpoints.
  - Render static card container with explicit modal open triggers on post title and "View full thread" button.
- **Feed View Refactoring (`FeedView.vue`)**:
  - Replace inline `.op-feed-card` rendering with `<FeedTimelineCard :item="item" @open-modal="openPostDetailModal" @post-updated="handlePostUpdated" />`.
  - Maintain two-column layout, active filters, search, exception toggles, and pagination.
- **Verification & Build**:
  - Execute `npm run build` in `cakra/src/frontend/Cakra.Web` to verify clean compilation.

---

# 3. Dependencies

**External Dependencies:** None.

**Slice Dependencies:** Listed in `Depends On` field for each slice below.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---------|---------|---------|---------|
| P1 - Timeline Card & Interaction Styling | IMPLEMENTED | GO | 1/1 |
| P2 - Feed Timeline Card Component Creation | IMPLEMENTED | GO | 1/1 |
| P3 - Operational Feed Integration | IMPLEMENTED | GO | 1/1 |
| P4 - Verification & Build | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Timeline Card & Interaction Styling

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P1-S01

**Title:** Add Timeline Card and Micro-Interaction Styles in `main.css`

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Define CSS rules in [main.css](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/assets/main.css) for Facebook-style floating reaction palette, action bar buttons, comment bubbles, and avatar circles.

**Depends On:** None

**Repository:** Cakra.Web

**Completion Criteria:**
- In `main.css`, class `.op-reaction-palette` is defined with floating positioning, subtle shadow, rounded pill shape, z-index layering, and scale micro-interactions.
- Class `.op-action-btn` is defined with transparent border, text centering, and hover background transitions.
- Class `.op-comment-bubble` is defined with light background (`rgba(0,0,0,0.035)` / `#f0f2f5`), rounded-3 / rounded-4 borders, and compact padding.
- Class `.op-avatar-circle` is defined for 28px–32px circular badges displaying author initials.

**Implementation Notes:**
- Added `.op-reaction-palette`, `.op-reaction-palette-btn`, and keyframe animation `opReactionPop` for floating Facebook-style reaction palette with hover scaling.
- Added `.op-action-btn` for borderless flex action bar buttons with hover transitions and active styling.
- Added `.op-comment-bubble` with rounded speech bubble styling and subtle light background.
- Added `.op-avatar-circle` (and `-sm`, `-md` variants) for author avatar initial badges with navy-to-cyan gradient background.

**Changed Files:**
- `cakra/src/frontend/Cakra.Web/src/assets/main.css`

---

## P2 - Feed Timeline Card Component Creation

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P2-S02

**Title:** Implement `FeedTimelineCard.vue` Component

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Create [`FeedTimelineCard.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/FeedTimelineCard.vue) implementing per-card asynchronous comments, 2-comment windowing & expansion toggle, inline comment input, and Facebook-style floating reaction picker.

**Depends On:** P1-S01

**Repository:** Cakra.Web

**Completion Criteria:**
- File `cakra/src/frontend/Cakra.Web/src/components/FeedTimelineCard.vue` is created using `<script setup lang="ts">`.
- Automatically fetches comments via `GET /api/v1/posts/${postId}/comments` on mount when `item.commentCount > 0`.
- Renders post header (status badges, exception badge, title clickable to open modal, customer & product tags, author, and timestamp) and content excerpt.
- Displays Engagement Summary row: reaction breakdown icons with total count, and comment count.
- Displays Facebook-style Action Bar:
  - React button with quick-toggle `SEEN` and floating palette for 6 reactions (`SEEN`, `EXPERIENCED`, `HAVE_IDEA`, `SIMILAR_ISSUE`, `DUPLICATE`, `NEED_CLARIFICATION`).
  - Comment button focusing the inline textarea.
  - "View full thread" button emitting `open-modal`.
- Displays Comments Section:
  - If total comments $> 2$ and not expanded, shows `"View previous comments (X)"` button.
  - Displays last 2 comments by default (or all comments if expanded) inside `.op-comment-bubble` elements with author name, relative timestamp, and comment content.
- Displays anchored Inline Comment Input box:
  - Textarea with <kbd>Enter</kbd> submission (<kbd>Shift</kbd>+<kbd>Enter</kbd> newline) and Send button calling `POST /api/v1/posts/${postId}/comments`.
  - Appends created comment immediately to local comments list, increments `commentCount`, and resets input.
- Emits `open-modal` and `post-updated` events to parent.

**Implementation Notes:**
- Created `FeedTimelineCard.vue` component encapsulating all per-post timeline states and REST interactions.
- Added lazy loading of comments via `GET /api/v1/posts/${postId}/comments` when post has `commentCount > 0`.
- Computed `displayedComments` to show the last 2 comments by default and render `"View previous comments (X)"` toggle button when $> 2$ comments exist.
- Implemented anchored inline comment form with current user avatar initials badge, <kbd>Enter</kbd> key submission, <kbd>Shift</kbd>+<kbd>Enter</kbd> newline support, and Send button calling `POST /api/v1/posts/${postId}/comments`.
- Implemented Facebook-style action bar with quick-toggle `SEEN` reaction and floating palette for all 6 operational reaction types calling `POST`/`DELETE` `/api/v1/posts/${postId}/reactions`.
- Added modal delegation with explicit triggers on post title and "View full thread" button emitting `open-modal`.
- Successfully validated TypeScript types with `npm run type-check`.

**Changed Files:**
- `cakra/src/frontend/Cakra.Web/src/components/FeedTimelineCard.vue`

---

## P3 - Operational Feed Integration

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P3-S03

**Title:** Refactor `FeedView.vue` to Host `FeedTimelineCard`

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Update [FeedView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue) to replace inline card markup with `<FeedTimelineCard>`, maintaining the two-column layout, active filters, search, exception toggles, and modal handlers.

**Depends On:** P2-S02

**Repository:** Cakra.Web

**Completion Criteria:**
- `FeedView.vue` imports and uses `<FeedTimelineCard>` within `v-for="item in feedItems"`.
- Binds `:item="item"`, `@open-modal="openPostDetailModal"`, and `@post-updated="handlePostUpdated"`.
- Retains existing filter sidebar, search, exception toggling, pagination controls, and `PostDetailModal.vue` integration without regression.
- Preserves all data test attributes (`data-testid`).

**Implementation Notes:**
- Imported and rendered `<FeedTimelineCard>` inside `feed-card-list` iterating over `feedItems`.
- Bound `:item="item"`, `:customer-name-map="customerNameById"`, `:product-name-map="productNameById"`, `@open-modal="openPostDetailModal"`, and `@post-updated="handlePostUpdated"`.
- Preserved responsive two-column grid layout, sticky filter and metrics sidebar, customer/product selects, exception toggle, pagination, and `PostDetailModal.vue` integration.
- Cleaned up obsolete redundant local formatting functions from `FeedView.vue`.
- Validated clean production build with `npm run build` (0 TypeScript / Vue compiler errors).

**Changed Files:**
- `cakra/src/frontend/Cakra.Web/src/views/FeedView.vue`

---

## P4 - Verification & Build

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P4-S04

**Title:** Frontend TypeScript Compilation and Build Verification

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Verify that the frontend compiles cleanly without TypeScript errors, lint errors, or bundling issues.

**Depends On:** P1-S01, P2-S02, P3-S03

**Repository:** Cakra.Web

**Completion Criteria:**
- `npm run build` in `cakra/src/frontend/Cakra.Web` succeeds with zero errors (`exit code 0`).
- No broken imports or type mismatches.

**Implementation Notes:**
- Executed `npm run build` (`vue-tsc --noEmit && vite build`) in `cakra/src/frontend/Cakra.Web`.
- Successfully validated TypeScript types and production asset bundling without errors (`exit code 0`).

**Changed Files:**
- None (verification slice)

---

# 6. Change Log

- 2026-10-05: Initial creation of CR-012-IMPLEMENTATION-PLAN with 4 phases and 4 continuous slices. Execution Approval set to APPROVED.
