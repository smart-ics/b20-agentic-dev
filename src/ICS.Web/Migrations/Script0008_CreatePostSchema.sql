-- Migration: Script0008_CreatePostSchema.sql
-- Module: Post (Architecture §12, §17, §19.3, §20)
-- Creates post.* schema tables: Posts, Comments, Reactions, PostReferences.
-- FeedItems table is implemented in P6-S24 (Script0009).
-- Idempotent: checks for existence before creation.
-- Dependency order: post schema must exist before any post tables.

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'post')
BEGIN
    EXEC('CREATE SCHEMA [post]');
END;

-- 1. [post].[Posts]
-- Authoritative operational communication records (human-authored or system-generated).
-- Architecture §17, §20 (Permanent Retention: soft-delete/archive only, no physical purge).
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('[post].[Posts]'))
BEGIN
    CREATE TABLE [post].[Posts] (
        PostId UNIQUEIDENTIFIER NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Content NVARCHAR(MAX) NOT NULL,
        Source NVARCHAR(30) NOT NULL,               -- 'SYSTEM_GENERATED' | 'HUMAN_AUTHORED'
        Status NVARCHAR(20) NOT NULL,               -- 'ACTIVE' | 'ARCHIVED'
        Visibility NVARCHAR(20) NOT NULL,           -- 'VISIBLE' | 'HIDDEN'
        AuthorPersonId UNIQUEIDENTIFIER NULL,       -- NULL for system-generated posts
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NULL,
        ArchivedAt DATETIME2 NULL,
        CONSTRAINT PK_Posts PRIMARY KEY CLUSTERED (PostId)
    );

    CREATE NONCLUSTERED INDEX IX_Posts_Status ON [post].[Posts] (Status);
    CREATE NONCLUSTERED INDEX IX_Posts_Visibility ON [post].[Posts] (Visibility);
    CREATE NONCLUSTERED INDEX IX_Posts_Source ON [post].[Posts] (Source);
    CREATE NONCLUSTERED INDEX IX_Posts_AuthorPersonId ON [post].[Posts] (AuthorPersonId);
    CREATE NONCLUSTERED INDEX IX_Posts_CreatedAt ON [post].[Posts] (CreatedAt);
END;

-- 2. [post].[Comments]
-- Flat discussion model: Comments belong to exactly one Post and have no child comments.
-- Architecture §17, §20 (Permanent Retention: soft-delete only).
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('[post].[Comments]'))
BEGIN
    CREATE TABLE [post].[Comments] (
        CommentId UNIQUEIDENTIFIER NOT NULL,
        PostId UNIQUEIDENTIFIER NOT NULL,
        AuthorPersonId UNIQUEIDENTIFIER NOT NULL,
        Content NVARCHAR(MAX) NOT NULL,
        Status NVARCHAR(20) NOT NULL,                -- 'ACTIVE' | 'HIDDEN'
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT PK_Comments PRIMARY KEY CLUSTERED (CommentId),
        CONSTRAINT FK_Comments_Posts FOREIGN KEY (PostId)
            REFERENCES [post].[Posts] (PostId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_Comments_PostId ON [post].[Comments] (PostId);
    CREATE NONCLUSTERED INDEX IX_Comments_AuthorPersonId ON [post].[Comments] (AuthorPersonId);
    CREATE NONCLUSTERED INDEX IX_Comments_Status ON [post].[Comments] (Status);
    CREATE NONCLUSTERED INDEX IX_Comments_CreatedAt ON [post].[Comments] (CreatedAt);
END;

-- 3. [post].[Reactions]
-- Structured operational responses to a Post or Comment.
-- A Person may express at most one Reaction of the same type on the same Post/Comment.
-- Architecture §17, §20 (Permanent Retention: no physical purge).
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('[post].[Reactions]'))
BEGIN
    CREATE TABLE [post].[Reactions] (
        ReactionId UNIQUEIDENTIFIER NOT NULL,
        PostId UNIQUEIDENTIFIER NULL,                -- NULL when reacting to a Comment
        CommentId UNIQUEIDENTIFIER NULL,             -- NULL when reacting to a Post
        PersonId UNIQUEIDENTIFIER NOT NULL,
        Type NVARCHAR(30) NOT NULL,                  -- 'SEEN' | 'EXPERIENCED' | 'HAVE_IDEA' | 'SIMILAR_ISSUE' | 'DUPLICATE' | 'NEED_CLARIFICATION'
        Status NVARCHAR(20) NOT NULL DEFAULT 'ACTIVE',  -- For soft-delete: no physical purge
        CreatedAt DATETIME2 NOT NULL,
        CONSTRAINT PK_Reactions PRIMARY KEY CLUSTERED (ReactionId),
        CONSTRAINT FK_Reactions_Posts FOREIGN KEY (PostId)
            REFERENCES [post].[Posts] (PostId) ON DELETE CASCADE,
        CONSTRAINT FK_Reactions_Comments FOREIGN KEY (CommentId)
            REFERENCES [post].[Comments] (CommentId) ON DELETE CASCADE,
        CONSTRAINT CK_Reaction_Target CHECK (PostId IS NOT NULL OR CommentId IS NOT NULL)
    );

    CREATE NONCLUSTERED INDEX IX_Reactions_PostId ON [post].[Reactions] (PostId);
    CREATE NONCLUSTERED INDEX IX_Reactions_CommentId ON [post].[Reactions] (CommentId);
    CREATE NONCLUSTERED INDEX IX_Reactions_PersonId ON [post].[Reactions] (PersonId);
    CREATE NONCLUSTERED INDEX IX_Reactions_Type ON [post].[Reactions] (Type);
    -- Unique constraint: one reaction per person per type per target
    CREATE UNIQUE INDEX IX_Reactions_UniqueTargetPersonType
        ON [post].[Reactions] (PostId, PersonId, Type)
        WHERE PostId IS NOT NULL;
    CREATE UNIQUE INDEX IX_Reactions_UniqueCommentPersonType
        ON [post].[Reactions] (CommentId, PersonId, Type)
        WHERE CommentId IS NOT NULL;
END;

-- 4. [post].[PostReferences]
-- Contextual links from a Post to external domain objects (Request, WorkPackage, Customer, Product, Organization).
-- The referenced domain remains authoritative for the referenced object's current state.
-- Architecture §17, §20: no cross-schema physical FK constraints (raw identifier values only).
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('[post].[PostReferences]'))
BEGIN
    CREATE TABLE [post].[PostReferences] (
        ReferenceId UNIQUEIDENTIFIER NOT NULL,
        PostId UNIQUEIDENTIFIER NOT NULL,
        ReferenceType NVARCHAR(30) NOT NULL,         -- 'REQUEST' | 'WORK_PACKAGE' | 'CUSTOMER' | 'PRODUCT' | 'ORGANIZATION'
        ReferenceId UNIQUEIDENTIFIER NOT NULL,       -- raw identifier value, validated through application query services
        CreatedAt DATETIME2 NOT NULL,
        CONSTRAINT PK_PostReferences PRIMARY KEY CLUSTERED (ReferenceId),
        CONSTRAINT FK_PostReferences_Posts FOREIGN KEY (PostId)
            REFERENCES [post].[Posts] (PostId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_PostReferences_PostId ON [post].[PostReferences] (PostId);
    CREATE NONCLUSTERED INDEX IX_PostReferences_ReferenceType ON [post].[PostReferences] (ReferenceType);
    CREATE NONCLUSTERED INDEX IX_PostReferences_ReferenceId ON [post].[PostReferences] (ReferenceId);
END;