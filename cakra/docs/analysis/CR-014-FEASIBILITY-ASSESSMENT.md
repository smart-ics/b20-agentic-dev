---
Title: Feasibility Assessment for Universal Search Textbox for Operational Feed
Code: CR-014
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-05
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Feature being assessed: Universal Search Textbox for Operational Feed, per request in [CR-014-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-014-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-014-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-014-ISSUE.md)
- FEATURE: [FEAT-AWR-001-observe-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-AWR-001-observe-operational-feed.md)
- FEATURE: [FEAT-FCOL-005-filter-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-FCOL-005-filter-operational-feed.md)
- ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- UI Layout Spec: [10-scr-feed-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/10-scr-feed-001.md)

## Objective

Assess the technical feasibility, baseline codebase state, interaction gaps, architectural impact, and planning readiness to:

1. Consolidate search and filtering in the Operational Feed ([`FeedView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue) / `SCR-FEED-001`) into a single universal search textbox prominently located on top of the operational feed stream.
2. Enable universal search across 5 operational dimensions within a single query:
   - Product Name
   - Customer Name
   - Relevant User Name(s) (post author and comment authors)
   - Request Text (request title and description)
   - Comment Text (all active comments)
3. Maintain sub-50ms single-table indexed queries against the `[post].[FeedItems]` materialized read-model table without performing query-time JOINs across domain tables.
4. Denormalize searchable data into a consolidated `[SearchContent] NVARCHAR(4000)` column on `[post].[FeedItems]`, indexed with a SQL Server Full-Text Index.
5. Provide Prefix-AND full-text matching with robust input sanitization to support responsive search-as-you-type without syntax errors.
6. Provide dual-mode graceful fallback: use SQL Server Full-Text Search (`CONTAINS`) when FTS is installed, and seamlessly fall back to parameterized `LIKE` on `[SearchContent]` if FTS is unavailable (e.g. lightweight local dev or CI test runners).
7. Remove the legacy right-sidebar filter card, establishing universal search as the sole feed filter mechanism, while keeping the Operational Stream Summary card in the sidebar.
8. Support 300ms debounced search-as-you-type, in-input clear button, browser URL query parameter sync (`?q=...`), and custom empty states with a quick reset action.

---

# 2. Current State

Summarize relevant findings from current artifacts and codebase. This section contains facts only.

## Existing Behavior

1. **Operational Feed Screen ([`FeedView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue))**:
   - Search and filtering are located inside a "Feed Filters" card in the right sidebar.
   - Contains 5 distinct filter inputs: Customer dropdown, Product dropdown, Exception Type dropdown, Exceptions Only switch, and a Search input.
   - The search input triggers debounced `applyFilters()`, calling `GET /api/v1/feed?searchTerm=...`.
   - The search input does not have an in-input clear button (`x`).
   - The active search query is not synchronized with browser URL query parameters (`?q=...`).
2. **Backend Query Service ([`FeedQueryService.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Post/Services/FeedQueryService.cs))**:
   - Queries `[post].[FeedItems]` directly using Dapper.
   - Search filtering uses SQL `LIKE @SearchPattern` against:
     - `[Title]`
     - `[ContentExcerpt]`
     - `[CustomerName]`
     - `[ProductName]`
     - `[ReferenceDisplay]`
   - It does NOT search `[AuthorName]` (User Name).
   - It does NOT search comment text beyond `[LatestCommentExcerpt]`, and `[LatestCommentExcerpt]` is not included in the current `LIKE` filter.
   - It does NOT search commenter names.
   - It does NOT search full request descriptions if they exceed the 500-character excerpt.
3. **Feed Read-Model Table ([`0009_feed_items_table.sql`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Migrations/Scripts/0009_feed_items_table.sql))**:
   - Table `[post].[FeedItems]` stores materialized attributes (`AuthorName`, `Title`, `ContentExcerpt`, `Summary`, `CustomerName`, `ProductName`, `LatestCommentExcerpt`).
   - There is no consolidated search column or SQL Server Full-Text Catalog/Index on the table.
4. **Feed Projection Handler ([`FeedProjectionHandler.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Post/Services/FeedProjectionHandler.cs))**:
   - On `PostCreated`: populates `AuthorName`, `CustomerName`, `ProductName`, `Title`, `ContentExcerpt`.
   - On `CommentAdded`: updates `CommentCount` and `LatestCommentExcerpt` (truncated to 300 characters), but does not preserve earlier comment text or store commenter names in `FeedItems`.
   - On `RequestEscalated`, `RequestRejected`, `RequestStalled`: updates exception flags.
