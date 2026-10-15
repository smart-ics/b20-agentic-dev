-- =============================================================================
-- CAKRA — 0021_request_blockers.sql
-- Slice: P1-S01 Request Module — Database Migration Script for Request Blockers & Active Counter
--
-- Purpose:
--   1. Creates table [request].[RequestBlockers] to store blocker items associated
--      with requests with cascading referential integrity.
--   2. Adds [ActiveBlockersCount] column to [request].[Requests] table with check
--      constraint enforcing [ActiveBlockersCount] >= 0.
--   3. Backfills [ActiveBlockersCount] from [request].[RequestBlockers] ensuring
--      idempotent execution.
--
-- Constraints & Rules (Architecture CR-032 §4 TD-004):
--   - FK_RequestBlockers_Requests references [request].[Requests]([Id]) ON DELETE CASCADE.
--   - Nonclustered index on [RequestId].
--   - Filtered nonclustered index on ([RequestId], [IsCleared]) INCLUDE ([Reason], [CreatedAtUtc]) WHERE [IsCleared] = 0.
--   - Check constraint [CK_Requests_ActiveBlockersCount] enforces [ActiveBlockersCount] >= 0.
--   - Script is idempotent (guarded by OBJECT_ID, sys.columns, and sys.check_constraints).
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'request')
    EXEC (N'CREATE SCHEMA [request] AUTHORIZATION [dbo];');

-- -----------------------------------------------------------------------------
-- 1. [request].[RequestBlockers]
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[request].[RequestBlockers]', N'U') IS NULL
BEGIN
    CREATE TABLE [request].[RequestBlockers] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_RequestBlockers] PRIMARY KEY CLUSTERED DEFAULT NEWID(),
        [RequestId] UNIQUEIDENTIFIER NOT NULL,
        [Reason] NVARCHAR(1000) NOT NULL,
        [CreatedByPersonId] UNIQUEIDENTIFIER NOT NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_RequestBlockers_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
        [IsCleared] BIT NOT NULL CONSTRAINT [DF_RequestBlockers_IsCleared] DEFAULT 0,
        [ClearedAtUtc] DATETIME2 NULL,
        [ClearedByPersonId] UNIQUEIDENTIFIER NULL,
        [ClearanceReason] NVARCHAR(1000) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_RequestBlockers_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_RequestBlockers_UpdatedAt] DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [FK_RequestBlockers_Requests] FOREIGN KEY ([RequestId])
            REFERENCES [request].[Requests]([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_RequestBlockers_RequestId]
        ON [request].[RequestBlockers] ([RequestId]);

    CREATE NONCLUSTERED INDEX [IX_RequestBlockers_Active]
        ON [request].[RequestBlockers] ([RequestId], [IsCleared])
        INCLUDE ([Reason], [CreatedAtUtc])
        WHERE [IsCleared] = 0;
END;

-- -----------------------------------------------------------------------------
-- 2. Alter [request].[Requests] summary columns and constraints
-- -----------------------------------------------------------------------------
IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[request].[Requests]'))
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.columns 
        WHERE object_id = OBJECT_ID(N'[request].[Requests]') 
          AND name = N'ActiveBlockersCount'
    )
    BEGIN
        EXEC (N'ALTER TABLE [request].[Requests] ADD [ActiveBlockersCount] INT NOT NULL CONSTRAINT [DF_Requests_ActiveBlockersCount] DEFAULT 0;');
    END;

    IF NOT EXISTS (
        SELECT 1 
        FROM sys.check_constraints 
        WHERE name = N'CK_Requests_ActiveBlockersCount' 
          AND parent_object_id = OBJECT_ID(N'[request].[Requests]')
    )
    BEGIN
        EXEC (N'ALTER TABLE [request].[Requests] ADD CONSTRAINT [CK_Requests_ActiveBlockersCount] CHECK ([ActiveBlockersCount] >= 0);');
    END;

    -- Backfill ActiveBlockersCount from existing blockers table
    EXEC (N'UPDATE r
           SET r.[ActiveBlockersCount] = ISNULL(b.[Cnt], 0)
           FROM [request].[Requests] r
           LEFT JOIN (
               SELECT [RequestId], COUNT(*) AS [Cnt]
               FROM [request].[RequestBlockers]
               WHERE [IsCleared] = 0
               GROUP BY [RequestId]
           ) b ON r.[Id] = b.[RequestId];');
END;
