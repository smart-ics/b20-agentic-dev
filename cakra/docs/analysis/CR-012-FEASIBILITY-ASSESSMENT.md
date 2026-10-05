---
Title: Feasibility Assessment for Timeline-Style Operational Feed with Default-Expanded Comments and Inline Interaction
Code: CR-012
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-05
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Feature being assessed: Timeline-Style Operational Feed with Default-Expanded Comments and Inline Interaction, per request in [CR-012-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-012-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-012-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-012-ISSUE.md)
- ARCHITECTURE (Reference): [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- UI Layout Spec (Reference): [10-scr-feed-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/10-scr-feed-001.md)
- Post Detail Spec (Reference): [09-scr-post-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/09-scr-post-001.md)

## Objective

Assess the technical feasibility, baseline codebase state, interaction gaps, architectural impact, and planning readiness to:

1. Transform the Operational Feed ([`FeedView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue) / `SCR-FEED-001`) into a Facebook-style timeline experience.
2. Render comments expanded by default on each operational feed card, showing the last 2 comments initially if $> 2$ comments exist, with an inline "View previous comments (X)" expansion toggle.
3. Provide an always-visible inline comment input box anchored at the bottom of each feed card with immediate keyboard (<kbd>Enter</kbd>) and button submission.
4. Provide a Facebook-style inline reaction picker with quick-toggle default reaction (`SEEN`) and a floating palette for all 6 operational reaction types (`SEEN`, `EXPERIENCED`, `HAVE_IDEA`, `SIMILAR_ISSUE`, `DUPLICATE`, `NEED_CLARIFICATION`).
5. Ensure smooth engagement statistics display and non-conflicting card interaction (static card body with explicit modal triggers via title and "View full thread").

---

# 2. Current State

Summarize relevant findings from current artifacts and codebase. This section contains facts only.

## Existing Behavior

1. **Operational Feed Screen ([`FeedView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue))**:
   - `FeedView.vue` calls `GET /api/v1/feed` to retrieve projected feed cards (`FeedItemDto`).
   - Each card displays `Title`, `Author`, `Customer`, `Product`, `CreatedAt`, `CommentCount`, `ReactionCount`, `ContentExcerpt`, and `LatestCommentExcerpt`.
   - The entire card is wrapped in a clickable container `@click="openPostDetailModal(item)"` that opens `PostDetailModal.vue` (`SCR-POST-001`).
   - Comments cannot be read in full, expanded, or created directly from the feed card.
   - Reactions cannot be chosen, added, or removed directly from the feed card.
2. **Post Detail Modal ([`PostDetailModal.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/PostDetailModal.vue))**:
   - Fetches thread details (`GET /api/v1/posts/${postId}`), chronological comments (`GET /api/v1/posts/${postId}/comments`), and active reactions (`GET /api/v1/posts/${postId}/reactions`).
   - Implements full comment composition calling `POST /api/v1/posts/${postId}/comments`.
   - Implements reaction toggling calling `POST /api/v1/posts/${postId}/reactions` and `DELETE /api/v1/posts/${postId}/reactions/${reactionType}`.
3. **Backend Post & Feed API Endpoints ([`PostsController.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/PostsController.cs), [`FeedController.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/FeedController.cs))**:
   - `GET /api/v1/feed` provides paginated feed items with aggregated counts.
   - `GET /api/v1/posts/{id}/comments` returns all active comments in chronological order.
   - `POST /api/v1/posts/{id}/comments` creates a new comment under the post.
   - `GET /api/v1/posts/{id}/reactions` returns all active reactions.
   - `POST /api/v1/posts/{id}/reactions` sets/updates a reaction for the authenticated person.
   - `DELETE /api/v1/posts/{id}/reactions/{reactionType}` removes a reaction.

## Existing Constraints

1. **Authentication & Identity**: Any comment or reaction submission requires an authenticated user session (`useAuthStore` / JWT token) providing the current user's identity.
2. **Projection vs. Live Thread Consistency**: Adding a comment or reaction via `POST /api/v1/posts/{id}/*` updates the Post write-side domain and triggers projection updates. Inline UI state on the feed card must reflect changes immediately (optimistically or via immediate local list update) without requiring a full feed re-fetch.
3. **Responsive UI & Event Propagation**: Card interaction must prevent event collisions: clicking reaction buttons, expanding comments, or typing in the textarea must never trigger the modal or page navigation.

---

# 3. Gap Analysis

Identify gaps between the requested change and the current system.

| ID | Severity | Gap |
|------|------|------|
| GAP-001 | CRITICAL | `FeedView.vue` does not display comments directly on the feed card by default. |
| GAP-002 | CRITICAL | `FeedView.vue` lacks the logic to show the 2 most recent comments by default when $> 2$ comments exist and toggle full comment history inline via "View previous comments (X)". |
| GAP-003 | CRITICAL | `FeedView.vue` lacks an inline comment input box anchored at the bottom of each card supporting keyboard submission (<kbd>Enter</kbd>) and an explicit Send button. |
| GAP-004 | CRITICAL | `FeedView.vue` lacks an inline Facebook-style reaction bar with quick toggle (`SEEN`) and a floating reaction palette for all 6 operational reaction types. |
| GAP-005 | MAJOR | Card root element in `FeedView.vue` captures all clicks to open `PostDetailModal`, interfering with inline interaction. The modal trigger must be separated from the card background. |
| GAP-006 | MAJOR | Feed cards lack per-card comment and user reaction state management (fetching comments when `commentCount > 0`, tracking user's active reaction, and local optimistic updates). |

---

# 4. Open Questions

Identify unresolved questions and their impact on design and architecture.

| ID | Question | Impact |
|------|------|------|
| OQ-001 | How should comments beyond the latest 2 be loaded and displayed on the feed card? | Determines whether comments are expanded inline via a toggle or delegated to a modal. |
| OQ-002 | How should inline reactions be presented on each feed card? | Determines the reaction UI component design (quick-toggle button with floating palette vs flat toolbar). |
| OQ-003 | What submission behavior should be used for the inline comment input field? | Determines keyboard listener and form submission UX (<kbd>Enter</kbd> to submit, <kbd>Shift</kbd>+<kbd>Enter</kbd> for newline). |
| OQ-004 | How should the Post Detail Modal interact with the new timeline card layout? | Determines card click handling and explicit trigger targets (post title and "View full thread" button). |
| OQ-005 | Should the inline comment input box always be visible on each feed card? | Determines whether the comment box is permanently anchored or revealed on demand. |

---

# 5. Assumptions

Document assumptions made during the assessment.

| ID | Assumption |
|------|------|
| ASM-001 | Existing backend endpoints in `PostsController` (`GET /api/v1/posts/{id}/comments`, `POST /api/v1/posts/{id}/comments`, `POST /api/v1/posts/{id}/reactions`, `DELETE /api/v1/posts/{id}/reactions/{reactionType}`) are fully functional and require no breaking schema changes. |
| ASM-002 | `PostDetailModal.vue` (`SCR-POST-001`) remains available in the application for deep inspection of post metadata, references, and audit history when invoked via post title or "View full thread". |
| ASM-003 | User identity is supplied by `useAuthStore()` (`personId`, `name`, `token`) for validating permissions and highlighting the current user's active reactions. |

---

# 6. Risks

Document identified risks, impacts, and mitigations.

| ID | Risk | Impact | Mitigation |
|------|------|------|------|
| RISK-001 | Fetching comments for every card with `commentCount > 0` on feed load could generate multiple network requests. | Moderate | Fetch comments lazily/asynchronously per card upon mount; do not execute API calls for items with `commentCount === 0`. |
| RISK-002 | Floating reaction palette hover state might not trigger cleanly on touch devices. | Low | Implement both mouse hover and click/tap trigger handlers for the reaction palette with click-outside dismissal. |
| RISK-003 | Accidental comment submission when typing multiline text. | Low | Bind submission to plain <kbd>Enter</kbd> while allowing <kbd>Shift</kbd>+<kbd>Enter</kbd> for multi-line formatting; provide visual hint. |

---

# 7. Recommendations

Recommend possible approaches for addressing major gaps.

## Option A (Recommended)

1. **Extract Timeline Card Component ([`FeedTimelineCard.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/FeedTimelineCard.vue))**:
   - Encapsulate per-card state: active comments list, comment expansion toggle (`showAllComments`), inline comment draft (`newCommentText`), isSubmittingComment, active reaction state, and reaction palette visibility.
   - Asynchronously load comments (`GET /api/v1/posts/{postId}/comments`) on mount if `item.commentCount > 0`.
   - Render Engagement Summary row above action bar (reaction icons + total count, comment count).
   - Render Facebook-style Action Bar:
     - **React Button**: Quick-click toggles `SEEN`. Hover/click reveals floating palette with 6 reactions (`SEEN`, `EXPERIENCED`, `HAVE_IDEA`, `SIMILAR_ISSUE`, `DUPLICATE`, `NEED_CLARIFICATION`).
     - **Comment Button**: Focuses the inline comment input box.
     - **View Full Thread Button**: Emits open modal event to parent.
   - Render Comments Section:
     - "View previous comments (X)" link when total comments $> 2$ and `!showAllComments`.
     - Displays slice of comments (last 2 or all if expanded) in Facebook-style rounded speech bubbles (`bg-light-subtle rounded-3 p-2 mb-1.5`).
   - Render Inline Comment Input:
     - Anchored at bottom with avatar/initials, auto-resizing input / textarea, and Send button.
     - <kbd>Enter</kbd> submits via `POST /api/v1/posts/{postId}/comments`, appends new comment locally, and updates comment count.
2. **Refactor [`FeedView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue)**:
   - Use `<FeedTimelineCard>` in the feed card stream.
   - Keep card background static and handle `openModal` event from card title / "View full thread".

### Advantages

- Clean component architecture with encapsulated per-card state and lifecycle.
- Fully matches Facebook timeline UX expectations.
- Zero breaking backend changes; leverages robust existing REST APIs.
- High maintainability and testability.

### Disadvantages

- Introduces a new subcomponent (`FeedTimelineCard.vue`), requiring component wiring and prop/event testing.

## Option B

Inline all state and markup directly within `FeedView.vue` without creating a child component.

### Advantages

- Single file edit in `FeedView.vue`.

### Disadvantages

- Bloats `FeedView.vue` beyond 1500 lines with complex array-of-objects state mappings for comments, drafts, and popovers across multiple cards.

---

# 8. Gap Closure

Record resolutions for gaps and open questions based on the alignment interview.

## GAP-001 & GAP-002: Default Inline Comment Expansion & 2-Comment Window

### Decision
Comments will be rendered directly on each feed card expanded by default. If a post has more than 2 comments, only the 2 most recent comments are displayed initially, accompanied by a `"View previous comments (X)"` button that expands and renders all earlier comments inline upon click.

### Rationale
Provides immediate conversational context without modal disruption, while keeping high-density feed cards readable without excessive vertical scroll for posts with lengthy discussions.

### Impact
Improves operational awareness and aligns feed scanning with standard social timeline paradigms.

### Architecture Impact
Requires per-card comment state (`comments[]`, `showAllComments: boolean`) and lazy comment fetching on card mount when `commentCount > 0`.

### Resolved By
User (/grill-me interview alignment)

### Resolved Date
2026-10-05

---

## GAP-003 & OQ-003 & OQ-005: Always-Visible Inline Comment Input & Submission

### Decision
An inline comment input box will be permanently anchored at the bottom of each feed card. Pressing <kbd>Enter</kbd> submits the comment immediately, while <kbd>Shift</kbd>+<kbd>Enter</kbd> inserts a new line. An explicit Send button (`bi-send`) is also provided.

### Rationale
Allows instant operational feedback without opening modal forms. Matches familiar communication UX patterns.

### Impact
Enables rapid response to operational events directly from the feed stream.

### Architecture Impact
Card calls `POST /api/v1/posts/{postId}/comments` and immediately appends the created comment to the local card state.

### Resolved By
User (/grill-me interview alignment)

### Resolved Date
2026-10-05

---

## GAP-004 & OQ-002: Facebook-Style Inline Reaction Picker

### Decision
Implement a Facebook-style reaction bar on each feed card. Quick-clicking the primary "React" button toggles the default reaction (`SEEN`). Hovering or clicking the reaction trigger reveals a floating palette displaying all 6 operational reaction types (`SEEN`, `EXPERIENCED`, `HAVE_IDEA`, `SIMILAR_ISSUE`, `DUPLICATE`, `NEED_CLARIFICATION`) with their respective icons, labels, and active states.

### Rationale
Allows rich operational sentiment capture without opening a modal, maintaining high usability for quick acknowledgments.

### Impact
Drastically increases reaction engagement on operational posts.

### Architecture Impact
Card calls `POST /api/v1/posts/{postId}/reactions` and `DELETE /api/v1/posts/{postId}/reactions/{reactionType}`, updating local reaction breakdown and active user reaction state.

### Resolved By
User (/grill-me interview alignment)

### Resolved Date
2026-10-05

---

## GAP-005 & OQ-004: Explicit Modal Triggers & Stable Card Background

### Decision
Make the feed card background static (non-navigating). The `PostDetailModal` (`SCR-POST-001`) is preserved and accessed explicitly by clicking the post title or the dedicated "View full thread" button in the action bar.

### Rationale
Prevents accidental modal popups when users click inside the card to read comments, select text, click reaction buttons, or type in the comment input.

### Impact
Eliminates interaction frustration and misclicks.

### Architecture Impact
Remove root-level click navigation on `.op-feed-card` and bind modal opening specifically to the title link and "View full thread" button.

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

This change introduces significant UI/UX component refactoring, a new timeline card component ([`FeedTimelineCard.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/FeedTimelineCard.vue)), per-card async state lifecycles (lazy comment loading, optimistic local updates, reaction palette popovers), and event delegation. A formal target architecture update and structured implementation plan are required.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved
- [x] All required decisions recorded
- [x] All blocking open questions resolved
- [x] Architecture can be finalized or updated

## Status

**NOT-READY**

## Notes

All functional gaps and open questions have been fully closed during the `/grill-me` alignment interview. In accordance with Knowledge-Centric SDLC rules, the Analyst maintains Status as NOT-READY. The gate to `READY-FOR-PLANNING` will be granted by `ica-architect` upon technical architecture review and update.

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-012-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-012-ISSUE.md)
- UI Layout Spec: [10-scr-feed-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/10-scr-feed-001.md)
- Post Detail Spec: [09-scr-post-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/09-scr-post-001.md)
- System Architecture: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

Referenced codebase locations:

- Operational Feed View: [FeedView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue)
- Post Detail Modal: [PostDetailModal.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/PostDetailModal.vue)
- Post API Controller: [PostsController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/PostsController.cs)
- Feed API Controller: [FeedController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/FeedController.cs)
