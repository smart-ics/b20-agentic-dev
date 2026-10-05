-- =============================================================================
-- CAKRA — 0014_workpackage_requests_sort_order.sql
-- Slice: P1-S01 Work Package Module — Database Migration Script for Work Package Request SortOrder
--
-- Purpose:
--   1. Adds [SortOrder] INT NOT NULL CONSTRAINT [DF_WorkPackageRequests_SortOrder] DEFAULT 0
--      to [workpackage].[WorkPackageRequests] table.
--   2. Backfills sequential [SortOrder] for existing records partitioned by
--      [WorkPackageId] ordered by [AddedAt] ASC.
--   3. Creates non-clustered index [IX_WorkPackageRequests_WorkPackageId_SortOrder]
--      on ([WorkPackageId], [SortOrder]) INCLUDE ([RequestId], [AddedAt], [RemovedAt]).
--
-- Constraints & Rules (Architecture CR-015 §4 TD-001, §8):
--   - Script is idempotent (guarded by COL_LENGTH and sys.indexes checks).
--   - Uses ROW_NUMBER() OVER (PARTITION BY [WorkPackageId] ORDER BY [AddedAt] ASC) - 1
--     to ensure all existing links receive a zero-based contiguous SortOrder.
-- =============================================================================

-- 1. Add SortOrder column if not present
IF COL_LENGTH(N'[workpackage].[WorkPackageRequests]', N'SortOrder') IS NULL
BEGIN
    ALTER TABLE [workpackage].[WorkPackageRequests]
        ADD [SortOrder] INT NOT NULL CONSTRAINT [DF_WorkPackageRequests_SortOrder] DEFAULT 0;
END;

-- 2. Backfill sequential SortOrder for existing records
EXEC (N'
WITH RankedRequests AS (
    SELECT
        [Id],
        ROW_NUMBER() OVER (PARTITION BY [WorkPackageId] ORDER BY [AddedAt] ASC) - 1 AS [NewSortOrder]
    FROM [workpackage].[WorkPackageRequests]
)
UPDATE wpr
SET wpr.[SortOrder] = rr.[NewSortOrder]
FROM [workpackage].[WorkPackageRequests] wpr
INNER JOIN RankedRequests rr ON wpr.[Id] = rr.[Id];
');

-- 3. Create non-clustered index on (WorkPackageId, SortOrder)
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_WorkPackageRequests_WorkPackageId_SortOrder'
)
BEGIN
    EXEC (N'CREATE NONCLUSTERED INDEX [IX_WorkPackageRequests_WorkPackageId_SortOrder]
        ON [workpackage].[WorkPackageRequests] ([WorkPackageId], [SortOrder])
        INCLUDE ([RequestId], [AddedAt], [RemovedAt]);');
END;
