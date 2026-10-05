---
Title: Architecture Specification for Timeline-Style Operational Feed with Default-Expanded Comments and Inline Interaction
Code: CR-012
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-05
---

# 1. Overview

This architecture artifact defines the technical realization for transforming the Operational Feed screen ([`FeedView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue) / `SCR-FEED-001`) into a social timeline interaction model (Facebook timeline style), as requested in [CR-012-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-012-ISSUE.md) and assessed in [CR-012-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-012-FEASIBILITY-ASSESSMENT.md).

The target architecture introduces:
1. **Default-Expanded Inline Comments**: Comments are displayed directly on each feed card. If a post has more than 2 comments, only the 2 most recent comments are shown by default with an inline "View previous comments (X)" expansion toggle.
2. **Anchored Inline Comment Input**: An always-visible comment box at the bottom of each feed card with instant keyboard (<kbd>Enter</kbd> to submit, <kbd>Shift</kbd>+<kbd>Enter</kbd> for newline) and button submission calling `POST /api/v1/posts/{postId}/comments`.
3. **Facebook-Style Inline Reaction Picker**: A primary quick-toggle button (defaulting to `SEEN`) with a floating hover/click reaction palette supporting all 6 operational reaction types (`SEEN`, `EXPERIENCED`, `HAVE_IDEA`, `SIMILAR_ISSUE`, `DUPLICATE`, `NEED_CLARIFICATION`).
4. **Encapsulated Component Architecture**: A dedicated `FeedTimelineCard.vue` component encapsulating per-post asynchronous comment loading, active user reaction state, inline drafts, and local optimistic updates, while preserving the `PostDetailModal.vue` (`SCR-POST-001`) for deep thread inspection.

---

# 2. Architectural Basis

## Business Context

- ISSUE: [CR-012-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-012-ISSUE.md)
- UI Layout Spec: [10-scr-feed-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/10-scr-feed-001.md)
- Post Detail Spec: [09-scr-post-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/09-scr-post-001.md)
- System Architecture: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

## Analysis Input

- FEASIBILITY-ASSESSMENT: [CR-012-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-012-FEASIBILITY-ASSESSMENT.md)
  - Approved Decisions: `GAP-001` through `GAP-005`, `OQ-001` through `OQ-005`.
  - Planning Gate: `READY-FOR-PLANNING` granted by `ica-architect`.

```text
CR-012-ISSUE + CR-012-FEASIBILITY-ASSESSMENT
                     ↓
           CR-012-ARCHITECTURE
```

---

# 3. Scope

## Included

1. **New Component ([`FeedTimelineCard.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/FeedTimelineCard.vue))**:
   - Encapsulates state for an individual operational post: comments array, comment expansion toggle (`showAllComments`), loading indicators, inline comment text draft, isSubmittingComment flag, reaction breakdown counts, active user reaction type, and reaction palette popover state.
   - Lifecycle hook (`onMounted` / reactive watcher): asynchronously loads comments via `GET /api/v1/posts/{postId}/comments` when `item.commentCount > 0`.
   - Renders post metadata header, content excerpt, and contextual reference link (`View Request`).
   - Renders Engagement Statistics Row (reaction counts and badges, total comments counter).
   - Renders Action Bar with:
     - React button with quick-toggle and floating reaction picker.
     - Comment button that focuses the inline comment input.
     - "View full thread" button emitting modal open event to parent.
   - Renders Comments Section with speech bubble styling, relative timestamps, author avatars, and "View previous comments (X)" toggle when $> 2$ comments exist.
   - Renders Inline Comment Input Box with current user avatar, textarea with <kbd>Enter</kbd> submit / <kbd>Shift</kbd>+<kbd>Enter</kbd> newline handling, and Send button.
