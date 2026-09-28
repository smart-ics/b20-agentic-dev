-- ICS Baseline Migration: Schema Segregation per Module
-- Architecture §17 & §19.3
-- Creates the 8 bounded context schemas in strict logical dependency order.
-- Idempotent: checks for existence before creation.

-- 1. Identity schema (IAM, UserAccounts, UserSessions)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'identity')
BEGIN
    EXEC('CREATE SCHEMA [identity]');
END;

-- 2. Organization schema (Persons, Teams, Roles, Responsibilities, Assignments)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'organization')
BEGIN
    EXEC('CREATE SCHEMA [organization]');
END;

-- 3. Customer schema (Customers, CustomerContacts)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'customer')
BEGIN
    EXEC('CREATE SCHEMA [customer]');
END;

-- 4. Product schema (Products)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'product')
BEGIN
    EXEC('CREATE SCHEMA [product]');
END;

-- 5. WorkPackage schema (WorkPackages, WorkPackageRequests)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'workpackage')
BEGIN
    EXEC('CREATE SCHEMA [workpackage]');
END;

-- 6. Request schema (Requests, RequestResolutions)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'request')
BEGIN
    EXEC('CREATE SCHEMA [request]');
END;

-- 7. Post schema (Posts, Comments, Reactions, PostReferences, FeedItems)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'post')
BEGIN
    EXEC('CREATE SCHEMA [post]');
END;

-- 8. Analytics schema (DailyWorkloadSnapshots, MonthlyCustomerPerformanceSnapshots)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'analytics')
BEGIN
    EXEC('CREATE SCHEMA [analytics]');
END;
