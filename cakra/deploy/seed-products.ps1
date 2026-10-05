#Requires -Version 5.1
<#
.SYNOPSIS
    CAKRA - Seed Product Catalog
.DESCRIPTION
    Inserts 8 reference products into product.Products.
    Idempotent: products whose Code already exist are skipped.
    Requires the Administrator person (admin@cakra.id) from seed-admin.ps1.
#>
param(
    [string]$Server       = "JUDE7",
    [string]$Database     = "CAKRA",
    [string]$SqlUser      = "cakraLogin",
    [string]$SqlPassword  = "cakra123!"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$sqlScript = @"
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
    s.Code,
    s.Name,
    s.[Description],
    s.OwnerPersonId,
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
"@

Write-Host "==> Seeding products into [$Database] on [$Server]..." -ForegroundColor Cyan

& sqlcmd -S $Server -d $Database -U $SqlUser -P $SqlPassword -Q $sqlScript
if ($LASTEXITCODE -ne 0) {
    throw "SQL execution failed with code $LASTEXITCODE"
}

Write-Host "==> Product seeding complete." -ForegroundColor Green
