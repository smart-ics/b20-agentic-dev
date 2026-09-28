-- Migration: Script0002_CreateIdentitySchema.sql
-- Module: Identity & Access Management (Architecture §14, §17, §20)

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'identity')
BEGIN
    EXEC('CREATE SCHEMA [identity]');
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('[identity].[UserAccounts]'))
BEGIN
    CREATE TABLE [identity].[UserAccounts] (
        UserId UNIQUEIDENTIFIER NOT NULL,
        PersonId UNIQUEIDENTIFIER NOT NULL,
        Username NVARCHAR(50) NOT NULL,
        Email NVARCHAR(150) NOT NULL,
        PasswordHash NVARCHAR(255) NOT NULL,
        Status NVARCHAR(20) NOT NULL,
        FailedLoginAttempts INT NOT NULL CONSTRAINT DF_UserAccounts_FailedLoginAttempts DEFAULT (0),
        LastLoginAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT PK_UserAccounts PRIMARY KEY CLUSTERED (UserId)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UQ_UserAccounts_PersonId ON [identity].[UserAccounts] (PersonId);
    CREATE UNIQUE NONCLUSTERED INDEX UQ_UserAccounts_Username ON [identity].[UserAccounts] (Username);
    CREATE UNIQUE NONCLUSTERED INDEX UQ_UserAccounts_Email ON [identity].[UserAccounts] (Email);
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('[identity].[UserSessions]'))
BEGIN
    CREATE TABLE [identity].[UserSessions] (
        SessionId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        PersonId UNIQUEIDENTIFIER NOT NULL,
        SessionToken NVARCHAR(255) NOT NULL,
        ExpiresAt DATETIME2 NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        ClientIp NVARCHAR(45) NULL,
        UserAgent NVARCHAR(255) NULL,
        IsRevoked BIT NOT NULL CONSTRAINT DF_UserSessions_IsRevoked DEFAULT (0),
        CONSTRAINT PK_UserSessions PRIMARY KEY CLUSTERED (SessionId),
        CONSTRAINT FK_UserSessions_UserAccounts FOREIGN KEY (UserId) REFERENCES [identity].[UserAccounts](UserId) ON DELETE CASCADE
    );

    CREATE UNIQUE NONCLUSTERED INDEX UQ_UserSessions_SessionToken ON [identity].[UserSessions] (SessionToken);
    CREATE NONCLUSTERED INDEX IX_UserSessions_ExpiresAt ON [identity].[UserSessions] (ExpiresAt);
    CREATE NONCLUSTERED INDEX IX_UserSessions_UserId ON [identity].[UserSessions] (UserId);
END;
