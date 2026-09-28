-- =============================================================================
-- CAKRA — 0009_feed_items_table.sql
-- Slice: P6-S29 Feed Projection — FeedItems Table & FeedProjectionHandler
--
-- Purpose:
--   Creates the materialized read model table [post].[FeedItems] in the [post]
--   schema (Architecture §6, §7, §8, §9, §12, §16, §17, §19.3, §20, §21).
--
-- Constraints & Rules:
--   - Includes all columns defined in Architecture §12 (Feed Projection Table
--     Schema) as well as convenience alias columns (Summary, Source, RequestId,
--     WorkPackageId, LastActivityAt) for sub-50ms single-table indexed queries.
--   - Intra-schema foreign key FK_FeedItems_Posts references [post].[Posts]([Id])
--     and unique constraint UQ_FeedItems_PostId enforces 1-to-1 projection per Post.
--   - Zero cross-schema foreign keys: AuthorPersonId, CustomerId, ProductId,
--     RequestId, WorkPackageId, and ReferenceId are stored as raw UNIQUEIDENTIFIER
--     values.
--   - Idempotent table and index creation guarded by OBJECT_ID / sys.indexes.
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'post')
    EXEC (N'CREATE SCHEMA [post] AUTHORIZATION [dbo];');

IF OBJECT_ID(N'[post].[FeedItems]', N'U') IS NULL
BEGIN
    CREATE TABLE [post].[FeedItems] (
        [FeedItemId] UNIQUEIDENTIFIER NOT NULL,
        [PostId] UNIQUEIDENTIFIER NOT NULL,
        [AuthorPersonId] UNIQUEIDENTIFIER NULL,
        [AuthorName] NVARCHAR(150) NULL,
        [PostType] NVARCHAR(30) NOT NULL CONSTRAINT [DF_FeedItems_PostType] DEFAULT ('HUMAN_AUTHORED'),
        [Source] NVARCHAR(30) NOT NULL CONSTRAINT [DF_FeedItems_Source] DEFAULT ('HUMAN_AUTHORED'),
        [Title] NVARCHAR(255) NOT NULL,
        [ContentExcerpt] NVARCHAR(500) NOT NULL CONSTRAINT [DF_FeedItems_ContentExcerpt] DEFAULT (''),
        [Summary] NVARCHAR(500) NOT NULL CONSTRAINT [DF_FeedItems_Summary] DEFAULT (''),
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT [DF_FeedItems_Status] DEFAULT ('ACTIVE'),
        [Visibility] NVARCHAR(20) NOT NULL CONSTRAINT [DF_FeedItems_Visibility] DEFAULT ('VISIBLE'),
        [IsException] BIT NOT NULL CONSTRAINT [DF_FeedItems_IsException] DEFAULT (0),
        [ExceptionType] NVARCHAR(50) NULL,
        [ReferenceType] NVARCHAR(50) NULL,
        [ReferenceId] UNIQUEIDENTIFIER NULL,
        [ReferenceDisplay] NVARCHAR(255) NULL,
        [CustomerId] UNIQUEIDENTIFIER NULL,
        [CustomerName] NVARCHAR(150) NULL,
        [ProductId] UNIQUEIDENTIFIER NULL,
        [ProductName] NVARCHAR(150) NULL,
        [RequestId] UNIQUEIDENTIFIER NULL,
        [WorkPackageId] UNIQUEIDENTIFIER NULL,
        [CommentCount] INT NOT NULL CONSTRAINT [DF_FeedItems_CommentCount] DEFAULT (0),
        [LatestCommentExcerpt] NVARCHAR(300) NULL,
        [ReactionCountsJson] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_FeedItems_ReactionCountsJson] DEFAULT ('{}'),
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_FeedItems_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_FeedItems_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
        [LastActivityAt] DATETIME2 NOT NULL CONSTRAINT [DF_FeedItems_LastActivityAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_FeedItems] PRIMARY KEY CLUSTERED ([FeedItemId]),
        CONSTRAINT [UQ_FeedItems_PostId] UNIQUE NONCLUSTERED ([PostId]),
        CONSTRAINT [FK_FeedItems_Posts] FOREIGN KEY ([PostId])
            REFERENCES [post].[Posts] ([Id]),
        CONSTRAINT [CK_FeedItems_PostType] CHECK ([PostType] IN ('HUMAN_AUTHORED', 'SYSTEM_GENERATED')),
        CONSTRAINT [CK_FeedItems_Source] CHECK ([Source] IN ('HUMAN_AUTHORED', 'SYSTEM_GENERATED')),
        CONSTRAINT [CK_FeedItems_Status] CHECK ([Status] IN ('ACTIVE', 'ARCHIVED')),
        CONSTRAINT [CK_FeedItems_Visibility] CHECK ([Visibility] IN ('VISIBLE', 'HIDDEN')),
        CONSTRAINT [CK_FeedItems_ExceptionType] CHECK ([ExceptionType] IS NULL OR [ExceptionType] IN ('ESCALATION', 'STALLED', 'REJECTION'))
    );

    -- Primary Feed query index supporting Visibility + Status + CreatedAt DESC (Architecture §12, §20)
    CREATE NONCLUSTERED INDEX [IX_FeedItems_Visibility_Status_CreatedAt]
        ON [post].[FeedItems] ([Visibility], [Status], [CreatedAt] DESC);

    -- Filtered query indexes for Customer, Product, Exception, Request, WorkPackage, CreatedAt, and LastActivityAt
    CREATE NONCLUSTERED INDEX [IX_FeedItems_CustomerId_CreatedAt]
        ON [post].[FeedItems] ([CustomerId], [CreatedAt] DESC);

    CREATE NONCLUSTERED INDEX [IX_FeedItems_ProductId_CreatedAt]
        ON [post].[FeedItems] ([ProductId], [CreatedAt] DESC);

    CREATE NONCLUSTERED INDEX [IX_FeedItems_IsException_CreatedAt]
        ON [post].[FeedItems] ([IsException], [CreatedAt] DESC);

    CREATE NONCLUSTERED INDEX [IX_FeedItems_RequestId]
        ON [post].[FeedItems] ([RequestId]);

    CREATE NONCLUSTERED INDEX [IX_FeedItems_WorkPackageId]
        ON [post].[FeedItems] ([WorkPackageId]);

    CREATE NONCLUSTERED INDEX [IX_FeedItems_CreatedAt]
        ON [post].[FeedItems] ([CreatedAt] DESC);

    CREATE NONCLUSTERED INDEX [IX_FeedItems_LastActivityAt]
        ON [post].[FeedItems] ([LastActivityAt] DESC);
END;
