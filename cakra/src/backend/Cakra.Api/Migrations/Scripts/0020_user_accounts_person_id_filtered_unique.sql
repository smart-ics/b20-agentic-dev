-- =============================================================================
-- CAKRA — 0020_user_accounts_person_id_filtered_unique.sql
-- Slice: Bugfix for User Account Self-Registration (CR-009 / FEAT-USR-002)
--
-- Purpose:
--   Replaces the unconditional unique constraint on [identity].[UserAccounts](PersonId)
--   with a filtered unique index excluding Guid.Empty ('00000000-0000-0000-0000-000000000000').
--   This allows multiple self-registered pending accounts with PersonId = Guid.Empty
--   while maintaining strict 1-to-1 uniqueness for all authoritative Person records.
--
-- Constraints & Rules:
--   - Script is fully idempotent.
-- =============================================================================

IF EXISTS (
    SELECT 1 
    FROM sys.key_constraints 
    WHERE name = N'UQ_UserAccounts_PersonId' 
      AND parent_object_id = OBJECT_ID(N'[identity].[UserAccounts]')
)
BEGIN
    ALTER TABLE [identity].[UserAccounts]
    DROP CONSTRAINT [UQ_UserAccounts_PersonId];
END
ELSE IF EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = N'UQ_UserAccounts_PersonId' 
      AND object_id = OBJECT_ID(N'[identity].[UserAccounts]')
      AND has_filter = 0
)
BEGIN
    DROP INDEX [UQ_UserAccounts_PersonId] ON [identity].[UserAccounts];
END;

IF NOT EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = N'UQ_UserAccounts_PersonId' 
      AND object_id = OBJECT_ID(N'[identity].[UserAccounts]')
      AND has_filter = 1
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_UserAccounts_PersonId]
    ON [identity].[UserAccounts] ([PersonId] ASC)
    WHERE [PersonId] <> '00000000-0000-0000-0000-000000000000';
END;
