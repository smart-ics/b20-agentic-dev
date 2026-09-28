#Requires -Version 5.1
<#
.SYNOPSIS
    CAKRA - IIS Deployment Script (elevated)
.DESCRIPTION
    Provisions IIS App Pool and Site for Cakra using WebAdministration module.
    MUST be run in an elevated PowerShell session.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$DeployPath = "D:\IISHosting\cakra\api"
$AppPoolName = "CakraAppPool"
$SiteName = "Cakra"
$HttpsPort = 443
$CertificateThumbprint = "1B0F251CBA6A631A77E31D501B230C77264255C6"

# Verify elevation
$principal = New-Object System.Security.Principal.WindowsPrincipal([System.Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "This script must be run as Administrator."
}

Write-Host "==> Importing WebAdministration module..." -ForegroundColor Cyan
Import-Module WebAdministration -ErrorAction Stop
Write-Host "    Done." -ForegroundColor Green

Write-Host "==> Creating Application Pool '$AppPoolName'..." -ForegroundColor Cyan
$AppPoolPath = "IIS:\AppPools\$AppPoolName"
if (Test-Path $AppPoolPath) {
    Remove-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
}
New-WebAppPool -Name $AppPoolName -Force | Out-Null
Set-ItemProperty -Path $AppPoolPath -Name "managedRuntimeVersion" -Value ""
Set-ItemProperty -Path $AppPoolPath -Name "enable32BitAppOnWin64" -Value $false
Set-ItemProperty -Path $AppPoolPath -Name "startMode" -Value "AlwaysRunning"
Set-ItemProperty -Path $AppPoolPath -Name "autoStart" -Value $true
Set-ItemProperty -Path $AppPoolPath -Name "processModel.idleTimeout" -Value ([TimeSpan]::Zero)
Set-ItemProperty -Path $AppPoolPath -Name "recycling.periodicRestart.time" -Value ([TimeSpan]::FromHours(29))
Write-Host "    Done." -ForegroundColor Green

Write-Host "==> Creating IIS Website '$SiteName' (HTTPS :$HttpsPort)..." -ForegroundColor Cyan
$SitePath = "IIS:\Sites\$SiteName"
if (Test-Path $SitePath) {
    Remove-Website -Name $SiteName -ErrorAction SilentlyContinue
}
New-Website -Name $SiteName `
            -PhysicalPath $DeployPath `
            -ApplicationPool $AppPoolName `
            -Port $HttpsPort `
            -Ssl | Out-Null
Write-Host "    Done." -ForegroundColor Green

Write-Host "==> Binding SSL certificate..." -ForegroundColor Cyan
$Binding = Get-WebBinding -Name $SiteName -Protocol "https" -Port $HttpsPort
$Binding.AddSslCertificate($CertificateThumbprint, "My")
Write-Host "    Done." -ForegroundColor Green

Write-Host "==> Starting site..." -ForegroundColor Cyan
Start-WebAppPool -Name $AppPoolName
Start-Website -Name $SiteName
Write-Host "    Done." -ForegroundColor Green

Write-Host ""
Write-Host "==> CAKRA IIS deployment complete!" -ForegroundColor Green
Write-Host "    Site:       https://localhost" -ForegroundColor White
Write-Host "    API:        https://localhost/api/v1" -ForegroundColor White
Write-Host "    Swagger:    https://localhost/swagger" -ForegroundColor White
Write-Host "    Health:     https://localhost/health/live" -ForegroundColor White
Write-Host "    App Pool:   $AppPoolName (No Managed Code, 64-bit)" -ForegroundColor White
Write-Host "    Path:       $DeployPath" -ForegroundColor White