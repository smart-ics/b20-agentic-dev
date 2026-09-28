#Requires -Version 5.1
<#
.SYNOPSIS
    CAKRA - Deploy to Local IIS (Option A: Single Site)
.DESCRIPTION
    Copies publish output to D:\IISHosting\cakra\api and provisions
    IIS App Pool + Site with HTTPS on port 443.
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

Write-Host "==> [1/5] Copying publish output to $DeployPath ..." -ForegroundColor Cyan
if (-not (Test-Path $DeployPath)) {
    New-Item -ItemType Directory -Path $DeployPath -Force | Out-Null
}
Copy-Item -Path "$PublishPath\*" -Destination $DeployPath -Recurse -Force -ErrorAction Stop
Write-Host "    Done." -ForegroundColor Green

Write-Host "==> [2/5] Importing WebAdministration module..." -ForegroundColor Cyan
Import-Module WebAdministration -ErrorAction Stop
Write-Host "    Done." -ForegroundColor Green

Write-Host "==> [3/5] Creating Application Pool '$AppPoolName'..." -ForegroundColor Cyan
$AppPoolPath = "IIS:\AppPools\$AppPoolName"
if (Test-Path $AppPoolPath) {
    Remove-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
}
New-WebAppPool -Name $AppPoolPath -Force | Out-Null
Set-ItemProperty -Path $AppPoolPath -Name "managedRuntimeVersion" -Value ""
Set-ItemProperty -Path $AppPoolPath -Name "enable32BitAppOnWin64" -Value $false
Set-ItemProperty -Path $AppPoolPath -Name "startMode" -Value "AlwaysRunning"
Set-ItemProperty -Path $AppPoolPath -Name "autoStart" -Value $true
Set-ItemProperty -Path $AppPoolPath -Name "processModel.idleTimeout" -Value ([TimeSpan]::Zero)
Set-ItemProperty -Path $AppPoolPath -Name "recycling.periodicRestart.time" -Value ([TimeSpan]::FromHours(29))
Write-Host "    Done." -ForegroundColor Green

Write-Host "==> [4/5] Creating IIS Website '$SiteName' (HTTPS :$HttpsPort)..." -ForegroundColor Cyan
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

Write-Host "==> [5/5] Binding SSL certificate..." -ForegroundColor Cyan
$ExistingBinding = Get-WebBinding -Name $SiteName -Protocol "https" -Port $HttpsPort -ErrorAction SilentlyContinue
if ($ExistingBinding) {
    $ExistingBinding.AddSslCertificate($CertificateThumbprint, "My")
} else {
    New-WebBinding -Name $SiteName -Protocol "https" -Port $HttpsPort -SslFlags 0 | Out-Null
    $Binding = Get-WebBinding -Name $SiteName -Protocol "https" -Port $HttpsPort
    $Binding.AddSslCertificate($CertificateThumbprint, "My")
}
Write-Host "    Done." -ForegroundColor Green

Write-Host ""
Write-Host "==> CAKRA deployed successfully!" -ForegroundColor Green
Write-Host "    Site:       https://localhost" -ForegroundColor White
Write-Host "    API:        https://localhost/api/v1" -ForegroundColor White
Write-Host "    Swagger:    https://localhost/swagger" -ForegroundColor White
Write-Host "    Health:     https://localhost/health/live" -ForegroundColor White
Write-Host "    App Pool:   $AppPoolName (No Managed Code, 64-bit)" -ForegroundColor White
Write-Host "    Path:       $DeployPath" -ForegroundColor White