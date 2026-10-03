-- =============================================================================
-- CAKRA — 0012_request_subtasks.sql
-- Slice: P1-S01 Request Module — Database Migration and Repository Mapping for Request Sub-Tasks
--
-- Purpose:
--   1. Creates table [request].[RequestSubTasks] to store child sub-task checklist
--      items associated with parent requests with cascading referential integrity.
--   2. Adds [TotalSubTasksCount], [CompletedSubTasksCount], and [CompletionPercentage]
--      columns to [request].[Requests] table with check constraint enforcing range 0-100.
--   3. Backfills existing records cleanly (completed requests default to 100%).
--
-- Constraints & Rules (Architecture CR-006 §8, §10):
--   - FK_RequestSubTasks_Requests references [request].[Requests]([Id]) ON DELETE CASCADE.
--   - Indexes on [RequestId] and filtered index on [AssigneePersonId].
--   - Check constraint [CK_Requests_CompletionPercentage] enforces range 0 to 100.
--   - Script is idempotent (guarded by OBJECT_ID, sys.columns, and sys.check_constraints).
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'request')
    EXEC (N'CREATE SCHEMA [request] AUTHORIZATION [dbo];');

-- -----------------------------------------------------------------------------
-- 1. [request].[RequestSubTasks]
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[request].[RequestSubTasks]', N'U') IS NULL
BEGIN
    CREATE TABLE [request].[RequestSubTasks] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_RequestSubTasks] PRIMARY KEY CLUSTERED DEFAULT NEWID(),
        [RequestId] UNIQUEIDENTIFIER NOT NULL,
        [Title] NVARCHAR(255) NOT NULL,
        [IsCompleted] BIT NOT NULL CONSTRAINT [DF_RequestSubTasks_IsCompleted] DEFAULT 0,
        [AssigneePersonId] UNIQUEIDENTIFIER NULL,
        [CompletedAt] DATETIME2 NULL,
        [CompletedByPersonId] UNIQUEIDENTIFIER NULL,
        [SortOrder] INT NOT NULL CONSTRAINT [DF_RequestSubTasks_SortOrder] DEFAULT 0,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_RequestSubTasks_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_RequestSubTasks_UpdatedAt] DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [FK_RequestSubTasks_Requests] FOREIGN KEY ([RequestId])
            REFERENCES [request].[Requests]([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_RequestSubTasks_RequestId]
        ON [request].[RequestSubTasks] ([RequestId]);

    CREATE NONCLUSTERED INDEX [IX_RequestSubTasks_AssigneePersonId]
        ON [request].[RequestSubTasks] ([AssigneePersonId])
        WHERE [AssigneePersonId] IS NOT NULL;
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
          AND name = N'TotalSubTasksCount'
    )
    BEGIN
        EXEC (N'ALTER TABLE [request].[Requests] ADD [TotalSubTasksCount] INT NOT NULL CONSTRAINT [DF_Requests_TotalSubTasksCount] DEFAULT 0;');
    END;

    IF NOT EXISTS (
        SELECT 1 
        FROM sys.columns 
        WHERE object_id = OBJECT_ID(N'[request].[Requests]') 
          AND name = N'CompletedSubTasksCount'
    )
    BEGIN
        EXEC (N'ALTER TABLE [request].[Requests] ADD [CompletedSubTasksCount] INT NOT NULL CONSTRAINT [DF_Requests_CompletedSubTasksCount] DEFAULT 0;');
    END;

    IF NOT EXISTS (
        SELECT 1 
        FROM sys.columns 
        WHERE object_id = OBJECT_ID(N'[request].[Requests]') 
          AND name = N'CompletionPercentage'
    )
    BEGIN
        EXEC (N'ALTER TABLE [request].[Requests] ADD [CompletionPercentage] INT NOT NULL CONSTRAINT [DF_Requests_CompletionPercentage] DEFAULT 0;');
    END;

    IF NOT EXISTS (
        SELECT 1 
        FROM sys.check_constraints 
        WHERE name = N'CK_Requests_CompletionPercentage'
          AND parent_object_id = OBJECT_ID(N'[request].[Requests]')
    )
    BEGIN
        EXEC (N'ALTER TABLE [request].[Requests] ADD CONSTRAINT [CK_Requests_CompletionPercentage] CHECK ([CompletionPercentage] BETWEEN 0 AND 100);');
    END;

    -- Backfill existing historical records cleanly
    EXEC (N'UPDATE [request].[Requests]
           SET [TotalSubTasksCount] = ISNULL([TotalSubTasksCount], 0),
               [CompletedSubTasksCount] = ISNULL([CompletedSubTasksCount], 0),
               [CompletionPercentage] = CASE WHEN [Status] = ''COMPLETED'' THEN 100 ELSE ISNULL([CompletionPercentage], 0) END
           WHERE [TotalSubTasksCount] IS NULL OR [CompletionPercentage] IS NULL OR ([Status] = ''COMPLETED'' AND [CompletionPercentage] = 0);');
END;
