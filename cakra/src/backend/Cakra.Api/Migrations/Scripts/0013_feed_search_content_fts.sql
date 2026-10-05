-- =============================================================================
-- CAKRA — 0013_feed_search_content_fts.sql
-- Slice: P1-S01 Universal Search — SearchContent Column & Full-Text Search
--
-- Purpose:
--   1. Adds denormalized read model column [SearchContent] NVARCHAR(4000) NULL
--      to [post].[FeedItems] for universal search indexing.
--   2. Backfills existing records with trimmed concatenated tokens across Product,
--      Customer, Author, Title, Excerpt, Reference, and Latest Comment.
--   3. Conditionally creates Full-Text Catalog [post_ft_catalog] and Full-Text Index
--      on [post].[FeedItems]([SearchContent]) if Full-Text Search is installed.
--
-- Constraints & Rules (Architecture CR-014 §4, §8):
--   - Script is idempotent (guarded by COL_LENGTH, sys.fulltext_catalogs, and sys.fulltext_indexes).
--   - SERVERPROPERTY('IsFullTextInstalled') check ensures safe execution in environments without FTS.
-- =============================================================================

-- 1. Add SearchContent column if not present
IF COL_LENGTH(N'[post].[FeedItems]', N'SearchContent') IS NULL
BEGIN
    ALTER TABLE [post].[FeedItems]
    ADD [SearchContent] NVARCHAR(4000) NULL;
END;

-- 2. Backfill SearchContent for existing records
EXEC (N'
UPDATE [post].[FeedItems]
SET [SearchContent] = LEFT(
    TRIM(CONCAT_WS(N'' '',
        COALESCE([ProductName], N''''),
        COALESCE([CustomerName], N''''),
        COALESCE([AuthorName], N''''),
        COALESCE([Title], N''''),
        COALESCE([ContentExcerpt], N''''),
        COALESCE([ReferenceDisplay], N''''),
        COALESCE([LatestCommentExcerpt], N'''')
    )), 4000)
WHERE [SearchContent] IS NULL;
');

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
