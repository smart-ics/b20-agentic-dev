#Requires -Version 5.1
<#
.SYNOPSIS
    CAKRA - Deploy to Local IIS using appcmd.exe
.DESCRIPTION
    Copies publish output to D:\IISHosting\cakra\api and provisions
    IIS App Pool + Site with HTTPS on port 443 using appcmd.exe.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$DeployPath = "D:\IISHosting\cakra\api"
$PublishPath = "D:\Project.Aktif\b20-agentic-dev\cakra\publish"
$AppPoolName = "CakraAppPool"
$SiteName = "Cakra"
$HttpsPort = 443
$CertificateThumbprint = "1B0F251CBA6A631A77E31D501B230C77264255C6"
$AppCmd = "$env:WINDIR\System32\inetsrv\appcmd.exe"

Write-Host "==> [1/5] Copying publish output to $DeployPath ..." -ForegroundColor Cyan
if (-not (Test-Path $DeployPath)) {
    New-Item -ItemType Directory -Path $DeployPath -Force | Out-Null
}
Copy-Item -Path "$PublishPath\*" -Destination $DeployPath -Recurse -Force -ErrorAction Stop
Write-Host "    Done." -ForegroundColor Green

Write-Host "==> [2/5] Creating Application Pool '$AppPoolName'..." -ForegroundColor Cyan
# Remove existing app pool if present
$existingPool = & $AppCmd list apppool $AppPoolName 2>$null
if ($LASTEXITCODE -eq 0) {
    & $AppCmd delete apppool $AppPoolName /stop
}
& $AppCmd add apppool $AppPoolName `
    /managedRuntimeVersion:"" `
    /managedPipelineMode:Integrated `
    /enable32BitAppOnWin64:$false `
    /startMode:AlwaysRunning
Write-Host "    Done." -ForegroundColor Green

Write-Host "==> [3/5] Creating IIS Website '$SiteName' (HTTPS :$HttpsPort)..." -ForegroundColor Cyan
# Remove existing site if present
$existingSite = & $AppCmd list site $SiteName 2>$null
if ($LASTEXITCODE -eq 0) {
    & $AppCmd delete site $SiteName
}
& $AppCmd add site $SiteName `
    /physicalPath:$DeployPath `
    /appPool:$AppPoolName `
    /bindings:https/*:$HttpsPort:localhost
Write-Host "    Done." -ForegroundColor Green

Write-Host "==> [4/5] Binding SSL certificate..." -ForegroundColor Cyan
& $AppCmd set site $SiteName `
    /bindings.[0].certificateThumbprint:$CertificateThumbprint
Write-Host "    Done." -ForegroundColor Green

Write-Host "==> [5/5] Starting site and app pool..." -ForegroundColor Cyan
& $AppCmd start apppool $AppPoolName
& $AppCmd start site $SiteName
Write-Host "    Done." -ForegroundColor Green

Write-Host ""
Write-Host "==> CAKRA deployed successfully!" -ForegroundColor Green
Write-Host "    Site:       https://localhost" -ForegroundColor White
Write-Host "    API:        https://localhost/api/v1" -ForegroundColor White
Write-Host "    Swagger:    https://localhost/swagger" -ForegroundColor White
Write-Host "    Health:     https://localhost/health/live" -ForegroundColor White
Write-Host "    App Pool:   $AppPoolName (No Managed Code, 64-bit)" -ForegroundColor White
Write-Host "    Path:       $DeployPath" -ForegroundColor White