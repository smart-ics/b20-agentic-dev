#Requires -Version 5.1
<#
.SYNOPSIS
    CAKRA - Seed Initial Roles and Administrator User Account
.DESCRIPTION
    Populates master roles and creates an initial Administrator user
    linked to an active organization.Persons record with full Administrator
    and Management permissions.
#>
param(
    [string]$Server = "JUDE7",
    [string]$Database = "CAKRA",
    [string]$SqlUser = "cakraLogin",
    [string]$SqlPassword = "cakra123!",
    [string]$AdminUsername = "admin",
    [string]$AdminEmail = "admin@cakra.id",
    [string]$AdminPassword = "Admin123!"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "==> Generating ASP.NET Core Identity password hash..." -ForegroundColor Cyan

# Locate Microsoft.Extensions.Identity.Core.dll from .NET shared runtime
$runtimeDll = Get-ChildItem -Path "$env:ProgramFiles\dotnet\shared\Microsoft.AspNetCore.App" -Recurse -Filter "Microsoft.Extensions.Identity.Core.dll" | Select-Object -Last 1
if (-not $runtimeDll) {
    throw "Microsoft.Extensions.Identity.Core.dll not found."
}

$asm = [System.Reflection.Assembly]::LoadFrom($runtimeDll.FullName)
$hasherType = $asm.GetType("Microsoft.AspNetCore.Identity.PasswordHasher``1").MakeGenericType([string])
$hasher = [System.Activator]::CreateInstance($hasherType, @($null))
$passwordHash = $hasher.HashPassword($AdminUsername, $AdminPassword)

Write-Host "    Hash generated successfully." -ForegroundColor Green
Write-Host "==> Seeding database [$Database] on [$Server]..." -ForegroundColor Cyan

$sqlScript = @"
BEGIN TRANSACTION;

-- 1. Standard Organization Roles
IF NOT EXISTS (SELECT 1 FROM organization.Roles WHERE Name = 'Administrator')
    INSERT INTO organization.Roles (Id, Name, Description, CreatedAt)
    VALUES (NEWID(), 'Administrator', 'System Administrator with full access', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM organization.Roles WHERE Name = 'Management')
    INSERT INTO organization.Roles (Id, Name, Description, CreatedAt)
    VALUES (NEWID(), 'Management', 'Management oversight and decision authority', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM organization.Roles WHERE Name = 'Programmer')
    INSERT INTO organization.Roles (Id, Name, Description, CreatedAt)
    VALUES (NEWID(), 'Programmer', 'Software development and technical implementation', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM organization.Roles WHERE Name = 'Implementator')
    INSERT INTO organization.Roles (Id, Name, Description, CreatedAt)
    VALUES (NEWID(), 'Implementator', 'Field deployment and client implementation', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM organization.Roles WHERE Name = 'Operational User')
    INSERT INTO organization.Roles (Id, Name, Description, CreatedAt)
    VALUES (NEWID(), 'Operational User', 'Standard operational user participating in operational workflows', SYSUTCDATETIME());

-- 2. Organization Person
DECLARE @PersonId UNIQUEIDENTIFIER;
SELECT @PersonId = Id FROM organization.Persons WHERE Email = '$AdminEmail';

IF @PersonId IS NULL
BEGIN
    SET @PersonId = NEWID();
    INSERT INTO organization.Persons (Id, FirstName, LastName, Email, Status, CreatedAt)
    VALUES (@PersonId, 'System', 'Administrator', '$AdminEmail', 'ACTIVE', SYSUTCDATETIME());
END

-- Assign Administrator & Management roles
INSERT INTO organization.RoleAssignments (PersonId, RoleId, AssignedAt)
SELECT @PersonId, Id, SYSUTCDATETIME()
FROM organization.Roles
WHERE Name IN ('Administrator', 'Management')
  AND Id NOT IN (SELECT RoleId FROM organization.RoleAssignments WHERE PersonId = @PersonId);

-- 3. Identity UserAccount
DECLARE @UserId UNIQUEIDENTIFIER;
SELECT @UserId = UserId FROM [identity].UserAccounts WHERE Username = '$AdminUsername' OR Email = '$AdminEmail';

IF @UserId IS NULL
BEGIN
    SET @UserId = NEWID();
    INSERT INTO [identity].UserAccounts (UserId, PersonId, Username, Email, PasswordHash, Status, FailedLoginAttempts, CreatedAt)
    VALUES (@UserId, @PersonId, '$AdminUsername', '$AdminEmail', '$passwordHash', 'ACTIVE', 0, SYSUTCDATETIME());
    PRINT 'Created user account: $AdminUsername';
END
ELSE
BEGIN
    UPDATE [identity].UserAccounts
    SET PasswordHash = '$passwordHash', Status = 'ACTIVE', FailedLoginAttempts = 0, UpdatedAt = SYSUTCDATETIME()
    WHERE UserId = @UserId;
    PRINT 'Updated existing user account: $AdminUsername';
END

COMMIT TRANSACTION;
"@

& sqlcmd -S $Server -d $Database -U $SqlUser -P $SqlPassword -Q $sqlScript
if ($LASTEXITCODE -ne 0) {
    throw "SQL execution failed with code $LASTEXITCODE"
}

Write-Host ""
Write-Host "==> Administrator account ready:" -ForegroundColor Green
Write-Host "    Username: $AdminUsername" -ForegroundColor White
Write-Host "    Password: $AdminPassword" -ForegroundColor White
Write-Host "    Email:    $AdminEmail" -ForegroundColor White
Write-Host "    Roles:    Administrator, Management" -ForegroundColor White
