-- =============================================================================
-- CAKRA — 0008_post_tables.sql
-- Slice: P6-S28 Post Module — Domain, Application Services & Persistence
--
-- Purpose:
--   Creates the authoritative Post tables in the [post] schema:
--     1. post.Posts
--     2. post.Comments
--     3. post.Reactions
--     4. post.PostReferences
--   (Note: post.FeedItems is created in P6-S29 0009_feed_items_table.sql)
--
-- Constraints & Rules (Architecture §6, §7, §8, §12, §15, §16, §17, §19.3, §20, §21):
--   - Zero cross-schema foreign keys: AuthorPersonId, PersonId, CustomerId,
--     ProductId, RequestId, WorkPackageId, and ReferenceId are stored as raw
--     UNIQUEIDENTIFIER values and validated through published application
--     query interfaces.
--   - Intra-schema foreign keys enforce referential integrity from Comments,
--     Reactions, and PostReferences to Posts.Id.
--   - Permanent Data Retention (§20, §21): Soft-delete / archive only.
--     Reactions use IsActive / RemovedAt rather than physical DELETE.
--   - All table creation statements are idempotent (guarded by OBJECT_ID).
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'post')
    EXEC (N'CREATE SCHEMA [post] AUTHORIZATION [dbo];');

-- -----------------------------------------------------------------------------
-- 1. post.Posts
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[post].[Posts]', N'U') IS NULL
BEGIN
    CREATE TABLE [post].[Posts] (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Title] NVARCHAR(255) NOT NULL,
        [Content] NVARCHAR(MAX) NOT NULL,
        [AuthorPersonId] UNIQUEIDENTIFIER NULL,
        [Source] NVARCHAR(30) NOT NULL CONSTRAINT [DF_Posts_Source] DEFAULT ('HUMAN_AUTHORED'),
        [SourceEventType] NVARCHAR(100) NULL,
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT [DF_Posts_Status] DEFAULT ('ACTIVE'),
        [Visibility] NVARCHAR(20) NOT NULL CONSTRAINT [DF_Posts_Visibility] DEFAULT ('VISIBLE'),
        [IsException] BIT NOT NULL CONSTRAINT [DF_Posts_IsException] DEFAULT (0),
        [ExceptionType] NVARCHAR(50) NULL,
        [CustomerId] UNIQUEIDENTIFIER NULL,
        [ProductId] UNIQUEIDENTIFIER NULL,
        [RequestId] UNIQUEIDENTIFIER NULL,
        [WorkPackageId] UNIQUEIDENTIFIER NULL,
        [ArchivedAt] DATETIME2 NULL,
        [ArchivedByPersonId] UNIQUEIDENTIFIER NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Posts_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_Posts] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [CK_Posts_Source] CHECK ([Source] IN ('HUMAN_AUTHORED', 'SYSTEM_GENERATED')),
        CONSTRAINT [CK_Posts_Status] CHECK ([Status] IN ('ACTIVE', 'ARCHIVED')),
        CONSTRAINT [CK_Posts_Visibility] CHECK ([Visibility] IN ('VISIBLE', 'HIDDEN')),
        CONSTRAINT [CK_Posts_ExceptionType] CHECK ([ExceptionType] IS NULL OR [ExceptionType] IN ('ESCALATION', 'STALLED', 'REJECTION'))
    );

    CREATE NONCLUSTERED INDEX [IX_Posts_Status_Visibility_CreatedAt]
        ON [post].[Posts] ([Status], [Visibility], [CreatedAt] DESC);
    CREATE NONCLUSTERED INDEX [IX_Posts_AuthorPersonId]
        ON [post].[Posts] ([AuthorPersonId]);
    CREATE NONCLUSTERED INDEX [IX_Posts_CustomerId]
        ON [post].[Posts] ([CustomerId]);
    CREATE NONCLUSTERED INDEX [IX_Posts_ProductId]
        ON [post].[Posts] ([ProductId]);
    CREATE NONCLUSTERED INDEX [IX_Posts_RequestId]
        ON [post].[Posts] ([RequestId]);
    CREATE NONCLUSTERED INDEX [IX_Posts_WorkPackageId]
        ON [post].[Posts] ([WorkPackageId]);
    CREATE NONCLUSTERED INDEX [IX_Posts_CreatedAt]
        ON [post].[Posts] ([CreatedAt] DESC);
END;

