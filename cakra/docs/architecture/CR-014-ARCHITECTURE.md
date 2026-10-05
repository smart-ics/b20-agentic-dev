---
Title: Architecture Specification for Universal Search Textbox for Operational Feed
Code: CR-014
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-05
---

# 1. Overview

This architecture artifact defines the technical realization for replacing the fragmented multi-field filter card with a single universal search textbox prominently located on top of the Operational Feed screen ([`FeedView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue) / `SCR-FEED-001`), as requested in [CR-014-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-014-ISSUE.md) and assessed in [CR-014-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-014-FEASIBILITY-ASSESSMENT.md).

The target architecture introduces:

1. **Denormalized Materialized Read-Model Search Column**:
   Adds `[SearchContent] NVARCHAR(4000) NULL` to `[post].[FeedItems]`, consolidating searchable tokens across 5 operational dimensions:
   - Product Name
   - Customer Name
   - Relevant User Name(s) (post author and comment authors)
   - Request Text (request title and description)
   - Comment Text (all active comments)
2. **SQL Server Full-Text Search (FTS)**:
   Creates Full-Text Catalog `[post_ft_catalog]` and Full-Text Index on `[post].[FeedItems](SearchContent)` keyed on `[PK_FeedItems]`, ensuring single-table indexed queries with zero query-time cross-schema JOINs and sub-50ms query latency.
3. **Prefix-AND Query Compilation & Input Sanitization**:
   In `FeedQueryService`, user search terms are sanitized (punctuation and boolean syntax stripped) and compiled into prefix-AND full-text syntax (e.g. `"john*" AND "laptop*"`), enabling responsive search-as-you-type without syntax errors.
4. **Dual-Mode Graceful Fallback**:
   Executes `CONTAINS([SearchContent], @FtsQuery)` when SQL Server FTS is installed (`SERVERPROPERTY('IsFullTextInstalled') = 1`), with automatic runtime fallback to parameterized `LIKE` on `[SearchContent]` if FTS is unavailable (e.g. lightweight local dev or CI test environments).
5. **Projection Lifecycle Maintenance**:
   - `FeedProjectionHandler.Handle(PostCreated)`: Assembles and persists initial `SearchContent` combining Product, Customer, Author, Post Title/Content, and Request Text.
   - `FeedProjectionHandler.Handle(CommentAdded)`: Queries active comments for the post and re-aggregates `SearchContent` (capped at 4,000 chars) with deduplicated author names.
   - `FeedProjectionRebuilder`: Regenerates `SearchContent` across all feed items during administrative rebuilds.
6. **Frontend Universal Search Architecture (`FeedView.vue`)**:
   - Replaces the legacy right-sidebar "Feed Filters" card entirely with a full-width dedicated search bar directly below the feed header and above the stream cards.
   - Equips the search input with search icon, clear button (`x`), and 300ms debounced search-as-you-type.
   - Synchronizes active search terms with the browser URL query parameter (`?q=...`).
   - Retains the right sidebar for "Operational Stream Summary" metrics.
   - Displays empty state with a "Clear Search" action when a query returns no matching items.
7. **Database Migration Script**:
   DbUp script `0013_feed_search_content_fts.sql` adds column, backfills existing records, and idempotently creates the FTS catalog and index.

---

# 2. Architectural Basis

## Business Context

- ISSUE: [CR-014-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-014-ISSUE.md)
- FEATURE: [FEAT-AWR-001-observe-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-AWR-001-observe-operational-feed.md)
- FEATURE: [FEAT-FCOL-005-filter-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-FCOL-005-filter-operational-feed.md)
- UI Layout Spec: [10-scr-feed-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/10-scr-feed-001.md)
- System Architecture: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

## Analysis Input

- FEASIBILITY-ASSESSMENT: [CR-014-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-014-FEASIBILITY-ASSESSMENT.md)
  - Approved Decisions: `GAP-001` through `GAP-006`, `OQ-001` through `OQ-006`.
  - Architecture Applicability: `ARCHITECTURE-REQUIRED`.
  - Planning Readiness Gate: `READY-FOR-PLANNING` (granted).

```text
CR-014-ISSUE + CR-014-FEASIBILITY-ASSESSMENT
                     ↓
           CR-014-ARCHITECTURE
```

---

# 3. Scope

## Included

1. **Database Schema & Read-Model Projection**:
   - New migration script: `0013_feed_search_content_fts.sql`.
   - Add column `[SearchContent] NVARCHAR(4000) NULL` to `[post].[FeedItems]`.
   - Backfill `SearchContent` for existing feed records.
   - Conditionally create Full-Text Catalog `[post_ft_catalog]` and Full-Text Index on `[post].[FeedItems](SearchContent)` if `SERVERPROPERTY('IsFullTextInstalled') = 1`.
2. **Post Module Projection Services**:
   - `FeedProjectionHandler.cs`:
     - Update `Handle(PostCreated)` to assemble initial `SearchContent`.
     - Update `Handle(CommentAdded)` to fetch active comments and re-aggregate `SearchContent` up to 4,000 chars.
   - `FeedProjectionRebuilder.cs`:
     - Update rebuilder logic to generate `SearchContent` for all posts.
   - `FeedItemDto.cs`:
     - Optional mapping support for `SearchContent` (if needed for debugging).
3. **Backend Query Service (`FeedQueryService.cs`)**:
   - Query parser and sanitizer transforming raw search strings into safe Prefix-AND tokens (`"term1*" AND "term2*"`).
   - Dual-mode execution: `CONTAINS([SearchContent], @FtsQuery)` when FTS is active; parameterized `LIKE` on `[SearchContent]` fallback when FTS is unavailable.
   - Single-table indexed query execution against `[post].[FeedItems]` preserved.
4. **Frontend Screen (`FeedView.vue`)**:
   - Removal of the legacy right-sidebar "Feed Filters" card (`data-testid="feed-filter-bar"`).
   - Addition of full-width dedicated search bar directly below `.op-screen-header` and above the feed stream list.
   - Search-as-you-type with 300ms debouncing, in-input clear button (`x`), and leading search icon.
   - URL query parameter synchronization (`?q=...`) with Vue Router.
   - Retention of right-sidebar "Operational Stream Summary" card.
   - Empty state message detailing active search query with a "Clear Search" button.
5. **Testing**:
   - Integration tests in `FeedQueryIntegrationTests.cs` verifying universal search across Product Name, Customer Name, User Name, Request Text, and Comment Text, multi-word matching, and prefix matching.

## Excluded

1. Domain entity modifications in `Post`, `Request`, `Customer`, `Product`, or `Organization` (this is strictly a read-model projection and query optimization).
2. Advanced syntax parsing (e.g. `product:xxx`, boolean expressions, regex).
3. Query-time JOINs across domain tables.

---

# 4. Technical Decisions

## TD-001: Consolidated `SearchContent` Read-Model Column Schema & Formatting

A denormalized column `[SearchContent] NVARCHAR(4000) NULL` is added to `[post].[FeedItems]`. It is not authoritative business data, but an optimized text index projection.

### Token Construction Format

Contributing attributes are concatenated in priority order:
1. Product Name
2. Customer Name
3. Post Author Name
4. Post Title
5. Post Content / Request Description
6. Reference Display
7. Commenter Names & Comment Texts (from active comments)

```csharp
public static string BuildSearchContent(
    string? productName,
    string? customerName,
    string? authorName,
    string? title,
    string? content,
    string? referenceDisplay,
    IEnumerable<(string? AuthorName, string? Content)>? comments = null)
{
    var sb = new StringBuilder(1024);

    AppendToken(sb, productName);
    AppendToken(sb, customerName);
    AppendToken(sb, authorName);
    AppendToken(sb, title);
    AppendToken(sb, content);
    AppendToken(sb, referenceDisplay);

    if (comments != null)
    {
        foreach (var (commentAuthor, commentText) in comments)
        {
            if (sb.Length >= 3900) break;
            AppendToken(sb, commentAuthor);
            AppendToken(sb, commentText);
        }
    }

    return sb.ToString().Trim();
}

private static void AppendToken(StringBuilder sb, string? token)
{
    if (string.IsNullOrWhiteSpace(token)) return;
    if (sb.Length > 0) sb.Append(' ');
    var remaining = 4000 - sb.Length;
    if (remaining <= 0) return;
    var trimmed = token.Trim();
    if (trimmed.Length > remaining)
    {
        trimmed = trimmed[..remaining];
    }
    sb.Append(trimmed);
}
```

The 4,000-character budget avoids LOB storage overhead, keeping row storage compact and allowing fast in-memory indexing.

---

## TD-002: SQL Server Full-Text Catalog & Index with Conditional DbUp Migration

Migration script `0013_feed_search_content_fts.sql`:

```sql
-- =============================================================================
-- CAKRA — 0013_feed_search_content_fts.sql
-- Slice: P6-S31 Universal Search — SearchContent Column & Full-Text Search
-- =============================================================================

-- 1. Add SearchContent column if not present
IF COL_LENGTH(N'[post].[FeedItems]', N'SearchContent') IS NULL
BEGIN
    ALTER TABLE [post].[FeedItems]
    ADD [SearchContent] NVARCHAR(4000) NULL;
END;

-- 2. Backfill SearchContent for existing records
UPDATE [post].[FeedItems]
SET [SearchContent] = LEFT(
    TRIM(CONCAT_WS(N' ',
        COALESCE([ProductName], N''),
        COALESCE([CustomerName], N''),
        COALESCE([AuthorName], N''),
        COALESCE([Title], N''),
        COALESCE([ContentExcerpt], N''),
        COALESCE([ReferenceDisplay], N''),
        COALESCE([LatestCommentExcerpt], N'')
    )), 4000)
WHERE [SearchContent] IS NULL;

-- 3. Conditionally create Full-Text Catalog and Full-Text Index if FTS is installed
IF SERVERPROPERTY('IsFullTextInstalled') = 1
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = N'post_ft_catalog')
    BEGIN
        EXEC (N'CREATE FULLTEXT CATALOG [post_ft_catalog] AS DEFAULT;');
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID(N'[post].[FeedItems]'))
    BEGIN
        EXEC (N'CREATE FULLTEXT INDEX ON [post].[FeedItems] ([SearchContent] LANGUAGE 1033)
               KEY INDEX [PK_FeedItems]
               ON [post_ft_catalog]
               WITH (CHANGE_TRACKING = AUTO);');
    END;
END;
```

---

## TD-003: Prefix-AND Query Construction and Input Sanitization

To ensure search-as-you-type functions smoothly without crashes, user input is sanitized before passing to `CONTAINS`:

1. **Sanitization**: Strips characters that have syntactic meaning in FTS: `"`, `*`, `(`, `)`, `&`, `|`, `!`, `~`, `<`, `>`, `,`, `:`, `;`, and reserved boolean words (`AND`, `OR`, `NOT`, `NEAR`).
2. **Tokenization**: Splits by whitespace into discrete word tokens.
3. **Prefix Formatting**: Formats each token as `"{token}*"` and combines with ` AND `.
   - Example: Input `"Acme Hosp"` -> Compiled FTS query: `"Acme*" AND "Hosp*"`.
   - Single word `"laptop"` -> Compiled FTS query: `"laptop*"`.

```csharp
public static (string? FtsQuery, string? LikePattern) ParseSearchTokens(string? input)
{
    if (string.IsNullOrWhiteSpace(input))
        return (null, null);

    var cleaned = new string(input
        .Select(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c : ' ')
        .ToArray());

    var tokens = cleaned
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(t => !IsFtsReservedWord(t))
        .ToList();

    if (tokens.Count == 0)
        return (null, null);

    var ftsQuery = string.Join(" AND ", tokens.Select(t => $"\"{t}*\""));
    var likePattern = $"%{string.Join("%", tokens)}%";

    return (ftsQuery, likePattern);
}
```

---

## TD-004: Dual-Mode FTS Execution with Graceful Fallback

In `FeedQueryService.cs`, the query runner determines whether SQL Server Full-Text Search is active.

```sql
-- When FTS is active:
SELECT COUNT(1)
FROM [post].[FeedItems]
WHERE [Visibility] = N'VISIBLE'
  AND [Status] = N'ACTIVE'
  AND (@FtsQuery IS NULL OR CONTAINS([SearchContent], @FtsQuery));

SELECT [FeedItemId], [PostId], ...
FROM [post].[FeedItems]
WHERE [Visibility] = N'VISIBLE'
  AND [Status] = N'ACTIVE'
  AND (@FtsQuery IS NULL OR CONTAINS([SearchContent], @FtsQuery))
ORDER BY [CreatedAt] DESC, [FeedItemId] DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
```

**Fallback Logic**:
If the database environment does not have FTS active (detected on startup or via an indexed probe check), the query substitutes:
`AND (@LikePattern IS NULL OR [SearchContent] LIKE @LikePattern ESCAPE N'\')`.
This guarantees that integration tests running on LocalDB or CI without FTS Advanced Services continue to execute reliably.

---

## TD-005: Projection Handler Maintenance (`FeedProjectionHandler` & `FeedProjectionRebuilder`)

### `PostCreated` Handling:
When a post is created:
1. `AuthorName` is resolved from `IOrganizationQueryService` or post metadata.
2. `CustomerName` and `ProductName` are resolved.
3. If linked to a Request, Request Title and Description are captured.
4. `SearchContent` is assembled via `BuildSearchContent` and written into the `INSERT / UPDATE` statement for `[post].[FeedItems]`.

### `CommentAdded` Handling:
When a comment is added to a post:
1. Query active comments for that `PostId` from `post.Comments`:
   ```sql
   SELECT c.[Content], o.[FullName] AS [AuthorName]
   FROM [post].[Comments] c
   LEFT JOIN [organization].[Persons] o ON o.[Id] = c.[AuthorPersonId]
   WHERE c.[PostId] = @PostId AND c.[Status] = N'ACTIVE'
   ORDER BY c.[CreatedAt] ASC;
   ```
2. Re-aggregate `SearchContent` using the post's core attributes + all active comment contents and commenter names.
3. Update `[post].[FeedItems]` atomically:
   ```sql
   UPDATE [post].[FeedItems]
   SET
       [CommentCount] = [CommentCount] + 1,
       [LatestCommentExcerpt] = @LatestCommentExcerpt,
       [SearchContent] = @SearchContent,
       [UpdatedAt] = @OccurredAtUtc,
       [LastActivityAt] = @OccurredAtUtc
   WHERE [PostId] = @PostId;
   ```

### `FeedProjectionRebuilder` Handling:
The rebuilder regenerates `SearchContent` during full database rebuilds by joining posts, references, and active comments.

---

## TD-006: Top Universal Search UI & State Architecture (`FeedView.vue`)

### Layout Hierarchy:
```text
<section data-screen-id="SCR-FEED-001">
  <div class="row g-3">
    <!-- Left Column: Feed Stream (~67% / 8 cols) -->
    <div class="col-12 col-xl-8 col-xxl-8">
      <!-- 1. Header Bar -->
      <div class="op-screen-header">...</div>

      <!-- 2. Universal Search Bar (Dedicated Full-Width Input) -->
      <div class="op-feed-universal-search card shadow-xs border-0 mb-3" data-testid="feed-universal-search">
        <div class="card-body p-2">
          <div class="input-group input-group-sm">
            <span class="input-group-text bg-white border-end-0 text-muted">
              <i class="bi bi-search" aria-hidden="true"></i>
            </span>
            <input
              id="feedUniversalSearch"
              v-model="searchTerm"
              type="text"
              class="form-control form-control-sm border-start-0 border-end-0 ps-0"
              placeholder="Search by product, customer, user, request, or comment text..."
              data-testid="feed-universal-search-input"
              @input="handleSearchInput"
            />
            <button
              v-if="searchTerm.trim().length > 0"
              type="button"
              class="btn btn-outline-secondary border-start-0 border bg-white text-muted"
              data-testid="feed-search-clear-btn"
              @click="clearSearch"
            >
              <i class="bi bi-x-circle-fill" aria-hidden="true"></i>
            </button>
          </div>
        </div>
      </div>

      <!-- 3. Stream Cards Container -->
      <div v-if="feedItems.length > 0" class="d-flex flex-column gap-2">
        <FeedTimelineCard v-for="item in feedItems" ... />
        <!-- Bottom Sentinel & Infinite Scroll Controls -->
      </div>

      <!-- 4. Empty State with Clear Search Action -->
      <div v-else-if="!isLoadingFeed" class="card border-0 shadow-xs my-2">
        <!-- Details query and offers "Clear Search" button -->
      </div>
    </div>

    <!-- Right Column: Sticky Contextual Panel (~33% / 4 cols) -->
    <div class="col-12 col-xl-4 col-xxl-4">
      <div class="op-feed-sticky-panel">
        <!-- Operational Stream Summary Card -->
        <div class="card shadow-xs">
          <!-- Total Stream Events, Exceptions Loaded, Stream Filter Status -->
        </div>
      </div>
    </div>
  </div>
</section>
```

### URL Parameter Sync:
- On mount: checks `route.query.q` and initializes `searchTerm`.
- On change: updates browser URL using `router.replace({ query: { ...route.query, q: term || undefined } })` without triggering page reload.

---

# 5. Component Responsibilities

| Component | Responsibility |
|------------|---------------|
| `0013_feed_search_content_fts.sql` | Adds `[SearchContent] NVARCHAR(4000)` column to `[post].[FeedItems]`, backfills data, and conditionally creates Full-Text Catalog and Index. |
| `FeedProjectionHandler.cs` | Populates `SearchContent` on `PostCreated` and re-aggregates active comments and commenters on `CommentAdded`. |
| `FeedProjectionRebuilder.cs` | Regenerates `SearchContent` across all posts during read-model rebuilds. |
| `FeedQueryService.cs` | Parses and sanitizes search tokens, compiles prefix-AND full-text queries, and executes dual-mode queries against `[post].[FeedItems]`. |
| `FeedController.cs` | Passes `searchTerm` / `q` query parameters to `GetFeedQuery` (backwards-compatible API). |
| `FeedView.vue` | Renders top universal search bar, manages debounced typing, synchronizes URL query params, and renders feed timeline cards. |
| `FeedQueryIntegrationTests.cs` | Verifies search correctness across product, customer, user, request, and comment text dimensions. |

---

# 6. Integration Design

| Source | Target | Purpose |
|----------|----------|----------|
| `FeedView.vue` | `GET /api/v1/feed?searchTerm=...` | Transmits universal search query on debounced input or clear. |
| `FeedController.cs` | `FeedQueryService` (via `IMediator`) | Forwards `GetFeedQuery` with resolved `SearchTerm`. |
| `FeedQueryService` | `[post].[FeedItems]` (SQL Server) | Executes single-table indexed FTS / `LIKE` query. |
| `PostCreated` event | `FeedProjectionHandler` | Enriches post with customer, product, and request data into `SearchContent`. |
| `CommentAdded` event | `FeedProjectionHandler` | Queries active comments for the post and updates `SearchContent`. |
| `FeedProjectionRebuilder` | `[post].[FeedItems]` | Batch rebuilds `SearchContent` during administrative maintenance. |

---

# 7. Data Ownership

| Data | Owner | Description |
|--------|--------|-------------|
| `[post].[FeedItems].[SearchContent]` | Post Module (`post` schema) | Denormalized read-model projection consolidating multi-domain searchable tokens. |
| `[post].[Posts]` | Post Module | Authoritative post master data. |
| `[post].[Comments]` | Post Module | Authoritative comment master data. |
| `[request].[Requests]` | Request Module | Authoritative request master data. |
| `[customer].[Customers]` | Customer Module | Authoritative customer master data. |
| `[product].[Products]` | Product Module | Authoritative product master data. |

---

# 8. Database Design

## Modified Tables

| Table | Change | Purpose |
|---------|---------|---------|
| `[post].[FeedItems]` | Add column `[SearchContent] NVARCHAR(4000) NULL` | Stores consolidated searchable tokens for universal search. |

## Full-Text Catalogs & Indexes

| Object | Type | Details |
|--------|------|---------|
| `[post_ft_catalog]` | Full-Text Catalog | Default full-text catalog for Post module. |
| `IX_FeedItems_SearchContent_FTS` | Full-Text Index | Index on `[post].[FeedItems](SearchContent)` keyed on `[PK_FeedItems]` with `CHANGE_TRACKING = AUTO`. |

## Migration Considerations

1. **Idempotence**: `COL_LENGTH` guards column creation; `sys.fulltext_catalogs` and `sys.fulltext_indexes` guard catalog and index creation.
2. **FTS Availability**: `IF SERVERPROPERTY('IsFullTextInstalled') = 1` prevents migration failures on environments without the SQL Server Full-Text Search service.
3. **Data Backfill**: Existing rows are populated with combined Product, Customer, Author, Title, and Excerpt text during migration.

---

# 9. Cross-Cutting Concerns

1. **Performance**: Target latency remains sub-50ms. Full-Text Index and single-table execution against `[post].[FeedItems]` ensure zero cross-schema JOIN overhead.
2. **Security & Input Sanitization**: Stripping FTS reserved words and punctuation prevents SQL Server FTS syntax errors and query injection vulnerabilities.
3. **Reliability & Portability**: Dual-mode execution ensures tests run cleanly on LocalDB and CI without requiring specialized SQL Server instances.
4. **Usability**: 300ms debouncing balances responsiveness with network efficiency; URL sync preserves user context on navigation and refresh.

---

# 10. Implementation Constraints

1. **Dapper Execution**: Must use Dapper single-table queries against `[post].[FeedItems]`.
2. **Zero Cross-Schema JOINs**: No query-time JOINs across `[request]`, `[customer]`, `[product]`, or `[organization]` in `FeedQueryService`.
3. **Column Constraint**: `SearchContent` must be `NVARCHAR(4000)`, not `NVARCHAR(MAX)`.
4. **API Compatibility**: Preserve `searchTerm`, `search`, and `q` query parameters in `FeedController.cs`.
5. **No Broken Tests**: Existing pagination and exception filter tests in `FeedQueryIntegrationTests.cs` must continue to pass.

---

# 11. Acceptance Conditions

1. `0013_feed_search_content_fts.sql` applies idempotently and creates `[SearchContent]` column and FTS catalog/index when FTS is installed.
2. `FeedProjectionHandler` populates `SearchContent` on `PostCreated` and re-aggregates active comments and commenters on `CommentAdded`.
3. `FeedQueryService` compiles prefix-AND tokens (`"word1*" AND "word2*"`) and executes `CONTAINS` or `LIKE` fallback.
4. Searching by Product Name returns matching feed items.
5. Searching by Customer Name returns matching feed items.
6. Searching by User Name (post author or commenter) returns matching feed items.
7. Searching by Request Text (title or description) returns matching feed items.
8. Searching by Comment Text returns matching feed items.
9. `FeedView.vue` renders the universal search bar on top of the feed stream with 300ms debouncing and an inline clear button.
10. The legacy right-sidebar filter card is removed, and the right sidebar displays the summary metrics card.
11. Search query is synchronized with the browser URL (`?q=...`).
12. All integration tests pass cleanly.
