-- Migration: Script0004_CreateCustomerSchema.sql
-- Module: Customer (Architecture §6, §7, §16, §17, §20)

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'customer')
BEGIN
    EXEC('CREATE SCHEMA [customer]');
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('[customer].[Customers]'))
BEGIN
    CREATE TABLE [customer].[Customers] (
        CustomerId UNIQUEIDENTIFIER NOT NULL,
        CustomerCode NVARCHAR(50) NOT NULL,
        CustomerName NVARCHAR(150) NOT NULL,
        Status NVARCHAR(20) NOT NULL,
        HasActiveMaintenanceContract BIT NOT NULL CONSTRAINT DF_Customers_HasActiveMaintenanceContract DEFAULT (0),
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT PK_Customers PRIMARY KEY CLUSTERED (CustomerId)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UQ_Customers_CustomerCode ON [customer].[Customers] (CustomerCode);
    CREATE NONCLUSTERED INDEX IX_Customers_Status ON [customer].[Customers] (Status);
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('[customer].[CustomerContacts]'))
BEGIN
    CREATE TABLE [customer].[CustomerContacts] (
        ContactId UNIQUEIDENTIFIER NOT NULL,
        CustomerId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(100) NOT NULL,
        Position NVARCHAR(100) NULL,
        PhoneNumber NVARCHAR(50) NULL,
        Email NVARCHAR(150) NULL,
        Status NVARCHAR(20) NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT PK_CustomerContacts PRIMARY KEY CLUSTERED (ContactId),
        CONSTRAINT FK_CustomerContacts_Customers FOREIGN KEY (CustomerId) REFERENCES [customer].[Customers](CustomerId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_CustomerContacts_CustomerId ON [customer].[CustomerContacts] (CustomerId);
    CREATE NONCLUSTERED INDEX IX_CustomerContacts_Status ON [customer].[CustomerContacts] (Status);
END;