-- -----------------------------------------------------------------------------
-- 2. post.Comments
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[post].[Comments]', N'U') IS NULL
BEGIN
    CREATE TABLE [post].[Comments] (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [PostId] UNIQUEIDENTIFIER NOT NULL,
        [AuthorPersonId] UNIQUEIDENTIFIER NOT NULL,
        [Content] NVARCHAR(MAX) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT [DF_Comments_Status] DEFAULT ('ACTIVE'),
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Comments_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_Comments] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_Comments_Posts] FOREIGN KEY ([PostId])
            REFERENCES [post].[Posts] ([Id]),
        CONSTRAINT [CK_Comments_Status] CHECK ([Status] IN ('ACTIVE', 'HIDDEN'))
    );

    CREATE NONCLUSTERED INDEX [IX_Comments_PostId_CreatedAt]
        ON [post].[Comments] ([PostId], [CreatedAt] ASC);
    CREATE NONCLUSTERED INDEX [IX_Comments_AuthorPersonId]
        ON [post].[Comments] ([AuthorPersonId]);
END;

-- -----------------------------------------------------------------------------
-- 3. post.Reactions
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[post].[Reactions]', N'U') IS NULL
BEGIN
    CREATE TABLE [post].[Reactions] (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [PostId] UNIQUEIDENTIFIER NOT NULL,
        [CommentId] UNIQUEIDENTIFIER NULL,
        [PersonId] UNIQUEIDENTIFIER NOT NULL,
        [ReactionType] NVARCHAR(50) NOT NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_Reactions_IsActive] DEFAULT (1),
        [RemovedAt] DATETIME2 NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Reactions_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_Reactions] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_Reactions_Posts] FOREIGN KEY ([PostId])
            REFERENCES [post].[Posts] ([Id]),
        CONSTRAINT [CK_Reactions_ReactionType] CHECK ([ReactionType] IN (
            'SEEN',
            'EXPERIENCED',
            'HAVE_IDEA',
            'SIMILAR_ISSUE',
            'DUPLICATE',
            'NEED_CLARIFICATION'
        ))
    );

    CREATE UNIQUE NONCLUSTERED INDEX [UQ_Reactions_ActivePostPersonType]
        ON [post].[Reactions] ([PostId], [PersonId], [ReactionType])
        WHERE [IsActive] = 1 AND [RemovedAt] IS NULL AND [CommentId] IS NULL;

    CREATE UNIQUE NONCLUSTERED INDEX [UQ_Reactions_ActiveCommentPersonType]
        ON [post].[Reactions] ([PostId], [CommentId], [PersonId], [ReactionType])
        WHERE [IsActive] = 1 AND [RemovedAt] IS NULL AND [CommentId] IS NOT NULL;

    CREATE NONCLUSTERED INDEX [IX_Reactions_PostId_IsActive]
        ON [post].[Reactions] ([PostId], [IsActive], [RemovedAt]);
    CREATE NONCLUSTERED INDEX [IX_Reactions_PersonId]
        ON [post].[Reactions] ([PersonId]);
END;

-- -----------------------------------------------------------------------------
-- 4. post.PostReferences
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[post].[PostReferences]', N'U') IS NULL
BEGIN
    CREATE TABLE [post].[PostReferences] (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [PostId] UNIQUEIDENTIFIER NOT NULL,
        [ReferenceType] NVARCHAR(50) NOT NULL,
        [ReferenceId] UNIQUEIDENTIFIER NOT NULL,
        [ReferenceDisplay] NVARCHAR(255) NULL,
        [RemovedAt] DATETIME2 NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_PostReferences_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_PostReferences] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_PostReferences_Posts] FOREIGN KEY ([PostId])
            REFERENCES [post].[Posts] ([Id]),
        CONSTRAINT [CK_PostReferences_ReferenceType] CHECK ([ReferenceType] IN (
            'REQUEST',
            'WORK_PACKAGE',
            'CUSTOMER',
            'PRODUCT',
            'ORGANIZATION'
        ))
    );

    CREATE UNIQUE NONCLUSTERED INDEX [UQ_PostReferences_ActivePair]
        ON [post].[PostReferences] ([PostId], [ReferenceType], [ReferenceId])
        WHERE [RemovedAt] IS NULL;

    CREATE NONCLUSTERED INDEX [IX_PostReferences_PostId_RemovedAt]
        ON [post].[PostReferences] ([PostId], [RemovedAt]);
    CREATE NONCLUSTERED INDEX [IX_PostReferences_ReferenceType_ReferenceId]
        ON [post].[PostReferences] ([ReferenceType], [ReferenceId]);
END;
