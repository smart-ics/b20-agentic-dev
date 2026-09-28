-- =============================================================================
-- CAKRA — 0004_customer_tables.sql
-- Slice: P3-S14 Customer Module — Domain, Application Services & Persistence
--
-- Purpose:
--   Creates authoritative Customer master tables in the [customer] schema:
--     - customer.Customers
--     - customer.CustomerContacts
--
-- Constraints & Rules (Architecture §17, §19.3, §20; Customer Domain §5, §6, §7):
--   - Foreign key exists exclusively between customer.CustomerContacts and
--     customer.Customers inside the [customer] schema. Zero cross-schema FKs.
--   - Status on Customers and CustomerContacts defaults to 'ACTIVE' and is
--     constrained to ('ACTIVE', 'INACTIVE').
--   - CustomerCode is unique across all Customer records.
--   - All table creation statements are idempotent (guarded by OBJECT_ID).
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'customer')
    EXEC (N'CREATE SCHEMA [customer] AUTHORIZATION [dbo];');

-- -----------------------------------------------------------------------------
-- 1. customer.Customers
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[customer].[Customers]', N'U') IS NULL
BEGIN
    CREATE TABLE [customer].[Customers] (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [CustomerCode] NVARCHAR(50) NOT NULL,
        [CustomerName] NVARCHAR(200) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT [DF_Customers_Status] DEFAULT ('ACTIVE'),
        [HasActiveMaintenanceContract] BIT NOT NULL CONSTRAINT [DF_Customers_HasActiveMaintenanceContract] DEFAULT (0),
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Customers_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_Customers] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [CK_Customers_Status] CHECK ([Status] IN ('ACTIVE', 'INACTIVE'))
    );

    CREATE UNIQUE NONCLUSTERED INDEX [UQ_Customers_CustomerCode] ON [customer].[Customers] ([CustomerCode]);
    CREATE NONCLUSTERED INDEX [IX_Customers_Status] ON [customer].[Customers] ([Status]);
END;

-- -----------------------------------------------------------------------------
-- 2. customer.CustomerContacts
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'[customer].[CustomerContacts]', N'U') IS NULL
BEGIN
    CREATE TABLE [customer].[CustomerContacts] (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [CustomerId] UNIQUEIDENTIFIER NOT NULL,
        [Name] NVARCHAR(150) NOT NULL,
        [Position] NVARCHAR(100) NULL,
        [PhoneNumber] NVARCHAR(50) NULL,
        [Email] NVARCHAR(255) NULL,
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT [DF_CustomerContacts_Status] DEFAULT ('ACTIVE'),
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_CustomerContacts_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_CustomerContacts] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_CustomerContacts_Customers] FOREIGN KEY ([CustomerId]) REFERENCES [customer].[Customers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [CK_CustomerContacts_Status] CHECK ([Status] IN ('ACTIVE', 'INACTIVE'))
    );

    CREATE NONCLUSTERED INDEX [IX_CustomerContacts_CustomerId] ON [customer].[CustomerContacts] ([CustomerId]);
    CREATE NONCLUSTERED INDEX [IX_CustomerContacts_Status] ON [customer].[CustomerContacts] ([Status]);
END;
