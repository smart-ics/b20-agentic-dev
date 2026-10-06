-- =============================================================================
-- CAKRA — 0016_add_request_deadline.sql
-- Slice: P1-S01 Database Migration Script for Request Deadline
--
-- Purpose:
--   1. Adds optional [Deadline] DATETIME2 NULL column to [request].[Requests] table.
--   2. Adds filtered nonclustered index [IX_Requests_Deadline] on ([Deadline])
--      WHERE [Deadline] IS NOT NULL.
--
-- Constraints & Rules (Architecture CR-020 §4 TD-001):
--   - Script is idempotent (guarded by OBJECT_ID, COL_LENGTH, and sys.indexes checks).
--   - Compatible with existing 0006_request_tables.sql and 0015_simplify_request_lifecycle.sql.
-- =============================================================================

IF OBJECT_ID(N'[request].[Requests]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[request].[Requests]', N'Deadline') IS NULL
    BEGIN
        ALTER TABLE [request].[Requests]
            ADD [Deadline] DATETIME2 NULL;
    END;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes 
        WHERE name = N'IX_Requests_Deadline' 
          AND object_id = OBJECT_ID(N'[request].[Requests]')
    )
    BEGIN
        EXEC(N'CREATE NONCLUSTERED INDEX [IX_Requests_Deadline] 
            ON [request].[Requests] ([Deadline])
            WHERE [Deadline] IS NOT NULL;');
    END;
END;
