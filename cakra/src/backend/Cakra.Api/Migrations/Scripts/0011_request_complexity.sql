-- =============================================================================
-- CAKRA — 0011_request_complexity.sql
-- Slice: P1-S01 Request Module — Database Migration and Repository Mapping for Request Complexity
--
-- Purpose:
--   Adds [Complexity] column to [request].[Requests] table with default 1
--   and check constraint enforcing range between 1 and 5 (Architecture CR-005).
--
-- Constraints & Rules:
--   - Default constraint [DF_Requests_Complexity] defaults to 1.
--   - Check constraint [CK_Requests_Complexity_Range] enforces range 1 to 5.
--   - All existing historical records automatically receive value 1.
--   - Script is idempotent (guarded by sys.columns check).
-- =============================================================================

IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[request].[Requests]'))
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.columns 
        WHERE object_id = OBJECT_ID(N'[request].[Requests]') 
          AND name = N'Complexity'
    )
    BEGIN
        EXEC (N'ALTER TABLE [request].[Requests] ADD [Complexity] INT NOT NULL CONSTRAINT [DF_Requests_Complexity] DEFAULT (1) CONSTRAINT [CK_Requests_Complexity_Range] CHECK ([Complexity] BETWEEN 1 AND 5);');
    END;
END;
