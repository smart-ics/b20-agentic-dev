-- =============================================================================
-- CAKRA — 0003_organization_tables.sql
-- Slice: P3-S12 Organization Module — Domain Entities & Persistence
--
-- Purpose:
--   Creates authoritative Organization master tables in the [organization] schema:
--     - organization.Persons
--     - organization.Teams
--     - organization.Roles
--     - organization.Responsibilities
--     - organization.TeamMemberships
--     - organization.RoleAssignments
--     - organization.ResponsibilityAssignments
--
-- Constraints & Rules (Architecture §17, §19.3, §20):
--   - Foreign keys exist exclusively between junction tables and parent tables
--     inside the [organization] schema. Zero cross-schema foreign keys.
--   - Status on Persons defaults to 'ACTIVE'.
--   - All table creation statements are idempotent (guarded by OBJECT_ID).
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'organization')
    EXEC (N'CREATE SCHEMA [organization] AUTHORIZATION [dbo];');

-- -----------------------------------------------------------------------------
-- 1. organization.Persons
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[organization].[Persons]', N'U') IS NULL
BEGIN
    CREATE TABLE [organization].[Persons] (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [FirstName] NVARCHAR(100) NOT NULL,
        [LastName] NVARCHAR(100) NOT NULL,
        [Email] NVARCHAR(255) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT [DF_Persons_Status] DEFAULT ('ACTIVE'),
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Persons_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_Persons] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [CK_Persons_Status] CHECK ([Status] IN ('ACTIVE', 'INACTIVE'))
    );

    CREATE UNIQUE NONCLUSTERED INDEX [UQ_Persons_Email] ON [organization].[Persons] ([Email]);
END;

-- -----------------------------------------------------------------------------
-- 2. organization.Teams
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[organization].[Teams]', N'U') IS NULL
BEGIN
    CREATE TABLE [organization].[Teams] (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Name] NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Teams_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_Teams] PRIMARY KEY CLUSTERED ([Id])
    );

    CREATE UNIQUE NONCLUSTERED INDEX [UQ_Teams_Name] ON [organization].[Teams] ([Name]);
END;

-- -----------------------------------------------------------------------------
-- 3. organization.Roles
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[organization].[Roles]', N'U') IS NULL
BEGIN
    CREATE TABLE [organization].[Roles] (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Name] NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Roles_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED ([Id])
    );

    CREATE UNIQUE NONCLUSTERED INDEX [UQ_Roles_Name] ON [organization].[Roles] ([Name]);
END;

-- -----------------------------------------------------------------------------
-- 4. organization.Responsibilities
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[organization].[Responsibilities]', N'U') IS NULL
BEGIN
    CREATE TABLE [organization].[Responsibilities] (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Name] NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Responsibilities_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_Responsibilities] PRIMARY KEY CLUSTERED ([Id])
    );

    CREATE UNIQUE NONCLUSTERED INDEX [UQ_Responsibilities_Name] ON [organization].[Responsibilities] ([Name]);
END;

-- -----------------------------------------------------------------------------
-- 5. organization.TeamMemberships
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[organization].[TeamMemberships]', N'U') IS NULL
BEGIN
    CREATE TABLE [organization].[TeamMemberships] (
        [PersonId] UNIQUEIDENTIFIER NOT NULL,
        [TeamId] UNIQUEIDENTIFIER NOT NULL,
        [AssignedAt] DATETIME2 NOT NULL CONSTRAINT [DF_TeamMemberships_AssignedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_TeamMemberships] PRIMARY KEY CLUSTERED ([PersonId], [TeamId]),
        CONSTRAINT [FK_TeamMemberships_Persons] FOREIGN KEY ([PersonId]) REFERENCES [organization].[Persons] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TeamMemberships_Teams] FOREIGN KEY ([TeamId]) REFERENCES [organization].[Teams] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_TeamMemberships_TeamId] ON [organization].[TeamMemberships] ([TeamId]);
END;

-- -----------------------------------------------------------------------------
-- 6. organization.RoleAssignments
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[organization].[RoleAssignments]', N'U') IS NULL
BEGIN
    CREATE TABLE [organization].[RoleAssignments] (
        [PersonId] UNIQUEIDENTIFIER NOT NULL,
        [RoleId] UNIQUEIDENTIFIER NOT NULL,
        [AssignedAt] DATETIME2 NOT NULL CONSTRAINT [DF_RoleAssignments_AssignedAt] DEFAULT (SYSUTCDATETIME()),
        [RevokedAt] DATETIME2 NULL,
        CONSTRAINT [PK_RoleAssignments] PRIMARY KEY CLUSTERED ([PersonId], [RoleId]),
        CONSTRAINT [FK_RoleAssignments_Persons] FOREIGN KEY ([PersonId]) REFERENCES [organization].[Persons] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RoleAssignments_Roles] FOREIGN KEY ([RoleId]) REFERENCES [organization].[Roles] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_RoleAssignments_RoleId] ON [organization].[RoleAssignments] ([RoleId]);
END;

-- -----------------------------------------------------------------------------
-- 7. organization.ResponsibilityAssignments
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[organization].[ResponsibilityAssignments]', N'U') IS NULL
BEGIN
    CREATE TABLE [organization].[ResponsibilityAssignments] (
        [PersonId] UNIQUEIDENTIFIER NOT NULL,
        [ResponsibilityId] UNIQUEIDENTIFIER NOT NULL,
        [AssignedAt] DATETIME2 NOT NULL CONSTRAINT [DF_ResponsibilityAssignments_AssignedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_ResponsibilityAssignments] PRIMARY KEY CLUSTERED ([PersonId], [ResponsibilityId]),
        CONSTRAINT [FK_ResponsibilityAssignments_Persons] FOREIGN KEY ([PersonId]) REFERENCES [organization].[Persons] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ResponsibilityAssignments_Responsibilities] FOREIGN KEY ([ResponsibilityId]) REFERENCES [organization].[Responsibilities] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_ResponsibilityAssignments_ResponsibilityId] ON [organization].[ResponsibilityAssignments] ([ResponsibilityId]);
END;
