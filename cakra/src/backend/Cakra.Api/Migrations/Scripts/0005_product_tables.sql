-- =============================================================================
-- CAKRA — 0005_product_tables.sql
-- Slice: P3-S15 Product Module — Domain, Application Services & Persistence
--
-- Purpose:
--   Creates the authoritative Product catalog table in the [product] schema:
--     - product.Products
--
-- Constraints & Rules (Architecture §10, §17, §19.3, §20; Product Domain §5, §8, §9):
--   - Zero cross-schema foreign keys: OwnerPersonId is stored as a raw
--     UNIQUEIDENTIFIER and validated via IOrganizationQueryService.
--   - Status on Products defaults to 'ACTIVE' and is constrained to
--     ('ACTIVE', 'INACTIVE').
--   - Code is unique across all Product records.
--   - All table creation statements are idempotent (guarded by OBJECT_ID).
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'product')
    EXEC (N'CREATE SCHEMA [product] AUTHORIZATION [dbo];');

-- -----------------------------------------------------------------------------
-- 1. product.Products
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[product].[Products]', N'U') IS NULL
BEGIN
    CREATE TABLE [product].[Products] (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Code] NVARCHAR(50) NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(1000) NULL,
        [OwnerPersonId] UNIQUEIDENTIFIER NOT NULL,
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT [DF_Products_Status] DEFAULT ('ACTIVE'),
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Products_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [CK_Products_Status] CHECK ([Status] IN ('ACTIVE', 'INACTIVE'))
    );

    CREATE UNIQUE NONCLUSTERED INDEX [UQ_Products_Code] ON [product].[Products] ([Code]);
    CREATE NONCLUSTERED INDEX [IX_Products_Status] ON [product].[Products] ([Status]);
    CREATE NONCLUSTERED INDEX [IX_Products_OwnerPersonId] ON [product].[Products] ([OwnerPersonId]);
END;
