#Requires -Version 5.1
<#
.SYNOPSIS
    CAKRA - ICS Operational System IIS Application Pool & Website Provisioning Script
.DESCRIPTION
    Provisions the authoritative Windows Server IIS hosting environment per Architecture §19.10:
      1. Dedicated IIS Application Pool (`CakraAppPool`):
         - `No Managed Code` (`managedRuntimeVersion = ""`)
         - 64-bit worker process `w3wp.exe` (`enable32BitAppOnWin64 = $false`)
         - Automatic start (`startMode = "AlwaysRunning"`, `autoStart = $true`)
         - Idle timeout disabled (`idleTimeout = 00:00:00`) and periodic process recycling (`29:00:00`)
      2. IIS Website (`Cakra`):
         - Physical path mapped to the published artifact directory (`./publish`)
         - Assigned to `CakraAppPool`
         - HTTPS binding on port 443 (`protocol = "https"`, port `443`)
      3. Optional DbUp SQL Server migration execution (`dotnet Cakra.Api.dll --migrate`) prior to starting traffic.
#>
[CmdletBinding()]
param(
    [string]$AppPoolName = "CakraAppPool",
    [string]$SiteName = "Cakra",
    [string]$PhysicalPath = "",
    [int]$HttpsPort = 443,
    [string]$HostName = "",
    [string]$CertificateThumbprint = "",
    [string]$ConnectionString = "",
    [switch]$RunMigrations = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($PhysicalPath)) {
    $PhysicalPath = Join-Path $RepoRoot "publish"
}

if (-not (Test-Path $PhysicalPath)) {
    throw "Published directory '$PhysicalPath' does not exist. Run deploy/publish.ps1 first."
}

$PhysicalPath = (Resolve-Path $PhysicalPath).Path

Write-Host "==> [1/4] Importing IIS WebAdministration module..." -ForegroundColor Cyan
Import-Module WebAdministration -ErrorAction Stop

Write-Host "==> [2/4] Provisioning dedicated IIS Application Pool '$AppPoolName' (No Managed Code, 64-bit, AlwaysRunning)..." -ForegroundColor Cyan
$AppPoolPath = "IIS:\AppPools\$AppPoolName"
if (-not (Test-Path $AppPoolPath)) {
    New-WebAppPool -Name $AppPoolName | Out-Null
}

# Target No Managed Code (ASP.NET Core Module V2 runs in-process inside w3wp.exe)
Set-ItemProperty -Path $AppPoolPath -Name "managedRuntimeVersion" -Value ""
# Enforce 64-bit worker process (enable32BitAppOnWin64 = $false)
Set-ItemProperty -Path $AppPoolPath -Name "enable32BitAppOnWin64" -Value $false
# Automatic start and AlwaysRunning mode
Set-ItemProperty -Path $AppPoolPath -Name "startMode" -Value "AlwaysRunning"
Set-ItemProperty -Path $AppPoolPath -Name "autoStart" -Value $true
# Process lifecycle, idle timeout, and recycling supervision (Architecture §19.10)
Set-ItemProperty -Path $AppPoolPath -Name "processModel.idleTimeout" -Value ([TimeSpan]::Zero)
Set-ItemProperty -Path $AppPoolPath -Name "recycling.periodicRestart.time" -Value ([TimeSpan]::FromHours(29))
Set-ItemProperty -Path $AppPoolPath -Name "failure.rapidFailProtection" -Value $true

if (-not [string]::IsNullOrWhiteSpace($ConnectionString)) {
    [System.Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", $ConnectionString, "Machine")
    $env:ConnectionStrings__DefaultConnection = $ConnectionString
}

if ($RunMigrations) {
    Write-Host "==> [3/4] Executing idempotent DbUp SQL Server migrations (dotnet Cakra.Api.dll --migrate)..." -ForegroundColor Cyan
    $ApiDllPath = Join-Path $PhysicalPath "Cakra.Api.dll"
    & dotnet $ApiDllPath --migrate
    if ($LASTEXITCODE -ne 0) {
        throw "Database migration failed with exit code $LASTEXITCODE"
    }
}

Write-Host "==> [4/4] Provisioning IIS Website '$SiteName' bound to HTTPS port $HttpsPort -> '$PhysicalPath'..." -ForegroundColor Cyan
$SitePath = "IIS:\Sites\$SiteName"
if (Test-Path $SitePath) {
    Set-ItemProperty -Path $SitePath -Name "physicalPath" -Value $PhysicalPath
    Set-ItemProperty -Path $SitePath -Name "applicationPool" -Value $AppPoolName
}
else {
    New-Website -Name $SiteName `
                -PhysicalPath $PhysicalPath `
                -ApplicationPool $AppPoolName `
                -Port $HttpsPort `
                -HostHeader $HostName `
                -Ssl | Out-Null
}

$ExistingHttpsBinding = Get-WebBinding -Name $SiteName -Protocol "https" -Port $HttpsPort -ErrorAction SilentlyContinue
if (-not $ExistingHttpsBinding) {
    New-WebBinding -Name $SiteName -Protocol "https" -Port $HttpsPort -HostHeader $HostName -SslFlags 0 | Out-Null
}

if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    $Binding = Get-WebBinding -Name $SiteName -Protocol "https" -Port $HttpsPort
    $Binding.AddSslCertificate($CertificateThumbprint, "My")
}

Start-WebAppPool -Name $AppPoolName
Start-Website -Name $SiteName

Write-Host "==> IIS Site '$SiteName' (AppPool '$AppPoolName', HTTPS :$HttpsPort) provisioned and running." -ForegroundColor Green
