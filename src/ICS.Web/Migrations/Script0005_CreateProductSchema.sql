-- Migration: Script0005_CreateProductSchema.sql
-- Module: Product (Architecture §10, §16, §17, §20)

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'product')
BEGIN
    EXEC('CREATE SCHEMA [product]');
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('[product].[Products]'))
BEGIN
    CREATE TABLE [product].[Products] (
        ProductId UNIQUEIDENTIFIER NOT NULL,
        Code NVARCHAR(50) NOT NULL,
        Name NVARCHAR(150) NOT NULL,
        Description NVARCHAR(MAX) NULL,
        OwnerPersonId UNIQUEIDENTIFIER NOT NULL,
        Status NVARCHAR(20) NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT PK_Products PRIMARY KEY CLUSTERED (ProductId)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UQ_Products_Code ON [product].[Products] (Code);
    CREATE NONCLUSTERED INDEX IX_Products_OwnerPersonId ON [product].[Products] (OwnerPersonId);
    CREATE NONCLUSTERED INDEX IX_Products_Status ON [product].[Products] (Status);
END;
