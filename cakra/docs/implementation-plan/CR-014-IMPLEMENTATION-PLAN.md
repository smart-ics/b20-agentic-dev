---
Title: Universal Search Textbox for Operational Feed Implementation Plan
Code: CR-014
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-05
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement a universal search textbox prominently located on top of the Operational Feed stream ([`FeedView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue) / `SCR-FEED-001`), replacing the legacy multi-field sidebar filter card with a unified single-input search across five operational dimensions: Product Name, Customer Name, User Name (authors and commenters), Request Text (title and description), and Comment Text (across all active comments). The architecture introduces a denormalized read-model projection column `[SearchContent] NVARCHAR(4000) NULL` on `[post].[FeedItems]` indexed with SQL Server Full-Text Search, prefix-AND query compilation, dual-mode fallback to parameterized `LIKE` for environments without FTS, 300ms debouncing, in-input clear button, browser URL query parameter sync (`?q=...`), and automated projection updates in accordance with [CR-014-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-014-ARCHITECTURE.md), [CR-014-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-014-FEASIBILITY-ASSESSMENT.md), and [CR-014-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-014-ISSUE.md).

**Planning Mode:** FEATURE-PLANNING

**Referenced artifacts:**
- ISSUE: [CR-014-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-014-ISSUE.md)
- FEASIBILITY-ASSESSMENT: [CR-014-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-014-FEASIBILITY-ASSESSMENT.md) (Planning Readiness: `READY-FOR-PLANNING`)
- FEATURE: [FEAT-AWR-001-observe-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-AWR-001-observe-operational-feed.md)
- FEATURE: [FEAT-FCOL-005-filter-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-FCOL-005-filter-operational-feed.md)
- ARCHITECTURE: [CR-014-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-014-ARCHITECTURE.md)
- UI Layout Spec: [10-scr-feed-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/10-scr-feed-001.md)

**Architecture Applicability:** ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers:
1. **Database Schema & Full-Text Search Migration (`Cakra.Api`)**:
   - Create migration script `0013_feed_search_content_fts.sql` adding column `[SearchContent] NVARCHAR(4000) NULL` to `[post].[FeedItems]`.
   - Backfill `SearchContent` for existing records from Product, Customer, Author, Title, Excerpt, and LatestComment data.
   - Conditionally create Full-Text Catalog `[post_ft_catalog]` and Full-Text Index on `[post].[FeedItems](SearchContent)` if `SERVERPROPERTY('IsFullTextInstalled') = 1`.
2. **Post Module Projection & Rebuilder Updates (`Cakra.Modules.Post`)**:
   - Update `FeedProjectionHandler.cs` to populate `SearchContent` on `PostCreated` and re-aggregate active comments and commenters on `CommentAdded` (capped at 4,000 characters).
   - Update `FeedProjectionRebuilder.cs` to generate `SearchContent` during administrative rebuilds.
3. **Query Engine & Fallback (`Cakra.Modules.Post`)**:
   - Update `FeedQueryService.cs` to parse search input, sanitize punctuation and boolean keywords, and compile safe Prefix-AND tokens (`"term1*" AND "term2*"`).
   - Execute dual-mode queries: `CONTAINS([SearchContent], @FtsQuery)` when FTS is active; fallback to parameterized `LIKE` on `[SearchContent]` when FTS is unavailable.
   - Preserve single-table execution against `[post].[FeedItems]` with zero query-time cross-schema JOINs.
4. **Frontend Universal Search UI (`Cakra.Web`)**:
   - Remove the legacy right-sidebar "Feed Filters" card (`data-testid="feed-filter-bar"`).
   - Add a full-width universal search textbox directly below `.op-screen-header` and above the feed stream list.
   - Equip with search icon, clear button (`x`), 300ms debounced search-as-you-type, and URL query parameter synchronization (`?q=...`).
   - Retain the right sidebar for "Operational Stream Summary" metrics.
   - Display empty state with active query display and a "Clear Search" reset button.
5. **Integration & Regression Testing (`Cakra.Tests.Integration`)**:
   - Add integration tests verifying universal search across Product Name, Customer Name, User Name, Request Text, and Comment Text.
   - Verify prefix matching, multi-word matching, and verify that existing pagination and exception filter tests continue to pass.

---

# 3. Dependencies

**External Dependencies:** None.

**Slice Dependencies:** Listed in `Depends On` field for each slice below.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---------|---------|---------|---------|
| P1 - Database Schema & Migration | IMPLEMENTED | GO | 1/1 |
| P2 - Projection Handlers & Query Engine | IMPLEMENTED | GO | 2/2 |
| P3 - Frontend Universal Search UI | IMPLEMENTED | GO | 1/1 |
| P4 - Integration Testing & Verification | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Database Schema & Migration

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P1-S01

**Title:** Create DbUp Migration for Feed SearchContent and Full-Text Search

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Create migration script `0013_feed_search_content_fts.sql` adding column `[SearchContent] NVARCHAR(4000) NULL` to `[post].[FeedItems]`, backfilling existing rows, and conditionally creating Full-Text Catalog `[post_ft_catalog]` and Full-Text Index on `[SearchContent]` if `SERVERPROPERTY('IsFullTextInstalled') = 1`.

**Depends On:** None

**Repository:** Cakra.Api

**Completion Criteria:**
- File `0013_feed_search_content_fts.sql` exists in `cakra/src/backend/Cakra.Api/Migrations/Scripts/`.
- Script checks `IF COL_LENGTH(N'[post].[FeedItems]', N'SearchContent') IS NULL` and adds `[SearchContent] NVARCHAR(4000) NULL`.
- Script backfills existing records with trimmed concatenated tokens: `ProductName`, `CustomerName`, `AuthorName`, `Title`, `ContentExcerpt`, `ReferenceDisplay`, `LatestCommentExcerpt` up to 4000 chars.
- Script checks `IF SERVERPROPERTY('IsFullTextInstalled') = 1` before creating `[post_ft_catalog]` and `FULLTEXT INDEX ON [post].[FeedItems]([SearchContent])`.
- Script is fully idempotent and succeeds on both FTS-enabled and non-FTS SQL Server instances.

**Implementation Notes:**
- Created `cakra/src/backend/Cakra.Api/Migrations/Scripts/0013_feed_search_content_fts.sql`.
- Added idempotent column creation for `[SearchContent] NVARCHAR(4000) NULL` on `[post].[FeedItems]` using `COL_LENGTH`.
- Added backfill statement using `CONCAT_WS` and `LEFT(..., 4000)` across existing feed attributes.
- Added conditional FTS catalog `[post_ft_catalog]` and index creation guarded by `SERVERPROPERTY('IsFullTextInstalled') = 1` and `sys.fulltext_catalogs` / `sys.fulltext_indexes` checks.
- Verified backend build passes with 0 errors.

**Changed Files:**
- `cakra/src/backend/Cakra.Api/Migrations/Scripts/0013_feed_search_content_fts.sql` (created)
- `cakra/docs/implementation-plan/CR-014-IMPLEMENTATION-PLAN.md` (updated status and notes)

---

## P2 - Projection Handlers & Query Engine

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P2-S02

**Title:** Update Projection Handlers and Rebuilder to Populate SearchContent

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Update `FeedProjectionHandler.cs` and `FeedProjectionRebuilder.cs` to build and maintain `SearchContent` on `[post].[FeedItems]`, combining Product Name, Customer Name, User Names, Request Text, and Comment Text, and re-aggregating active comments on `CommentAdded`.

**Depends On:** P1-S01

**Repository:** Cakra.Modules.Post

**Completion Criteria:**
- In `FeedProjectionHandler.cs`:
  - `BuildSearchContent` helper method concatenates Product, Customer, Author, Title, Request Text, and active Comments up to 4,000 characters.
  - `Handle(PostCreated)` assembles initial `SearchContent` and persists it in the `INSERT / UPDATE` statement for `[post].[FeedItems]`.
  - `Handle(CommentAdded)` queries active comments from `post.Comments` (with commenter names from `organization.Persons`), re-aggregates `SearchContent` up to 4,000 characters, and updates `[post].[FeedItems].[SearchContent]`.
- In `FeedProjectionRebuilder.cs`:
  - Rebuild queries assemble `SearchContent` across all posts, including active comments and commenter names, persisting it into `[post].[FeedItems]`.

**Implementation Notes:**
- Added `BuildSearchContent` and `AppendToken` static helpers in `FeedProjectionHandler.cs` enforcing priority-ordered token construction and 4,000-character truncation across Product Name, Customer Name, Post Author, Title, Post Content / Request Description, Reference Display, and active comment contents / commenter names.
- Updated `FeedProjectionHandler.Handle(PostCreated)` and `CreateSystemExceptionPostAndFeedItemAsync` to construct `SearchContent` and persist it on `[post].[FeedItems]` INSERT and UPDATE operations.
- Updated `FeedProjectionHandler.Handle(CommentAdded)` to fetch feed item attributes, query active comments with resolved commenter names from `IOrganizationQueryService`, re-aggregate `SearchContent`, and update `[post].[FeedItems].[SearchContent]`.
- Updated `FeedProjectionRebuilder.RebuildAllAsync` to assemble `SearchContent` with resolved commenter names for each post during read-model rebuilding, persisting it in `[post].[FeedItems]`.
- Added optional `SearchContent` property to `FeedItemDto.cs`.
- Verified all unit tests (`Cakra.Tests.Unit`) and feed projection integration tests (`FeedProjectionIntegrationTests`) pass cleanly.

**Changed Files:**
- `cakra/src/backend/Cakra.Modules.Post/Models/FeedItemDto.cs` (modified)
- `cakra/src/backend/Cakra.Modules.Post/Services/FeedProjectionHandler.cs` (modified)
- `cakra/src/backend/Cakra.Modules.Post/Services/FeedProjectionRebuilder.cs` (modified)
- `cakra/src/backend/Cakra.Api/Migrations/Scripts/0013_feed_search_content_fts.sql` (modified)
- `cakra/docs/implementation-plan/CR-014-IMPLEMENTATION-PLAN.md` (updated status and notes)

---

### P2-S03

**Title:** Implement Prefix-AND FTS Compilation and Dual-Mode Fallback in FeedQueryService

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Update `FeedQueryService.cs` to sanitize search input, compile safe Prefix-AND tokens (`"term1*" AND "term2*"`), and execute dual-mode queries (`CONTAINS` when FTS is active, parameterized `LIKE` on `SearchContent` fallback when FTS is unavailable) against `[post].[FeedItems]`.

**Depends On:** P1-S01

**Repository:** Cakra.Modules.Post

**Completion Criteria:**
- `FeedQueryService.cs` implements an FTS input sanitizer stripping punctuation and boolean reserved keywords.
- Multi-word search terms compile into Prefix-AND syntax (e.g. `"john*" AND "laptop*"`).
- Query detects FTS availability on `[post].[FeedItems]`.
- When FTS is active, queries `[post].[FeedItems]` using `CONTAINS([SearchContent], @FtsQuery)`.
- When FTS is unavailable, executes parameterized `LIKE @SearchPattern` against `[SearchContent]`.
- Retains single-table indexed execution with sub-50ms target latency and zero cross-schema JOINs.

**Implementation Notes:**
- Added `ParseSearchTokens` and `IsFtsReservedWord` helper methods to sanitize punctuation and strip FTS reserved keywords (`AND`, `OR`, `NOT`, `NEAR`, `FORMSOF`, `INFLECTIONAL`, `THESAURUS`, `ISABOUT`, `WEIGHT`), compiling tokens into Prefix-AND full-text syntax (`"term1*" AND "term2*"`) and parameterized LIKE pattern.
- Implemented `CheckFtsAvailabilityAsync` with thread-safe cached detection querying `SERVERPROPERTY('IsFullTextInstalled')` and `sys.fulltext_indexes` on `[post].[FeedItems]`.
- Implemented dual-mode query execution in `GetFeedAsync`: executes `CONTAINS([SearchContent], @FtsQuery)` when FTS is active, and falls back to parameterized `LIKE` against `[SearchContent]` (and individual column fallbacks) when FTS is unavailable.
- Retained single-table indexed execution against `[post].[FeedItems]` with zero cross-schema JOINs.

**Changed Files:**
- `cakra/src/backend/Cakra.Modules.Post/Services/FeedQueryService.cs` (modified)
- `cakra/docs/implementation-plan/CR-014-IMPLEMENTATION-PLAN.md` (updated status and notes)

---

## P3 - Frontend Universal Search UI

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P3-S04

**Title:** Implement Universal Search Textbox in FeedView.vue

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Refactor [FeedView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue) to remove the legacy right-sidebar filter card, insert a full-width universal search textbox on top of the operational feed stream, implement 300ms debouncing, in-input clear button, browser URL query parameter sync (`?q=...`), and custom empty state with reset button.

**Depends On:** P2-S03

**Repository:** Cakra.Web

**Completion Criteria:**
- In `FeedView.vue`:
  - The right-sidebar "Feed Filters" card (`data-testid="feed-filter-bar"`) is removed.
  - A dedicated full-width universal search bar (`data-testid="feed-universal-search"`) is placed directly below `.op-screen-header` and above the feed stream list.
  - Search input has `data-testid="feed-universal-search-input"`, leading search icon, placeholder `"Search by product, customer, user, request, or comment text..."`, and 300ms debounced input handler.
  - Clear button (`data-testid="feed-search-clear-btn"`) appears when input is non-empty and resets the search immediately.
  - Active search term is synchronized with browser URL query parameter (`?q=...`) on change and restored from `route.query.q` on initial mount.
  - The right sidebar retains the "Operational Stream Summary" card displaying Total Stream Events, Exceptions Loaded, and Stream Filter Status.
  - Empty state displays `"No feed items matching '{searchTerm}'"` and offers a "Clear Search" button.

**Implementation Notes:**
- Removed legacy right-sidebar "Feed Filters" card (`data-testid="feed-filter-bar"`).
- Added dedicated full-width universal search bar (`data-testid="feed-universal-search"`) below `.op-screen-header` with search input (`data-testid="feed-universal-search-input"`), leading search icon, placeholder `"Search by product, customer, user, request, or comment text..."`, and 300ms debounced input handler.
- Implemented immediate search clear button (`data-testid="feed-search-clear-btn"`) visible when search input is non-empty.
- Integrated two-way browser URL query parameter sync (`?q=...`) on input/clear via `router.replace` and restored query from `route.query.q` on initial mount and route change.
- Updated empty state with `"No feed items matching '{searchTerm}'"` message and "Clear Search" button (`data-testid="feed-empty-clear-btn"`).
- Retained the "Operational Stream Summary" card in the right sidebar.
- Verified TypeScript compilation and production bundle build with `npm run build`.

**Changed Files:**
- `cakra/src/frontend/Cakra.Web/src/views/FeedView.vue` (modified)
- `cakra/docs/implementation-plan/CR-014-IMPLEMENTATION-PLAN.md` (updated status, notes, changed files)

---

## P4 - Integration Testing & Verification

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P4-S05

**Title:** Verify Universal Search across Dimensions with Integration Tests & Build

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Update [FeedQueryIntegrationTests.cs](file:///d:/Project.Aktif/b20-agentic-dev\cakra\tests\backend\Cakra.Tests.Integration\Post\FeedQueryIntegrationTests.cs) to verify universal search across all 5 dimensions (Product Name, Customer Name, User Name, Request Text, Comment Text), verify multi-word prefix search, ensure existing feed tests pass, and verify frontend compilation.

**Depends On:** P2-S02, P2-S03, P3-S04

**Repository:** Cakra.Tests.Integration

**Completion Criteria:**
- `FeedQueryIntegrationTests.cs` includes integration test cases verifying:
  - Universal search matches by Product Name.
  - Universal search matches by Customer Name.
  - Universal search matches by Post Author Name and Commenter Name.
  - Universal search matches by Request Text (Title and Description).
  - Universal search matches by Comment Text across multiple comments.
  - Universal search matches multi-word queries using Prefix-AND logic.
  - Clearing search restores full feed list.
- All existing tests in `Cakra.Tests.Integration` pass cleanly (`dotnet test`).
- Frontend builds cleanly without TypeScript or asset errors (`npm run build`).

**Implementation Notes:**
- Added comprehensive integration test `GetFeed_with_SearchTerm_matches_across_all_five_dimensions_multi_word_prefix_and_clearing_search_restores_full_feed` in `FeedQueryIntegrationTests.cs`.
- Verified universal search matching across all 5 dimensions (Product Name, Customer Name, Post Author Name, Commenter Name, Request Title/Description, Comment Text across multiple comments).
- Verified multi-word prefix search behavior and negative non-matching queries.
- Verified clearing search (`null`, `""`, whitespace) restores full feed list.
- Enhanced `FeedQueryService.cs` LIKE fallback to support order-independent multi-token search predicates with `DynamicParameters`.
- [Remediation RV-001]: Removed manual `COMMIT TRANSACTION;` and `BEGIN TRANSACTION;` blocks in `0013_feed_search_content_fts.sql` (aligning with ARCHITECTURE TD-002) and adjusted `DatabaseMigrationRunner.cs` to `.WithoutTransaction()`, eliminating `SqlTransaction.ZombieCheck` exceptions during DbUp schema migrations and integration test database provisioning.
- Verified all unit (358) and integration (171) tests pass cleanly with `dotnet test`.
- Verified frontend build (`vue-tsc --noEmit && vite build`) compiles cleanly without errors.

**Changed Files:**
- `cakra/tests/backend/Cakra.Tests.Integration/Post/FeedQueryIntegrationTests.cs` (modified)
- `cakra/src/backend/Cakra.Modules.Post/Services/FeedQueryService.cs` (modified)
- `cakra/src/backend/Cakra.Api/Migrations/Scripts/0013_feed_search_content_fts.sql` (modified)
- `cakra/src/backend/Cakra.Api/Infrastructure/Migrations/DatabaseMigrationRunner.cs` (modified)
- `cakra/docs/implementation-plan/CR-014-IMPLEMENTATION-PLAN.md` (updated status, notes, changed files)

---

# 6. Change Log

- 2026-10-05: Initial creation of CR-014 implementation plan for Universal Search Textbox for Operational Feed. Execution Approval granted by Architect.
