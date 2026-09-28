-- =============================================================================
-- CAKRA — 0010_analytics_tables.sql
-- Slice: P7-S34 Analytics Module — Snapshot Tables & Snapshot Job
--
-- Purpose:
--   Creates the periodic materialized snapshot tables in the [analytics] schema
--   (Architecture §6, §7, §13, §16, §17, §19.3, §19.7, §20, §21):
--     1. analytics.DailyWorkloadSnapshots
--     2. analytics.MonthlyCustomerPerformanceSnapshots
--
-- Constraints & Rules:
--   - Zero cross-schema foreign keys: PersonId and CustomerId are stored as raw
--     UNIQUEIDENTIFIER values and resolved/validated through published query
--     interfaces (IOrganizationQueryService, ICustomerQueryService) per
--     Architecture §20.
--   - Enforces UNIQUE(SnapshotDate, PersonId) on DailyWorkloadSnapshots and
--     UNIQUE(YearMonth, CustomerId) on MonthlyCustomerPerformanceSnapshots per
--     Architecture §13.
--   - All table creation statements are idempotent (guarded by OBJECT_ID).
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'analytics')
    EXEC (N'CREATE SCHEMA [analytics] AUTHORIZATION [dbo];');

-- -----------------------------------------------------------------------------
-- 1. analytics.DailyWorkloadSnapshots
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[analytics].[DailyWorkloadSnapshots]', N'U') IS NULL
BEGIN
    CREATE TABLE [analytics].[DailyWorkloadSnapshots] (
        [SnapshotId] UNIQUEIDENTIFIER NOT NULL,
        [SnapshotDate] DATE NOT NULL,
        [PersonId] UNIQUEIDENTIFIER NOT NULL,
        [ActiveRequestsCount] INT NOT NULL CONSTRAINT [DF_DailyWorkloadSnapshots_ActiveRequestsCount] DEFAULT (0),
        [EscalatedRequestsCount] INT NOT NULL CONSTRAINT [DF_DailyWorkloadSnapshots_EscalatedRequestsCount] DEFAULT (0),
        [StalledRequestsCount] INT NOT NULL CONSTRAINT [DF_DailyWorkloadSnapshots_StalledRequestsCount] DEFAULT (0),
        [CompletedRequestsToday] INT NOT NULL CONSTRAINT [DF_DailyWorkloadSnapshots_CompletedRequestsToday] DEFAULT (0),
        [AvgAgeHours] DECIMAL(10, 2) NOT NULL CONSTRAINT [DF_DailyWorkloadSnapshots_AvgAgeHours] DEFAULT (0.00),
        [CapturedAt] DATETIME2 NOT NULL CONSTRAINT [DF_DailyWorkloadSnapshots_CapturedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_DailyWorkloadSnapshots] PRIMARY KEY CLUSTERED ([SnapshotId]),
        CONSTRAINT [UQ_DailyWorkloadSnapshots_SnapshotDate_PersonId] UNIQUE NONCLUSTERED ([SnapshotDate], [PersonId])
    );

    CREATE NONCLUSTERED INDEX [IX_DailyWorkloadSnapshots_SnapshotDate]
        ON [analytics].[DailyWorkloadSnapshots] ([SnapshotDate] DESC);

    CREATE NONCLUSTERED INDEX [IX_DailyWorkloadSnapshots_PersonId]
        ON [analytics].[DailyWorkloadSnapshots] ([PersonId], [SnapshotDate] DESC);
END;

-- -----------------------------------------------------------------------------
-- 2. analytics.MonthlyCustomerPerformanceSnapshots
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[analytics].[MonthlyCustomerPerformanceSnapshots]', N'U') IS NULL
BEGIN
    CREATE TABLE [analytics].[MonthlyCustomerPerformanceSnapshots] (
        [SnapshotId] UNIQUEIDENTIFIER NOT NULL,
        [YearMonth] VARCHAR(7) NOT NULL,
        [CustomerId] UNIQUEIDENTIFIER NOT NULL,
        [TotalRequests] INT NOT NULL CONSTRAINT [DF_MonthlyCustomerPerformanceSnapshots_TotalRequests] DEFAULT (0),
        [ResolvedRequestsCount] INT NOT NULL CONSTRAINT [DF_MonthlyCustomerPerformanceSnapshots_ResolvedRequestsCount] DEFAULT (0),
        [RejectedRequestsCount] INT NOT NULL CONSTRAINT [DF_MonthlyCustomerPerformanceSnapshots_RejectedRequestsCount] DEFAULT (0),
        [AvgResolutionHours] DECIMAL(10, 2) NOT NULL CONSTRAINT [DF_MonthlyCustomerPerformanceSnapshots_AvgResolutionHours] DEFAULT (0.00),
        [SlaMetCount] INT NOT NULL CONSTRAINT [DF_MonthlyCustomerPerformanceSnapshots_SlaMetCount] DEFAULT (0),
        [SlaBreachedCount] INT NOT NULL CONSTRAINT [DF_MonthlyCustomerPerformanceSnapshots_SlaBreachedCount] DEFAULT (0),
        [CapturedAt] DATETIME2 NOT NULL CONSTRAINT [DF_MonthlyCustomerPerformanceSnapshots_CapturedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_MonthlyCustomerPerformanceSnapshots] PRIMARY KEY CLUSTERED ([SnapshotId]),
        CONSTRAINT [UQ_MonthlyCustomerPerformanceSnapshots_YearMonth_CustomerId] UNIQUE NONCLUSTERED ([YearMonth], [CustomerId]),
        CONSTRAINT [CK_MonthlyCustomerPerformanceSnapshots_YearMonth] CHECK (
            LEN([YearMonth]) = 7 AND [YearMonth] LIKE '[0-9][0-9][0-9][0-9]-[0-1][0-9]'
        )
    );

    CREATE NONCLUSTERED INDEX [IX_MonthlyCustomerPerformanceSnapshots_YearMonth]
        ON [analytics].[MonthlyCustomerPerformanceSnapshots] ([YearMonth] DESC);

    CREATE NONCLUSTERED INDEX [IX_MonthlyCustomerPerformanceSnapshots_CustomerId]
        ON [analytics].[MonthlyCustomerPerformanceSnapshots] ([CustomerId], [YearMonth] DESC);
END;
