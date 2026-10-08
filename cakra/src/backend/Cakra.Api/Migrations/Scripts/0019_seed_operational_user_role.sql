-- =============================================================================
-- CAKRA — 0019_seed_operational_user_role.sql
-- Slice: P1-S01 Database Migration for Operational User Master Role
--
-- Purpose:
--   Seeds standard master role 'Operational User' in [organization].[Roles]
--   alongside Administrator, Management, Programmer, and Implementator.
--
-- Constraints & Rules (Architecture CR-027 §3, §4, §8):
--   - Script is idempotent (guarded by NOT EXISTS check on [Name]).
--   - Standard operational user participating in operational workflows.
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM [organization].[Roles] WHERE [Name] = 'Operational User')
BEGIN
    INSERT INTO [organization].[Roles] ([Id], [Name], [Description], [CreatedAt])
    VALUES (NEWID(), 'Operational User', 'Standard operational user participating in operational workflows', SYSUTCDATETIME());
END;
