-- =============================================================================
-- CAKRA — 0017_add_workpackage_deadline.sql
-- Slice: P1-S01 Database Migration Script for Work Package Deadline
--
-- Purpose:
--   1. Adds optional [Deadline] DATETIME2 NULL column to [workpackage].[WorkPackages] table.
--   2. Adds filtered nonclustered index [IX_WorkPackages_Deadline] on ([Deadline])
--      WHERE [Deadline] IS NOT NULL.
--
-- Constraints & Rules (Architecture CR-023 §4 TD-001):
--   - Script is idempotent (guarded by OBJECT_ID, COL_LENGTH, and sys.indexes checks).
--   - Compatible with existing 0007_workpackage_tables.sql and 0014_workpackage_requests_sort_order.sql.
-- =============================================================================

IF OBJECT_ID(N'[workpackage].[WorkPackages]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[workpackage].[WorkPackages]', N'Deadline') IS NULL
    BEGIN
        ALTER TABLE [workpackage].[WorkPackages]
            ADD [Deadline] DATETIME2 NULL;
    END;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes 
        WHERE name = N'IX_WorkPackages_Deadline' 
          AND object_id = OBJECT_ID(N'[workpackage].[WorkPackages]')
    )
    BEGIN
        EXEC(N'CREATE NONCLUSTERED INDEX [IX_WorkPackages_Deadline] 
            ON [workpackage].[WorkPackages] ([Deadline])
            WHERE [Deadline] IS NOT NULL;');
    END;
END;
