-- =============================================================================
-- CAKRA — 0006_request_tables.sql
-- Slice: P4-S18 Request Module — Persistence & Core Commands
--
-- Purpose:
--   Creates the authoritative Request lifecycle tables in the [request] schema:
--     1. request.Requests
--     2. request.RequestResolutions
--     3. request.RequestAssignments
--
-- Constraints & Rules (Architecture §6, §7, §8, §16, §17, §18, §19.3, §20):
--   - Zero cross-schema foreign keys: OwnerPersonId, CustomerId, ProductId,
--     WorkPackageId, ResolvedBy, PreviousOwnerPersonId, AssignedOwnerPersonId,
--     and ActorPersonId are stored as raw UNIQUEIDENTIFIER values and validated
--     through published application query interfaces.
--   - Same-schema foreign keys enforce referential integrity from
--     RequestResolutions and RequestAssignments to Requests.
--   - All table creation statements are idempotent (guarded by OBJECT_ID).
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'request')
    EXEC (N'CREATE SCHEMA [request] AUTHORIZATION [dbo];');

-- -----------------------------------------------------------------------------
-- 1. request.Requests
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[request].[Requests]', N'U') IS NULL
BEGIN
    CREATE TABLE [request].[Requests] (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Title] NVARCHAR(255) NOT NULL,
        [Description] NVARCHAR(MAX) NOT NULL,
        [RequestType] NVARCHAR(50) NOT NULL CONSTRAINT [DF_Requests_RequestType] DEFAULT ('GENERAL'),
        [Status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_Requests_Status] DEFAULT ('CAPTURED'),
        [Priority] NVARCHAR(20) NOT NULL CONSTRAINT [DF_Requests_Priority] DEFAULT ('NORMAL'),
        [OwnerPersonId] UNIQUEIDENTIFIER NULL,
        [CustomerId] UNIQUEIDENTIFIER NULL,
        [ProductId] UNIQUEIDENTIFIER NULL,
        [WorkPackageId] UNIQUEIDENTIFIER NULL,
        [EvaluationNotes] NVARCHAR(MAX) NULL,
        [EscalationReason] NVARCHAR(MAX) NULL,
        [ManagementDecisionNotes] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Requests_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_Requests] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [CK_Requests_Status] CHECK ([Status] IN ('CAPTURED', 'EVALUATING', 'ACCEPTED', 'REJECTED', 'IN_PROGRESS', 'ESCALATED', 'COMPLETED'))
    );

    CREATE NONCLUSTERED INDEX [IX_Requests_Status] ON [request].[Requests] ([Status]);
    CREATE NONCLUSTERED INDEX [IX_Requests_OwnerPersonId] ON [request].[Requests] ([OwnerPersonId]);
    CREATE NONCLUSTERED INDEX [IX_Requests_CustomerId] ON [request].[Requests] ([CustomerId]);
    CREATE NONCLUSTERED INDEX [IX_Requests_ProductId] ON [request].[Requests] ([ProductId]);
    CREATE NONCLUSTERED INDEX [IX_Requests_CreatedAt] ON [request].[Requests] ([CreatedAt] DESC);
END;

-- -----------------------------------------------------------------------------
-- 2. request.RequestResolutions
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[request].[RequestResolutions]', N'U') IS NULL
BEGIN
    CREATE TABLE [request].[RequestResolutions] (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [RequestId] UNIQUEIDENTIFIER NOT NULL,
        [Outcome] NVARCHAR(30) NOT NULL,
        [Description] NVARCHAR(MAX) NOT NULL,
        [ResolvedBy] UNIQUEIDENTIFIER NOT NULL,
        [ResolvedAt] DATETIME2 NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_RequestResolutions_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_RequestResolutions] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_RequestResolutions_Requests] FOREIGN KEY ([RequestId])
            REFERENCES [request].[Requests] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [CK_RequestResolutions_Outcome] CHECK ([Outcome] IN ('REJECTED', 'COMPLETED', 'RESOLVED'))
    );

    CREATE UNIQUE NONCLUSTERED INDEX [UQ_RequestResolutions_RequestId] ON [request].[RequestResolutions] ([RequestId]);
    CREATE NONCLUSTERED INDEX [IX_RequestResolutions_ResolvedBy] ON [request].[RequestResolutions] ([ResolvedBy]);
END;

-- -----------------------------------------------------------------------------
-- 3. request.RequestAssignments
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[request].[RequestAssignments]', N'U') IS NULL
BEGIN
    CREATE TABLE [request].[RequestAssignments] (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [RequestId] UNIQUEIDENTIFIER NOT NULL,
        [PreviousOwnerPersonId] UNIQUEIDENTIFIER NULL,
        [AssignedOwnerPersonId] UNIQUEIDENTIFIER NULL,
        [ActorPersonId] UNIQUEIDENTIFIER NOT NULL,
        [PreviousStatus] NVARCHAR(30) NULL,
        [NewStatus] NVARCHAR(30) NOT NULL,
        [AssignedAtUtc] DATETIME2 NOT NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_RequestAssignments_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_RequestAssignments] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_RequestAssignments_Requests] FOREIGN KEY ([RequestId])
            REFERENCES [request].[Requests] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [CK_RequestAssignments_PreviousStatus] CHECK (
            [PreviousStatus] IS NULL OR
            [PreviousStatus] IN ('CAPTURED', 'EVALUATING', 'ACCEPTED', 'REJECTED', 'IN_PROGRESS', 'ESCALATED', 'COMPLETED')
        ),
        CONSTRAINT [CK_RequestAssignments_NewStatus] CHECK (
            [NewStatus] IN ('CAPTURED', 'EVALUATING', 'ACCEPTED', 'REJECTED', 'IN_PROGRESS', 'ESCALATED', 'COMPLETED')
        )
    );

    CREATE NONCLUSTERED INDEX [IX_RequestAssignments_RequestId_AssignedAtUtc]
        ON [request].[RequestAssignments] ([RequestId], [AssignedAtUtc]);
    CREATE NONCLUSTERED INDEX [IX_RequestAssignments_ActorPersonId]
        ON [request].[RequestAssignments] ([ActorPersonId]);
END;
