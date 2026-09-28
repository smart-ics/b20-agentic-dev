-- =============================================================================
-- CAKRA — 0007_workpackage_tables.sql
-- Slice: P5-S25 Work Package Module — Application Services & Persistence
--
-- Purpose:
--   Creates the authoritative Work Package tables in the [workpackage] schema:
--     1. workpackage.WorkPackages
--     2. workpackage.WorkPackageRequests
--
-- Constraints & Rules (Architecture §6, §7, §11, §15, §16, §17, §19.3, §20):
--   - Zero cross-schema foreign keys: OwnerPersonId, CustomerId, ProductId,
--     and RequestId are stored as raw UNIQUEIDENTIFIER values and validated
--     through published application query interfaces.
--   - Intra-schema foreign key enforces referential integrity from
--     WorkPackageRequests.WorkPackageId to WorkPackages.Id.
--   - Status on WorkPackages defaults to 'DRAFT' and is constrained to
--     ('DRAFT', 'ACTIVE', 'CLOSED').
--   - All table creation statements are idempotent (guarded by OBJECT_ID).
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'workpackage')
    EXEC (N'CREATE SCHEMA [workpackage] AUTHORIZATION [dbo];');

-- -----------------------------------------------------------------------------
-- 1. workpackage.WorkPackages
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[workpackage].[WorkPackages]', N'U') IS NULL
BEGIN
    CREATE TABLE [workpackage].[WorkPackages] (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Name] NVARCHAR(255) NOT NULL,
        [Objective] NVARCHAR(MAX) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT [DF_WorkPackages_Status] DEFAULT ('DRAFT'),
        [OwnerPersonId] UNIQUEIDENTIFIER NOT NULL,
        [CustomerId] UNIQUEIDENTIFIER NULL,
        [ProductId] UNIQUEIDENTIFIER NULL,
        [ClosedReason] NVARCHAR(MAX) NULL,
        [ClosedAt] DATETIME2 NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_WorkPackages_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_WorkPackages] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [CK_WorkPackages_Status] CHECK ([Status] IN ('DRAFT', 'ACTIVE', 'CLOSED'))
    );

    CREATE NONCLUSTERED INDEX [IX_WorkPackages_Status] ON [workpackage].[WorkPackages] ([Status]);
    CREATE NONCLUSTERED INDEX [IX_WorkPackages_OwnerPersonId] ON [workpackage].[WorkPackages] ([OwnerPersonId]);
    CREATE NONCLUSTERED INDEX [IX_WorkPackages_CustomerId] ON [workpackage].[WorkPackages] ([CustomerId]);
    CREATE NONCLUSTERED INDEX [IX_WorkPackages_ProductId] ON [workpackage].[WorkPackages] ([ProductId]);
    CREATE NONCLUSTERED INDEX [IX_WorkPackages_CreatedAt] ON [workpackage].[WorkPackages] ([CreatedAt] DESC);
END;

-- -----------------------------------------------------------------------------
-- 2. workpackage.WorkPackageRequests
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[workpackage].[WorkPackageRequests]', N'U') IS NULL
BEGIN
    CREATE TABLE [workpackage].[WorkPackageRequests] (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [WorkPackageId] UNIQUEIDENTIFIER NOT NULL,
        [RequestId] UNIQUEIDENTIFIER NOT NULL,
        [AddedAt] DATETIME2 NOT NULL,
        [RemovedAt] DATETIME2 NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_WorkPackageRequests_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_WorkPackageRequests] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_WorkPackageRequests_WorkPackages] FOREIGN KEY ([WorkPackageId])
            REFERENCES [workpackage].[WorkPackages] ([Id]) ON DELETE CASCADE
    );

    CREATE UNIQUE NONCLUSTERED INDEX [UQ_WorkPackageRequests_ActivePair]
        ON [workpackage].[WorkPackageRequests] ([WorkPackageId], [RequestId])
        WHERE [RemovedAt] IS NULL;
    CREATE NONCLUSTERED INDEX [IX_WorkPackageRequests_WorkPackageId_AddedAt]
        ON [workpackage].[WorkPackageRequests] ([WorkPackageId], [AddedAt]);
    CREATE NONCLUSTERED INDEX [IX_WorkPackageRequests_RequestId_RemovedAt]
        ON [workpackage].[WorkPackageRequests] ([RequestId], [RemovedAt]);
END;
