-- Migration: Script0003_CreateOrganizationSchema.sql
-- Module: Organization (Architecture §6, §7, §17, §20)

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'organization')
BEGIN
    EXEC('CREATE SCHEMA [organization]');
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('[organization].[Persons]'))
BEGIN
    CREATE TABLE [organization].[Persons] (
        PersonId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(100) NOT NULL,
        Email NVARCHAR(150) NOT NULL,
        Status NVARCHAR(20) NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT PK_Persons PRIMARY KEY CLUSTERED (PersonId)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UQ_Persons_Email ON [organization].[Persons] (Email);
    CREATE NONCLUSTERED INDEX IX_Persons_Status ON [organization].[Persons] (Status);
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('[organization].[Teams]'))
BEGIN
    CREATE TABLE [organization].[Teams] (
        TeamId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500) NULL,
        Status NVARCHAR(20) NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT PK_Teams PRIMARY KEY CLUSTERED (TeamId)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UQ_Teams_Name ON [organization].[Teams] (Name);
    CREATE NONCLUSTERED INDEX IX_Teams_Status ON [organization].[Teams] (Status);
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('[organization].[Roles]'))
BEGIN
    CREATE TABLE [organization].[Roles] (
        RoleId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(50) NOT NULL,
        Description NVARCHAR(255) NULL,
        Status NVARCHAR(20) NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (RoleId)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UQ_Roles_Name ON [organization].[Roles] (Name);
    CREATE NONCLUSTERED INDEX IX_Roles_Status ON [organization].[Roles] (Status);
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('[organization].[Responsibilities]'))
BEGIN
    CREATE TABLE [organization].[Responsibilities] (
        ResponsibilityId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500) NULL,
        Status NVARCHAR(20) NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT PK_Responsibilities PRIMARY KEY CLUSTERED (ResponsibilityId)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UQ_Responsibilities_Name ON [organization].[Responsibilities] (Name);
    CREATE NONCLUSTERED INDEX IX_Responsibilities_Status ON [organization].[Responsibilities] (Status);
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('[organization].[TeamMemberships]'))
BEGIN
    CREATE TABLE [organization].[TeamMemberships] (
        MembershipId UNIQUEIDENTIFIER NOT NULL,
        PersonId UNIQUEIDENTIFIER NOT NULL,
        TeamId UNIQUEIDENTIFIER NOT NULL,
        JoinedAt DATETIME2 NOT NULL,
        LeftAt DATETIME2 NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_TeamMemberships_IsActive DEFAULT (1),
        CONSTRAINT PK_TeamMemberships PRIMARY KEY CLUSTERED (MembershipId),
        CONSTRAINT FK_TeamMemberships_Persons FOREIGN KEY (PersonId) REFERENCES [organization].[Persons](PersonId) ON DELETE CASCADE,
        CONSTRAINT FK_TeamMemberships_Teams FOREIGN KEY (TeamId) REFERENCES [organization].[Teams](TeamId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_TeamMemberships_PersonId ON [organization].[TeamMemberships] (PersonId);
    CREATE NONCLUSTERED INDEX IX_TeamMemberships_TeamId ON [organization].[TeamMemberships] (TeamId);
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('[organization].[RoleAssignments]'))
BEGIN
    CREATE TABLE [organization].[RoleAssignments] (
        AssignmentId UNIQUEIDENTIFIER NOT NULL,
        PersonId UNIQUEIDENTIFIER NOT NULL,
        RoleId UNIQUEIDENTIFIER NOT NULL,
        AssignedAt DATETIME2 NOT NULL,
        RevokedAt DATETIME2 NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_RoleAssignments_IsActive DEFAULT (1),
        CONSTRAINT PK_RoleAssignments PRIMARY KEY CLUSTERED (AssignmentId),
        CONSTRAINT FK_RoleAssignments_Persons FOREIGN KEY (PersonId) REFERENCES [organization].[Persons](PersonId) ON DELETE CASCADE,
        CONSTRAINT FK_RoleAssignments_Roles FOREIGN KEY (RoleId) REFERENCES [organization].[Roles](RoleId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_RoleAssignments_PersonId ON [organization].[RoleAssignments] (PersonId);
    CREATE NONCLUSTERED INDEX IX_RoleAssignments_RoleId ON [organization].[RoleAssignments] (RoleId);
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('[organization].[ResponsibilityAssignments]'))
BEGIN
    CREATE TABLE [organization].[ResponsibilityAssignments] (
        AssignmentId UNIQUEIDENTIFIER NOT NULL,
        PersonId UNIQUEIDENTIFIER NOT NULL,
        ResponsibilityId UNIQUEIDENTIFIER NOT NULL,
        AssignedAt DATETIME2 NOT NULL,
        RevokedAt DATETIME2 NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_ResponsibilityAssignments_IsActive DEFAULT (1),
        CONSTRAINT PK_ResponsibilityAssignments PRIMARY KEY CLUSTERED (AssignmentId),
        CONSTRAINT FK_ResponsibilityAssignments_Persons FOREIGN KEY (PersonId) REFERENCES [organization].[Persons](PersonId) ON DELETE CASCADE,
        CONSTRAINT FK_ResponsibilityAssignments_Responsibilities FOREIGN KEY (ResponsibilityId) REFERENCES [organization].[Responsibilities](ResponsibilityId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_ResponsibilityAssignments_PersonId ON [organization].[ResponsibilityAssignments] (PersonId);
    CREATE NONCLUSTERED INDEX IX_ResponsibilityAssignments_ResponsibilityId ON [organization].[ResponsibilityAssignments] (ResponsibilityId);
END;