5. **Feed Projection Rebuilder ([`FeedProjectionRebuilder.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Post/Services/FeedProjectionRebuilder.cs))**:
   - Reconstructs `[post].[FeedItems]` from `post.Posts`, `post.Comments`, `post.Reactions`, and related master data.
   - Does not build or persist a consolidated search document.

## Existing Constraints

1. **Materialized Read Model Architecture (§6, §12, §19.3, §20)**:
   - Target query latency is sub-50ms single-table execution against `[post].[FeedItems]`.
   - Zero cross-schema foreign keys or query-time JOINs across `[request]`, `[customer]`, `[product]`, and `[post]` schemas during feed queries.
2. **Column Size Limit**:
   - `SearchContent` is constrained to `NVARCHAR(4000)` (per user decision) to prevent LOB storage overhead while comfortably indexing hundreds of tokens.
3. **SQL Server Environment Heterogeneity**:
   - SQL Server Full-Text Search is an optional component. Lightweight development environments, local containers, or LocalDB instances may not have FTS installed (`SERVERPROPERTY('IsFullTextInstalled') = 0`).
   - DbUp migration scripts must not fail on environments lacking the FTS service.
4. **FTS Syntax Sanitization**:
   - SQL Server `CONTAINS` queries throw syntax exceptions if unescaped punctuation, parentheses, quotes, or boolean operators (`AND`, `OR`, `NEAR`, `NOT`) are submitted directly by users.

---

# 3. Gap Analysis

Identify gaps between the requested change and the current system.

| ID | Severity | Gap |
|------|------|------|
| GAP-001 | CRITICAL | Searchable data across the 5 target dimensions (Product Name, Customer Name, User Names, Request Text, Comment Text) is not consolidated in a single denormalized column on `[post].[FeedItems]`. |
| GAP-002 | CRITICAL | `[post].[FeedItems]` lacks a SQL Server Full-Text Index and Catalog, preventing performant tokenized search. |
| GAP-003 | MAJOR | `FeedProjectionHandler` and `FeedProjectionRebuilder` do not project commenter names, all comment texts, or full request details into a consolidated search column. |
| GAP-004 | MAJOR | `FeedView.vue` places search inside a sidebar card with discrete dropdowns instead of a unified universal search textbox on top of the operational feed stream. |
| GAP-005 | MINOR | `FeedQueryService` does not parse multi-word queries into prefix-AND full-text syntax (`"term1*" AND "term2*"`), nor does it provide a fallback when SQL Server FTS is not installed. |
| GAP-006 | MINOR | `FeedView.vue` does not synchronize search query parameters with the browser URL (`?q=...`) and lacks an inline clear button (`x`) inside the search input. |

---

# 4. Open Questions

All open questions have been evaluated and resolved through the `/grill-me` alignment interview:

| ID | Question | Impact | Resolution |
|------|------|------|------|
| OQ-001 | How should the universal search textbox interact with the existing feed filters? | Determines whether sidebar filters remain or are removed. | **Resolved**: Replace all sidebar filters entirely with the top universal search bar (search becomes the sole filtering mechanism). |
| OQ-002 | How should searchable data be indexed in SQL Server? | Determines table schema, indexing strategy, and query performance. | **Resolved**: Consolidate text into an aggregated `SearchContent NVARCHAR(4000)` column on `[post].[FeedItems]` indexed with SQL Server Full-Text Search. Zero query-time JOINs. |
| OQ-003 | How should multi-word queries match against `SearchContent`? | Defines search tokenization, query parsing, and search-as-you-type semantics. | **Resolved**: Prefix-AND matching (`"word1*" AND "word2*"`) with input sanitization. |
| OQ-004 | When a comment is added, how should `FeedProjectionHandler` update `SearchContent`? | Determines update strategy and prevents stale/unbounded text accumulation. | **Resolved**: Re-aggregate all active comments for the post and reassemble `SearchContent` (capped at 4,000 chars) with deduplicated author names. |
| OQ-005 | How should environments without SQL Server Full-Text Search installed be handled? | Ensures portability and testability in local dev and CI. | **Resolved**: Dual-mode graceful fallback: conditionally create FTS index if installed; fall back to parameterized `LIKE` on `SearchContent` if unavailable. |
| OQ-006 | Where on top of the feed should the search bar be placed, and should URL sync be supported? | Defines frontend layout and URL routing behavior. | **Resolved**: Full-width dedicated search bar directly below the header, above stream cards. Sync with browser URL (`?q=...`). Keep summary metrics in sidebar. |

---

# 5. Assumptions

Document assumptions made during the assessment.

| ID | Assumption |
|------|------|
| ASM-001 | Production SQL Server instances have the Full-Text Search service installed and enabled (`SERVERPROPERTY('IsFullTextInstalled') = 1`). |
| ASM-002 | Re-aggregating comments for a single post on `CommentAdded` executes within the MediatR notification handler with sub-10ms execution time. |
| ASM-003 | A 4,000-character budget for `SearchContent` provides ample capacity for operational posts, product names, customer names, author names, and typical comment threads without requiring LOB storage (`NVARCHAR(MAX)`). |
| ASM-004 | The existing `FeedController.cs` query parameter `searchTerm` (and aliases `search`, `q`) can be preserved, maintaining full backwards compatibility for external API callers. |

---

# 6. Risks

Document identified risks, impacts, and mitigations.

| ID | Risk | Impact | Mitigation |
|------|------|------|------|
| RISK-001 | Malformed user input (e.g. quotes, dashes, boolean words) causing SQL Server FTS syntax errors in `CONTAINS`. | High | Implement a robust search query sanitizer in `FeedQueryService` that strips punctuation, isolates alphanumeric word tokens, and formats safe prefix clauses (`"word*"`). |
| RISK-002 | Migration failure in environments without SQL Server Full-Text Search installed (e.g. CI runners, LocalDB). | High | Guard catalog and index creation in DbUp migration script with `IF SERVERPROPERTY('IsFullTextInstalled') = 1`. Implement automatic query-time fallback to `LIKE` on `SearchContent`. |
| RISK-003 | Comment volume exceeding 4,000 characters leading to truncation of older comment text. | Low | Order contributing content so critical identifiers (Product, Customer, Author, Title, Request Text) appear first, followed by comments in reverse-chronological order up to the 4,000-char boundary. |
| RISK-004 | Search latency spikes when users type rapidly. | Moderate | Maintain 300ms debounce on input and leverage SQL Server Full-Text Index on single table (`FeedItems`) to ensure sub-50ms execution. |

---

# 7. Recommendations

Recommend possible approaches for addressing major gaps.

## Option A (Recommended)

1. **Database Schema & Read-Model Projection**:
   - Add `[SearchContent] NVARCHAR(4000) NULL` to `[post].[FeedItems]` via migration script `0013_feed_search_content_fts.sql`.
   - Populate `[SearchContent]` for existing rows during migration.
   - Conditionally create Full-Text Catalog `[post_ft_catalog]` and Full-Text Index on `[post].[FeedItems](SearchContent)` if `SERVERPROPERTY('IsFullTextInstalled') = 1`.
2. **Projection Handlers**:
   - Update `FeedProjectionHandler.Handle(PostCreated)` to assemble `SearchContent` combining Product Name, Customer Name, Author Name, Request Title/Description, and Post Title/Content.
   - Update `FeedProjectionHandler.Handle(CommentAdded)` to fetch active comments for the post and re-aggregate `SearchContent` with deduplicated commenter names and comment content (capped at 4,000 chars).
   - Update `FeedProjectionRebuilder` to populate `SearchContent` during administrative rebuilds.
3. **Backend Query Service**:
   - Update `FeedQueryService` to parse `searchTerm` into prefix-AND tokens (`"term1*" AND "term2*"`).
   - If FTS is active, execute `WHERE CONTAINS([SearchContent], @FtsQuery)`; otherwise fall back to parameterized `LIKE @SearchPattern` against `[SearchContent]`.
4. **Frontend Screen (`FeedView.vue`)**:
   - Remove the right-sidebar "Feed Filters" card.
   - Add a full-width universal search textbox directly below the screen header, above the timeline cards.
   - Add search icon, clear button (`x`), and 300ms debounced search-as-you-type.
   - Synchronize query string with browser URL (`?q=...`) using Vue Router.
   - Keep the right sidebar for "Operational Stream Summary" metrics.

### Advantages

- Meets all business and user requirements with a clean, unified search experience.
- Strictly adheres to Cakra's single-table sub-50ms read-model architecture.
- Portable across all environments with graceful FTS fallback.
- Avoids LOB storage overhead by using `NVARCHAR(4000)`.

### Disadvantages

- Requires database migration and projection handler updates to maintain `SearchContent`.

## Option B

Query-time JOINs across `post.Posts`, `post.Comments`, and `request.Requests` with multi-table `CONTAINS` or `LIKE` subqueries.

### Advantages

- Does not require a new column on `[post].[FeedItems]`.

### Disadvantages

- Violates Architecture §6, §12, §20 (single-table indexed queries with zero cross-schema JOINs).
- Substantially degrades query performance and breaks sub-50ms latency SLA on high feed volumes.
- Explicitly rejected in the `/grill-me` alignment interview.

---

# 8. Gap Closure

Record resolutions for gaps and open questions based on the `/grill-me` alignment interview.

## GAP-001 & GAP-002: Consolidated `SearchContent` Column & SQL Server Full-Text Index

### Decision
Add a consolidated `[SearchContent] NVARCHAR(4000) NULL` column on `[post].[FeedItems]` populated with Product Name, Customer Name, User Names, Request Text, and Comment Text. Create a SQL Server Full-Text Catalog and Full-Text Index on `[SearchContent]` with `PK_FeedItems` as key index, conditional on `SERVERPROPERTY('IsFullTextInstalled') = 1`.

### Rationale
Enables universal search across all requested operational dimensions in a single fast, indexed query without cross-schema JOINs.

### Impact
Adds a read-model column to `[post].[FeedItems]` and ensures sub-50ms query latency.

### Architecture Impact
DbUp migration script `0013_feed_search_content_fts.sql`. Target architecture update required.

### Resolved By
User (/grill-me interview alignment)

### Resolved Date
2026-10-05

---

## GAP-003: Projection Handler & Rebuilder Maintenance

### Decision
Update `FeedProjectionHandler` and `FeedProjectionRebuilder` to maintain `SearchContent`:
- `PostCreated`: Enriches with Product, Customer, Author, Post Title/Content, and Request Text.
- `CommentAdded`: Queries active comments for the post and re-aggregates `SearchContent` (capped at 4,000 chars) with deduplicated commenter names.
- `FeedProjectionRebuilder`: Regenerates `SearchContent` for all posts during read-model rebuilds.

### Rationale
Maintains `SearchContent` as a fresh denormalized projection synchronously within domain command scopes.

### Impact
Comment authors and texts become searchable immediately upon posting.

### Architecture Impact
Update `FeedProjectionHandler.cs` and `FeedProjectionRebuilder.cs` in `Cakra.Modules.Post`.

### Resolved By
User (/grill-me interview alignment)

### Resolved Date
2026-10-05

---

## GAP-004 & GAP-006: Top Universal Search UI & URL Synchronization

### Decision
In `FeedView.vue`:
- Remove the right-sidebar "Feed Filters" card.
- Insert a full-width dedicated search bar directly below the screen header, above the stream cards.
- Equip with search icon, clear button (`x`), and 300ms debounced search-as-you-type.
- Synchronize search queries with browser URL query parameter `?q=...`.
- Keep the Operational Stream Summary in the right sidebar.

### Rationale
Simplifies user experience by providing a single intuitive search entrypoint, matches modern web feed conventions, and enables shareable search URLs.

### Impact
Streamlines `SCR-FEED-001` UI and eliminates fragmented filter controls.

### Architecture Impact
Update `FeedView.vue` template and script logic.

### Resolved By
User (/grill-me interview alignment)

### Resolved Date
2026-10-05

---

## GAP-005: Prefix-AND Matching & Dual-Mode FTS Fallback

### Decision
In `FeedQueryService`:
- Parse and sanitize multi-word queries into prefix-AND full-text syntax: `"term1*" AND "term2*"`.
- If SQL Server Full-Text Search is active, query using `CONTAINS([SearchContent], @FtsQuery)`.
- If SQL Server Full-Text Search is not installed or enabled in the environment, fall back gracefully to parameterized `LIKE @SearchPattern` against `[SearchContent]`.

### Rationale
Allows instant matching of partial words as users type, prevents FTS syntax crashes, and guarantees seamless execution across local development and CI testing environments.

### Impact
High search relevance, zero syntax errors, and flawless test environment execution.

### Architecture Impact
Update `FeedQueryService.cs` query compilation and parameter binding.

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

This change introduces database read-model modifications (new `[SearchContent] NVARCHAR(4000)` column, Full-Text Catalog and Full-Text Index on `[post].[FeedItems]`), updates domain projection handler behaviors (`FeedProjectionHandler` comment re-aggregation, `FeedProjectionRebuilder`), implements dual-mode query execution in `FeedQueryService`, and fundamentally refactors the search and filter interaction architecture in `SCR-FEED-001` (`FeedView.vue`). A formal target architecture update and structured implementation plan are required before implementation begins.

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

All functional gaps, design decisions, database indexing strategies, and UX behaviors have been resolved and agreed upon. In accordance with Knowledge-Centric SDLC rules, the Architect evaluates and grants the gate to `READY-FOR-PLANNING` based on closed gaps and complete alignment.

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-014-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-014-ISSUE.md)
- FEATURE: [FEAT-AWR-001-observe-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-AWR-001-observe-operational-feed.md)
- FEATURE: [FEAT-FCOL-005-filter-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-FCOL-005-filter-operational-feed.md)
- UI Layout Spec: [10-scr-feed-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/10-scr-feed-001.md)
- System Architecture: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

Referenced codebase locations:

- Operational Feed Screen: [FeedView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue)
- Feed Timeline Card: [FeedTimelineCard.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/FeedTimelineCard.vue)
- Feed Controller: [FeedController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/FeedController.cs)
- Feed Query Service: [FeedQueryService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Post/Services/FeedQueryService.cs)
- Feed Projection Handler: [FeedProjectionHandler.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Post/Services/FeedProjectionHandler.cs)
- Feed Projection Rebuilder: [FeedProjectionRebuilder.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Post/Services/FeedProjectionRebuilder.cs)
- Feed Items Table Migration: [0009_feed_items_table.sql](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Migrations/Scripts/0009_feed_items_table.sql)
- Integration Tests: [FeedQueryIntegrationTests.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/tests/backend/Cakra.Tests.Integration/Post/FeedQueryIntegrationTests.cs)
