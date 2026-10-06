-- =============================================================================
-- CAKRA — 0015_simplify_request_lifecycle.sql
-- Slice: P1-S01 Database Migration Script for Simplified Request Lifecycle
--
-- Purpose:
--   1. Migrates historical records in [request].[Requests] to simplified statuses:
--      - ESCALATED -> PAUSED
--      - EVALUATING, ACCEPTED -> ASSIGNED
--      - REJECTED -> CANCELLED
--   2. Migrates historical records in [request].[RequestResolutions]:
--      - REJECTED -> CANCELLED
--   3. Drops and recreates check constraint [CK_Requests_Status] on [request].[Requests]
--      to allow: 'CAPTURED', 'ASSIGNED', 'IN_PROGRESS', 'PAUSED', 'COMPLETED', 'CANCELLED'.
--   4. Drops and recreates check constraint [CK_RequestResolutions_Outcome] on
--      [request].[RequestResolutions] to allow: 'COMPLETED', 'RESOLVED', 'CANCELLED', 'REJECTED'.
--   5. Drops and recreates check constraints [CK_RequestAssignments_PreviousStatus] and
--      [CK_RequestAssignments_NewStatus] on [request].[RequestAssignments] to accommodate
--      the simplified lifecycle while preserving historical assignment audits.
--
-- Constraints & Rules (Architecture CR-016 §5, §8):
--   - Script is idempotent (guarded by sys.check_constraints and OBJECT_ID checks).
--   - Constraints are dropped before data updates to avoid constraint violation on existing records.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. [request].[Requests] - Status Constraint & Data Migration
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[request].[Requests]', N'U') IS NOT NULL
BEGIN
    -- Drop existing check constraint if present before migrating data
    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = N'CK_Requests_Status'
          AND parent_object_id = OBJECT_ID(N'[request].[Requests]')
    )
    BEGIN
        ALTER TABLE [request].[Requests] DROP CONSTRAINT [CK_Requests_Status];
    END;

    -- Migrate historical statuses to simplified lifecycle states
    UPDATE [request].[Requests]
    SET [Status] = 'PAUSED'
    WHERE [Status] = 'ESCALATED';

    UPDATE [request].[Requests]
    SET [Status] = 'ASSIGNED'
    WHERE [Status] IN ('EVALUATING', 'ACCEPTED');

    UPDATE [request].[Requests]
    SET [Status] = 'CANCELLED'
    WHERE [Status] = 'REJECTED';

    -- Recreate check constraint enforcing the 6 simplified request statuses
    ALTER TABLE [request].[Requests]
        WITH CHECK ADD CONSTRAINT [CK_Requests_Status]
        CHECK ([Status] IN ('CAPTURED', 'ASSIGNED', 'IN_PROGRESS', 'PAUSED', 'COMPLETED', 'CANCELLED'));
END;

-- -----------------------------------------------------------------------------
-- 2. [request].[RequestResolutions] - Outcome Constraint & Data Migration
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[request].[RequestResolutions]', N'U') IS NOT NULL
BEGIN
    -- Drop existing check constraint if present before migrating data
    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = N'CK_RequestResolutions_Outcome'
          AND parent_object_id = OBJECT_ID(N'[request].[RequestResolutions]')
    )
    BEGIN
        ALTER TABLE [request].[RequestResolutions] DROP CONSTRAINT [CK_RequestResolutions_Outcome];
    END;

    -- Migrate historical outcomes: REJECTED -> CANCELLED
    UPDATE [request].[RequestResolutions]
    SET [Outcome] = 'CANCELLED'
    WHERE [Outcome] = 'REJECTED';

    -- Recreate check constraint enforcing resolution outcomes
    ALTER TABLE [request].[RequestResolutions]
        WITH CHECK ADD CONSTRAINT [CK_RequestResolutions_Outcome]
        CHECK ([Outcome] IN ('COMPLETED', 'RESOLVED', 'CANCELLED', 'REJECTED'));
END;

-- -----------------------------------------------------------------------------
-- 3. [request].[RequestAssignments] - Status Constraints Update
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[request].[RequestAssignments]', N'U') IS NOT NULL
BEGIN
    -- Drop existing CK_RequestAssignments_PreviousStatus check constraint if present
    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = N'CK_RequestAssignments_PreviousStatus'
          AND parent_object_id = OBJECT_ID(N'[request].[RequestAssignments]')
    )
    BEGIN
        ALTER TABLE [request].[RequestAssignments] DROP CONSTRAINT [CK_RequestAssignments_PreviousStatus];
    END;

    -- Recreate CK_RequestAssignments_PreviousStatus check constraint
    ALTER TABLE [request].[RequestAssignments]
        WITH CHECK ADD CONSTRAINT [CK_RequestAssignments_PreviousStatus]
        CHECK (
            [PreviousStatus] IS NULL OR
            [PreviousStatus] IN ('CAPTURED', 'ASSIGNED', 'IN_PROGRESS', 'PAUSED', 'COMPLETED', 'CANCELLED', 'EVALUATING', 'ACCEPTED', 'REJECTED', 'ESCALATED')
        );

    -- Drop existing CK_RequestAssignments_NewStatus check constraint if present
    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = N'CK_RequestAssignments_NewStatus'
          AND parent_object_id = OBJECT_ID(N'[request].[RequestAssignments]')
    )
    BEGIN
        ALTER TABLE [request].[RequestAssignments] DROP CONSTRAINT [CK_RequestAssignments_NewStatus];
    END;

    -- Recreate CK_RequestAssignments_NewStatus check constraint
    ALTER TABLE [request].[RequestAssignments]
        WITH CHECK ADD CONSTRAINT [CK_RequestAssignments_NewStatus]
        CHECK (
            [NewStatus] IN ('CAPTURED', 'ASSIGNED', 'IN_PROGRESS', 'PAUSED', 'COMPLETED', 'CANCELLED', 'EVALUATING', 'ACCEPTED', 'REJECTED', 'ESCALATED')
        );
END;
