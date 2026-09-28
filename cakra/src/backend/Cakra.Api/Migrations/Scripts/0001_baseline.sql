-- =============================================================================
-- CAKRA — 0001_baseline.sql
-- Slice: P1-S03 Database Infrastructure & Migration Framework
--
-- Purpose:
--   Establishes the per-module SQL Server schemas defined in Architecture §17
--   (Schema Segregation). This baseline is intentionally EMPTY of business
--   tables — only the eight module schemas are provisioned.
--
-- Schemas (Architecture §17):
--   identity, organization, customer, product, workpackage, request, post, analytics
--
-- Idempotency:
--   Every statement is guarded by a sys.schemas existence check, so the script
--   is safe to re-run. DbUp additionally journals applied scripts in
--   dbo.SchemaVersions and applies each script at most once.
--
-- Notes:
--   CREATE SCHEMA must be the only statement in a batch, hence the EXEC(...)
--   dynamic-SQL wrapper. No cross-schema foreign keys or business objects are
--   created here (Architecture §20).
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'identity')
    EXEC (N'CREATE SCHEMA [identity] AUTHORIZATION [dbo];');

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'organization')
    EXEC (N'CREATE SCHEMA [organization] AUTHORIZATION [dbo];');

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'customer')
    EXEC (N'CREATE SCHEMA [customer] AUTHORIZATION [dbo];');

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'product')
    EXEC (N'CREATE SCHEMA [product] AUTHORIZATION [dbo];');

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'workpackage')
    EXEC (N'CREATE SCHEMA [workpackage] AUTHORIZATION [dbo];');

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'request')
    EXEC (N'CREATE SCHEMA [request] AUTHORIZATION [dbo];');

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'post')
    EXEC (N'CREATE SCHEMA [post] AUTHORIZATION [dbo];');

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'analytics')
    EXEC (N'CREATE SCHEMA [analytics] AUTHORIZATION [dbo];');