2. **Refactoring of Operational Feed View ([`FeedView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue))**:
   - Replaces inline card rendering with `<FeedTimelineCard :item="item" @open-modal="openPostDetailModal" @post-updated="handlePostUpdated" />`.
   - Maintains the existing responsive two-column grid (stream column + sticky sidebar panel), filter reactivity, and search capabilities.
3. **CSS Styling & Micro-Interactions ([`main.css`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/assets/main.css))**:
   - Facebook-style floating reaction palette styling with smooth hover/fade animation and z-index layering.
   - Comment bubble styling (`.op-comment-bubble`), avatar initials styling (`.op-avatar-circle`), and action button hover states (`.op-action-btn`).

## Excluded

1. Backend database schema modifications or new database tables (existing PostgreSQL schema and Dapper queries are fully sufficient).
2. Backend REST API modifications in `PostsController` or `FeedController` (all required endpoints already exist).
3. Alterations to `PostDetailModal.vue` internal logic (modal is retained as an explicit detailed view).

---

# 4. Technical Decisions

## TD-001: Component Decomposition and State Encapsulation

To avoid inflating `FeedView.vue` with multi-card complex state arrays (handling comment lists, drafts, hover timeouts, and submission flags for 20+ cards), each feed post is encapsulated in a dedicated Vue 3 component: `FeedTimelineCard.vue`.

```text
[FeedView.vue] (SCR-FEED-001)
  ├── Filter Sidebar & Metrics
  └── Feed Stream List
        └── [FeedTimelineCard.vue] (Instance per FeedItem)
              ├── Post Header & Excerpt
              ├── Engagement Stats Summary
              ├── Action Bar (React / Comment / View Thread)
              │     └── [Floating Reaction Palette]
              ├── Comments List (Last 2 or Expanded All)
              └── Inline Comment Input Form
```

## TD-002: Comment Windowing & Expansion Algorithm

Each feed card maintains:
- `comments = ref<PostCommentItem[]>([])`
- `showAllComments = ref<boolean>(false)`
- `isLoadingComments = ref<boolean>(false)`

When `item.commentCount > 0`, comments are fetched via `GET /api/v1/posts/${postId}/comments`.

Computed displayed comments:
```typescript
const displayedComments = computed<PostCommentItem[]>(() => {
  if (showAllComments.value || comments.value.length <= 2) {
    return comments.value
  }
  return comments.value.slice(-2) // Last 2 comments
})

const hiddenCommentCount = computed<number>(() => {
  return Math.max(0, comments.value.length - 2)
})
```

When `hiddenCommentCount > 0` and `!showAllComments.value`, a button is displayed above the comments:
`"View previous comments (" + hiddenCommentCount + ")"`
Clicking this button sets `showAllComments.value = true` without additional network roundtrips.

## TD-003: Inline Comment Submission & Optimistic UI Update

The inline comment form binds to `newCommentContent = ref('')`.

**Keybinding Rules**:
- Plain <kbd>Enter</kbd> (<kbd>e.key === 'Enter' && !e.shiftKey</kbd>): Triggers `submitComment()`. Prevents default newline insertion.
- <kbd>Shift</kbd> + <kbd>Enter</kbd>: Inserts a standard newline in the textarea.
- Send Button (`bi-send`): Calls `submitComment()`.

**Submission Flow**:
1. Validate non-whitespace text.
2. Set `isSubmitting = true`.
3. Call `POST /api/v1/posts/${postId}/comments` with payload `{ content: newCommentContent.value.trim() }`.
4. On success:
   - Append the returned/created comment to `comments.value`.
   - Increment local `item.commentCount`.
   - Clear `newCommentContent.value = ''`.
   - Automatically reveal new comment (scroll into view or ensure displayed).
   - Emit `post-updated` to parent if needed.
5. On failure: Display inline toast or error text under the comment input.

## TD-004: Facebook-Style Floating Reaction Palette & Quick Toggle

**Reaction Types**:
- `SEEN`: 👍 Seen (default quick toggle)
- `EXPERIENCED`: 👤 Experienced
- `HAVE_IDEA`: 💡 Have Idea
- `SIMILAR_ISSUE`: 🚩 Similar Issue
- `DUPLICATE`: 📄 Duplicate
- `NEED_CLARIFICATION`: ❓ Need Clarification

**Interaction Behavior**:
- **Quick Click on React Button**:
  - If user has already reacted with `SEEN`: calls `DELETE /api/v1/posts/${postId}/reactions/SEEN` and clears active reaction.
  - If user has no active reaction: calls `POST /api/v1/posts/${postId}/reactions` with `{ reactionType: 'SEEN' }` and sets active reaction to `SEEN`.
  - If user has a different active reaction: calls `POST /api/v1/posts/${postId}/reactions` with `{ reactionType: 'SEEN' }` to switch reaction.
- **Hover / Long Press / Click Picker Trigger**:
  - Displays a floating floating pill palette (`.op-reaction-palette`) above the React button.
  - Clicking any reaction icon in the palette invokes `toggleReaction(reactionType)`.
  - Automatically dismisses palette after selection or when mouse leaves / user clicks outside.
- **Local Count Synchronization**:
  - Adjusts local `reactionCounts` dictionary and total `reactionCount` immediately upon successful API response.

## TD-005: Event Isolation & Modal Separation

To eliminate accidental modal activations:
1. The root card element `.op-feed-card` is purely a layout container with `cursor: default` (no root `@click="openModal"`).
2. The post title is styled as a link/button with `@click.stop="openModal"`.
3. The Action Bar contains a dedicated **"View full thread"** button (`.op-action-btn`) with `@click.stop="openModal"`.
4. All child interactive controls (reactions, comment toggles, text input, links) maintain explicit `@click.stop` to prevent bubbling.

## TD-006: Visual Styling & Design System Tokens

Added to [main.css](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/assets/main.css):
- `.op-reaction-palette`: Floating white pill container with subtle shadow (`0 4px 14px rgba(0,0,0,0.15)`), rounded pills, smooth hover scaling (`transform: scale(1.25)`).
- `.op-action-btn`: Borderless, lightweight action button with subtle gray hover background (`var(--bs-gray-100)`), full-width flex child in action bar.
- `.op-comment-bubble`: Light gray background (`#f0f2f5` / `rgba(0,0,0,0.04)`), rounded-4 border radius (`18px`), padding `8px 12px`.
- `.op-avatar-circle`: 28px–32px circular badge displaying author initials or default user icon.

---

# 5. Component Responsibilities

| Component / File | Responsibility |
|------------------|----------------|
| [FeedTimelineCard.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/FeedTimelineCard.vue) | Encapsulates single feed item rendering, lazy comment loading, comment windowing (last 2 vs all), inline comment submission, floating reaction palette, optimistic state updates, and modal open event delegation. |
| [FeedView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue) | Screen `SCR-FEED-001`: Manages feed query parameters, customer/product filter lookups, exception toggle, pagination, and hosts the feed stream with `<FeedTimelineCard>` instances. |
| [main.css](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/assets/main.css) | Defines CSS styling rules for `.op-reaction-palette`, `.op-action-btn`, `.op-comment-bubble`, `.op-avatar-circle`, and responsive timeline card layouts. |
| [PostDetailModal.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/PostDetailModal.vue) | Screen `SCR-POST-001`: Retained for deep inspection of post metadata, full audit history, reference links, and visibility/archival actions when triggered via title or "View full thread". |

---

# 6. Integration Design

```text
[FeedTimelineCard]
   │
   ├── (Mount when commentCount > 0)
   │     └── GET /api/v1/posts/{postId}/comments ──► [PostsController]
   │
   ├── (Quick Click React / Select from Palette)
   │     ├── POST /api/v1/posts/{postId}/reactions ──► [PostsController]
   │     └── DELETE /api/v1/posts/{postId}/reactions/{type} ──► [PostsController]
   │
   ├── (Submit Inline Comment: Enter / Send Button)
   │     └── POST /api/v1/posts/{postId}/comments ──► [PostsController]
   │
   ├── (Click Post Title / "View full thread")
   │     └── emit('open-modal', item) ──► [FeedView.vue] ──► Opens [PostDetailModal.vue]
   │
   └── (Click "View Request")
         └── router.push('/requests/{requestId}')
```

---

# 7. Data Ownership

| Data Object | Owner Component | Lifecycle & Storage |
|-------------|-----------------|---------------------|
| Feed page items & filter params | `FeedView.vue` | Component ref state, populated by `GET /api/v1/feed`. |
| Post comments array & windowing state | `FeedTimelineCard.vue` | Component ref state, populated by `GET /api/v1/posts/{postId}/comments` and mutated by local comment creations. |
| Post reaction counts & user reaction state | `FeedTimelineCard.vue` | Component ref state, initialized from `item.reactionCounts` and updated via reaction API calls. |
| Active user credentials & identity | `useAuthStore` | Global Pinia store (`personId`, `name`, `token`). |
| Permanent post, comment, and reaction data | PostgreSQL Database (`post` schema) | Read/written via `Cakra.Modules.Post` and `PostsController.cs`. |

---

# 8. Database Design

## New Tables
*None*. Existing tables are used without schema modifications.

## Modified Tables
*None*.

## Relationships
Existing relationships between `post.Posts`, `post.Comments`, `post.Reactions`, and `post.FeedItems` projection remain unchanged and fully functional.

## Migration Considerations
*None required*. This change is purely a frontend presentation and interaction enhancement that operates on top of existing, fully tested backend REST endpoints.

---

# 9. Cross-Cutting Concerns

1. **Authentication Context**:
   - `FeedTimelineCard.vue` reads `useAuthStore()` to determine the current user's `personId` for matching existing user reactions and displaying the user's avatar.
2. **Reactivity & Concurrent Updates**:
   - If a post is modified in `PostDetailModal.vue` and closed, `FeedView.vue` handles `@updated` to reload the feed stream, ensuring synchronization.
3. **Error Handling & Resilience**:
   - Network failures during inline comment submission or reaction toggle display localized, non-blocking error toasts rather than crashing the card or refreshing the entire feed.
4. **Keyboard Accessibility**:
   - Comments and reactions support standard keyboard focus, <kbd>Enter</kbd>, <kbd>Space</kbd>, and <kbd>Escape</kbd> (to close floating reaction palettes).

---

# 10. Implementation Constraints

1. **Vue 3 Composition API**: Must use `<script setup lang="ts">` with strict TypeScript typings matching existing models.
2. **Bootstrap 5 & Icons**: Must utilize Bootstrap 5 utility classes and Bootstrap Icons (`bi-*`) without introducing third-party UI libraries.
3. **Existing API Compatibility**: Must strictly consume existing endpoints in `PostsController` (`GET/POST /api/v1/posts/{id}/comments`, `GET/POST/DELETE /api/v1/posts/{id}/reactions`).
4. **No Regressions on Filtering & Pagination**: Feed filtering (Customer, Product, Exceptions, Search) and pagination must continue to operate seamlessly.

---

# 11. Acceptance Conditions

1. **Default-Expanded Comments**: Feed cards with comments display comments inline upon feed loading without opening any modal dialog.
2. **2-Comment Window & Toggle**:
   - Posts with $\le 2$ comments display all comments.
   - Posts with $> 2$ comments display the 2 most recent comments and show a clickable `"View previous comments (X)"` button.
   - Clicking `"View previous comments (X)"` expands and renders all earlier comments inline immediately.
3. **Inline Comment Submission**:
   - An inline comment input box is permanently anchored at the bottom of each feed card.
   - Typing text and pressing <kbd>Enter</kbd> (or clicking Send) submits the comment to `POST /api/v1/posts/{postId}/comments`.
   - The comment appears immediately in the card's comment list, `commentCount` increments, and the input field clears.
   - <kbd>Shift</kbd>+<kbd>Enter</kbd> creates a newline in the input.
4. **Facebook-Style Reactions**:
   - Clicking the React button toggles `SEEN`.
   - Hovering or clicking the reaction trigger reveals a floating palette with all 6 operational reaction types.
   - Selecting a reaction updates the reaction count badge and highlights the user's active reaction.
5. **Modal Access Preserved**:
   - Clicking post title or "View full thread" opens `PostDetailModal.vue`.
   - Clicking within comments, reactions, or the comment box does NOT trigger the modal.
6. **Zero TypeScript or Lint Errors**: Frontend passes `npm run build` or typecheck cleanly.
