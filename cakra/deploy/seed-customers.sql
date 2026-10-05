-- ============================================================
-- CAKRA - Seed Customer Master
-- ============================================================
-- Inserts 17 reference customers (rumah sakit) into customer.Customers.
-- Idempotent: customers whose CustomerCode already exist are skipped.
-- All seeded as ACTIVE with HasActiveMaintenanceContract = 1.
--
-- Run with:
--   sqlcmd -S JUDE7 -d CAKRA -U cakraLogin -P "cakra123!" -i seed-customers.sql
-- ============================================================

BEGIN TRANSACTION;

;WITH SourceCustomers AS (
    SELECT 'RSKRA'  AS CustomerCode, 'RSUD Karangasem'             AS CustomerName
    UNION ALL SELECT 'RISA',   'RS Risa Mataram'
    UNION ALL SELECT 'RSWGY',  'RSUD Wangaya'
    UNION ALL SELECT 'RSMIDR', 'RS Mata Bali Mandara'
    UNION ALL SELECT 'RSNGR',  'RSU Negara'
    UNION ALL SELECT 'RSJTS',  'RS Jati Sampurna'
    UNION ALL SELECT 'RSVAB',  'RS Vania'
    UNION ALL SELECT 'RSMEKA', 'RS Mekar Sari'
    UNION ALL SELECT 'RSUKI',  'RS UKI'
    UNION ALL SELECT 'RSHSD',  'RS Husada'
    UNION ALL SELECT 'RSSAT',  'RS Satya Negara'
    UNION ALL SELECT 'RSPKL',  'RS Budi Rahayu'
    UNION ALL SELECT 'RSWPD',  'RSUD Purwodadi'
    UNION ALL SELECT 'RSHPL',  'RS Happy Land'
    UNION ALL SELECT 'RSSEC',  'RS Mata Solo'
    UNION ALL SELECT 'RSAMR',  'RS Amira'
    UNION ALL SELECT 'RSBTA',  'RSUD Ibnu Sutowo'
    UNION ALL SELECT 'RSMIT',  'RS Mitra Husada'
)
INSERT INTO customer.Customers (Id, CustomerCode, CustomerName, Status, HasActiveMaintenanceContract, CreatedAt, UpdatedAt)
SELECT
    NEWID(),
    s.CustomerCode,
    s.CustomerName,
    'ACTIVE',
    1,
    SYSUTCDATETIME(),
    NULL
FROM SourceCustomers s
WHERE NOT EXISTS (
    SELECT 1 FROM customer.Customers c WHERE c.CustomerCode = s.CustomerCode
);

IF @@ROWCOUNT > 0
    PRINT 'Seeded customers into customer.Customers.';
ELSE
    PRINT 'No new customers to seed — all codes already exist.';

COMMIT TRANSACTION;