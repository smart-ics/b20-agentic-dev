-- Migration: Script0006_CreateRequestSchema.sql
-- Module: Request (Architecture §6, §7, §8, §13, §16, §17, §18, §19.3, §20)

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'request')
BEGIN
    EXEC('CREATE SCHEMA [request]');
END;

-- 1. [request].[Requests]
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('[request].[Requests]'))
BEGIN
    CREATE TABLE [request].[Requests] (
        RequestId UNIQUEIDENTIFIER NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(MAX) NOT NULL,
        Type NVARCHAR(50) NOT NULL,
        Priority NVARCHAR(50) NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        RequesterPersonId UNIQUEIDENTIFIER NULL,
        RequesterContactId UNIQUEIDENTIFIER NULL,
        RequesterName NVARCHAR(200) NULL,
        OwnerPersonId UNIQUEIDENTIFIER NULL,
        CustomerId UNIQUEIDENTIFIER NULL,
        ProductId UNIQUEIDENTIFIER NULL,
        WorkPackageId UNIQUEIDENTIFIER NULL,
        IsAwaitingManagementDecision BIT NOT NULL DEFAULT 0,
        ManagementDecisionQuestion NVARCHAR(MAX) NULL,
        ManagementDecisionOptions NVARCHAR(MAX) NULL,
        ManagementDecisionImpact NVARCHAR(MAX) NULL,
        ManagementDecisionRequestedAt DATETIME2 NULL,
        EscalationReason NVARCHAR(MAX) NULL,
        EscalatedByPersonId UNIQUEIDENTIFIER NULL,
        EscalatedAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NULL,
        ClosedAt DATETIME2 NULL,
        CONSTRAINT PK_Requests PRIMARY KEY CLUSTERED (RequestId)
    );

    CREATE NONCLUSTERED INDEX IX_Requests_Status ON [request].[Requests] (Status);
    CREATE NONCLUSTERED INDEX IX_Requests_OwnerPersonId ON [request].[Requests] (OwnerPersonId);
    CREATE NONCLUSTERED INDEX IX_Requests_CustomerId ON [request].[Requests] (CustomerId);
    CREATE NONCLUSTERED INDEX IX_Requests_ProductId ON [request].[Requests] (ProductId);
    CREATE NONCLUSTERED INDEX IX_Requests_WorkPackageId ON [request].[Requests] (WorkPackageId);
    CREATE NONCLUSTERED INDEX IX_Requests_CreatedAt ON [request].[Requests] (CreatedAt);
END;

-- 2. [request].[RequestResolutions]
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('[request].[RequestResolutions]'))
BEGIN
    CREATE TABLE [request].[RequestResolutions] (
        ResolutionId UNIQUEIDENTIFIER NOT NULL,
        RequestId UNIQUEIDENTIFIER NOT NULL,
        Outcome NVARCHAR(50) NOT NULL,
        Summary NVARCHAR(MAX) NOT NULL,
        ResolvedByPersonId UNIQUEIDENTIFIER NOT NULL,
        ResolvedAt DATETIME2 NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT PK_RequestResolutions PRIMARY KEY CLUSTERED (ResolutionId),
        CONSTRAINT FK_RequestResolutions_Requests FOREIGN KEY (RequestId)
            REFERENCES [request].[Requests] (RequestId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_RequestResolutions_RequestId ON [request].[RequestResolutions] (RequestId);
END;

-- 3. [request].[RequestAssignments]
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('[request].[RequestAssignments]'))
BEGIN
    CREATE TABLE [request].[RequestAssignments] (
        AssignmentId UNIQUEIDENTIFIER NOT NULL,
        RequestId UNIQUEIDENTIFIER NOT NULL,
        OwnerPersonId UNIQUEIDENTIFIER NOT NULL,
        AssignedByPersonId UNIQUEIDENTIFIER NOT NULL,
        AssignedAt DATETIME2 NOT NULL,
        Note NVARCHAR(MAX) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT PK_RequestAssignments PRIMARY KEY CLUSTERED (AssignmentId),
        CONSTRAINT FK_RequestAssignments_Requests FOREIGN KEY (RequestId)
            REFERENCES [request].[Requests] (RequestId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_RequestAssignments_RequestId ON [request].[RequestAssignments] (RequestId);
    CREATE NONCLUSTERED INDEX IX_RequestAssignments_OwnerPersonId ON [request].[RequestAssignments] (OwnerPersonId);
END;

-- 4. [request].[RequestStateHistories] (Audit logging of every state change per Architecture §18)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('[request].[RequestStateHistories]'))
BEGIN
    CREATE TABLE [request].[RequestStateHistories] (
        StateHistoryId UNIQUEIDENTIFIER NOT NULL,
        RequestId UNIQUEIDENTIFIER NOT NULL,
        FromStatus NVARCHAR(50) NULL,
        ToStatus NVARCHAR(50) NOT NULL,
        ActorPersonId UNIQUEIDENTIFIER NOT NULL,
        Reason NVARCHAR(MAX) NULL,
        ChangedAt DATETIME2 NOT NULL,
        CONSTRAINT PK_RequestStateHistories PRIMARY KEY CLUSTERED (StateHistoryId),
        CONSTRAINT FK_RequestStateHistories_Requests FOREIGN KEY (RequestId)
            REFERENCES [request].[Requests] (RequestId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_RequestStateHistories_RequestId ON [request].[RequestStateHistories] (RequestId);
    CREATE NONCLUSTERED INDEX IX_RequestStateHistories_ActorPersonId ON [request].[RequestStateHistories] (ActorPersonId);
    CREATE NONCLUSTERED INDEX IX_RequestStateHistories_ChangedAt ON [request].[RequestStateHistories] (ChangedAt);
END;
