-- =============================================================================
-- CAKRA — 0002_identity_tables.sql
-- Slice: P2-S09 Identity Module — Domain, Application Services & Persistence
--
-- Purpose:
--   Establishes IAM persistence tables in the identity schema per Architecture §14
--   (IAM Persistence Tables) and §17 (Database Design):
--     - identity.UserAccounts: user credentials and 1-to-1 linkage to Person
--     - identity.UserSessions: active login sessions and token expiration
--
-- Constraints & Rules (Architecture §20):
--   - No cross-schema foreign keys. PersonId is stored as UNIQUEIDENTIFIER without
--     a physical FK constraint to organization.Persons.
--   - Same-schema FK: UserSessions.UserId -> UserAccounts.UserId is enforced.
--   - Explicit indexes on unique and queried fields.
--   - Script is fully idempotent.
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[identity].[UserAccounts]'))
BEGIN
    CREATE TABLE [identity].[UserAccounts]
    (
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [PersonId] UNIQUEIDENTIFIER NOT NULL,
        [Username] VARCHAR(50) NOT NULL,
        [Email] VARCHAR(150) NOT NULL,
        [PasswordHash] VARCHAR(255) NOT NULL,
        [Status] VARCHAR(20) NOT NULL CONSTRAINT [DF_UserAccounts_Status] DEFAULT 'ACTIVE',
        [FailedLoginAttempts] INT NOT NULL CONSTRAINT [DF_UserAccounts_FailedLoginAttempts] DEFAULT 0,
        [LastLoginAt] DATETIME2 NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL,

        CONSTRAINT [PK_UserAccounts] PRIMARY KEY CLUSTERED ([UserId] ASC),
        CONSTRAINT [UQ_UserAccounts_PersonId] UNIQUE NONCLUSTERED ([PersonId] ASC),
        CONSTRAINT [UQ_UserAccounts_Username] UNIQUE NONCLUSTERED ([Username] ASC),
        CONSTRAINT [UQ_UserAccounts_Email] UNIQUE NONCLUSTERED ([Email] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_UserAccounts_Status]
        ON [identity].[UserAccounts] ([Status] ASC);
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[identity].[UserSessions]'))
BEGIN
    CREATE TABLE [identity].[UserSessions]
    (
        [SessionId] UNIQUEIDENTIFIER NOT NULL,
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [PersonId] UNIQUEIDENTIFIER NOT NULL,
        [SessionToken] VARCHAR(255) NOT NULL,
        [ExpiresAt] DATETIME2 NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL,
        [ClientIp] VARCHAR(45) NULL,
        [UserAgent] VARCHAR(255) NULL,
        [IsRevoked] BIT NOT NULL CONSTRAINT [DF_UserSessions_IsRevoked] DEFAULT 0,

        CONSTRAINT [PK_UserSessions] PRIMARY KEY CLUSTERED ([SessionId] ASC),
        CONSTRAINT [FK_UserSessions_UserAccounts] FOREIGN KEY ([UserId])
            REFERENCES [identity].[UserAccounts] ([UserId]),
        CONSTRAINT [UQ_UserSessions_SessionToken] UNIQUE NONCLUSTERED ([SessionToken] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_UserSessions_ExpiresAt]
        ON [identity].[UserSessions] ([ExpiresAt] ASC);

    CREATE NONCLUSTERED INDEX [IX_UserSessions_UserId]
        ON [identity].[UserSessions] ([UserId] ASC);

    CREATE NONCLUSTERED INDEX [IX_UserSessions_PersonId]
        ON [identity].[UserSessions] ([PersonId] ASC);
END;
