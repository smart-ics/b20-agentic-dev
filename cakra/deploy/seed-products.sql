-- ============================================================
-- CAKRA - Seed Product Catalog
-- ============================================================
-- Inserts 8 reference products into product.Products.
-- Idempotent: products whose Code already exist are skipped.
-- OwnerPersonId is resolved by looking up the Administrator person
-- inserted by seed-admin.ps1 (Email = 'admin@cakra.id').
--
-- Run with:
--   sqlcmd -S JUDE7 -d CAKRA -U cakraLogin -P "cakra123!" -i seed-products.sql
-- ============================================================

BEGIN TRANSACTION;

DECLARE @OwnerPersonId UNIQUEIDENTIFIER;
SELECT @OwnerPersonId = Id
FROM organization.Persons
WHERE Email = 'admin@cakra.id';

IF @OwnerPersonId IS NULL
BEGIN
    RAISERROR('Owner person (admin@cakra.id) not found. Run seed-admin.ps1 first.', 16, 1);
    ROLLBACK;
    RETURN;
END

;WITH SourceProducts AS (
    SELECT 'MHW'   AS Code, 'MyHospital Web'              AS [Name], 'Web-based hospital management system'                    AS [Description], @OwnerPersonId AS OwnerPersonId
    UNION ALL SELECT 'FO',    'MyHospital Desktop',          'Desktop client for hospital operations',                  @OwnerPersonId
    UNION ALL SELECT 'EMR',   'Electronic Medical Record',   'Digital patient medical records system',                  @OwnerPersonId
    UNION ALL SELECT 'HIDOK', 'Hidok Online Booking',        'Online appointment booking platform',                    @OwnerPersonId
    UNION ALL SELECT 'NERS',  'NERS Asuhan Keperawatan',     'Nursing care documentation and electronic medical records', @OwnerPersonId
    UNION ALL SELECT 'OFTA',  'Ofta Office Automation',      'Office automation suite for ICS operations',              @OwnerPersonId
    UNION ALL SELECT 'PNL',   'PenaEl Electronic Sign',      'Electronic document signing and verification solution',    @OwnerPersonId
    UNION ALL SELECT 'HRD',   'Human Resource Development', 'HR management and employee development platform',          @OwnerPersonId
)
INSERT INTO product.Products (Id, Code, Name, Description, OwnerPersonId, Status, CreatedAt, UpdatedAt)
SELECT
    NEWID(),
    Code,
    Name,
    Description,
    OwnerPersonId,
    'ACTIVE',
    SYSUTCDATETIME(),
    NULL
FROM SourceProducts s
WHERE NOT EXISTS (
    SELECT 1 FROM product.Products p WHERE p.Code = s.Code
);

IF @@ROWCOUNT > 0
    PRINT 'Seeded products into product.Products.';
ELSE
    PRINT 'No new products to seed — all codes already exist.';

COMMIT TRANSACTION;
