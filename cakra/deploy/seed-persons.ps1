#Requires -Version 5.1
<#
.SYNOPSIS
    CAKRA - Seed Organizational Persons
.DESCRIPTION
    Inserts 26 reference persons into organization.Persons.
    Idempotent: persons whose Email already exists are skipped.
#>
param(
    [string]$Server     = "JUDE7",
    [string]$Database   = "CAKRA",
    [string]$SqlUser    = "cakraLogin",
    [string]$SqlPassword = "cakra123!"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$sqlScript = @"
BEGIN TRANSACTION;

;WITH SourcePersons AS (
    SELECT 'Wahyu'        AS FirstName, 'Widanarto'        AS LastName, 'wahyu.widanarto@cakra.id'        AS Email
    UNION ALL SELECT 'Roso',          'Wahono',            'roso.wahono@cakra.id'
    UNION ALL SELECT 'Arif',          'Hidayat',           'arif.hidayat@cakra.id'
    UNION ALL SELECT '-',             'Sulistiyarno',      'sulistiyarno@cakra.id'
    UNION ALL SELECT 'Rizal Aditya',  'Pratama',           'rizal.aditya.pratama@cakra.id'
    UNION ALL SELECT 'Arie',          'Haryanto',          'arie.haryanto@cakra.id'
    UNION ALL SELECT 'Fikri',         'Haikal',            'fikri.haikal@cakra.id'
    UNION ALL SELECT 'Ernawan',       'Sukoco',            'ernawan.sukoco@cakra.id'
    UNION ALL SELECT '-',             'Kriswanto',         'kriswanto@cakra.id'
    UNION ALL SELECT 'Teguh',         'Pramono',           'teguh.pramono@cakra.id'
    UNION ALL SELECT 'Rahmat',        'Suryantoro',        'rahmat.suryantoro@cakra.id'
    UNION ALL SELECT 'Arif',          'Mahmudi',           'arif.mahmudi@cakra.id'
    UNION ALL SELECT 'Saharudin',     'Aslam',             'saharudin.aslam@cakra.id'
    UNION ALL SELECT 'Wahyu',         'Ariwibowo',         'wahyu.ariwibowo@cakra.id'
    UNION ALL SELECT 'Julia',         'Suryaningrum',      'julia.suryaningrum@cakra.id'
    UNION ALL SELECT 'Pandu',         'Antareksa',         'pandu.antareksa@cakra.id'
    UNION ALL SELECT 'Teguh',         'Syahrian',          'teguh.syahrian@cakra.id'
    UNION ALL SELECT 'Ismail',        'Sunni',             'ismail.sunni@cakra.id'
    UNION ALL SELECT 'Heri',          'Abriyanto',         'heri.abriyanto@cakra.id'
    UNION ALL SELECT 'Lalu',          'Zainudin',          'lalu.zainudin@cakra.id'
    UNION ALL SELECT 'Saiful',        'Hidayat',           'saiful.hidayat@cakra.id'
    UNION ALL SELECT 'Heri',          'Santoso',           'heri.santoso@cakra.id'
    UNION ALL SELECT 'Bobby',         'Suharman',          'bobby.suharman@cakra.id'
    UNION ALL SELECT 'Tri',           'Handoyo',           'tri.handoyo@cakra.id'
    UNION ALL SELECT 'Fadli Aji',     'Triono',            'fadli.aji.triono@cakra.id'
    UNION ALL SELECT '-',             'Sunardianto',       'sunardianto@cakra.id'
)
INSERT INTO organization.Persons (Id, FirstName, LastName, Email, Status, CreatedAt, UpdatedAt)
SELECT
    NEWID(),
    s.FirstName,
    s.LastName,
    s.Email,
    'ACTIVE',
    SYSUTCDATETIME(),
    SYSUTCDATETIME()
FROM SourcePersons s
WHERE NOT EXISTS (
    SELECT 1 FROM organization.Persons p WHERE p.Email = s.Email
);

IF @@ROWCOUNT > 0
    PRINT 'Seeded persons into organization.Persons.';
ELSE
    PRINT 'No new persons to seed — all emails already exist.';

COMMIT TRANSACTION;
"@

Write-Host "==> Seeding persons into [$Database] on [$Server]..." -ForegroundColor Cyan

& sqlcmd -S $Server -d $Database -U $SqlUser -P $SqlPassword -Q $sqlScript
if ($LASTEXITCODE -ne 0) {
    throw "SQL execution failed with code $LASTEXITCODE"
}

Write-Host "==> Person seeding complete." -ForegroundColor Green
