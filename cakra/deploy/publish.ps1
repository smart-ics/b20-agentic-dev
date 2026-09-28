#Requires -Version 5.1
<#
.SYNOPSIS
    CAKRA - ICS Operational System Production Build & IIS Release Packaging Script
.DESCRIPTION
    Automates the unified production build and IIS release packaging pipeline per Architecture §19.10:
      1. Compiles the Vue 3 SPA via Vite (`npm run build`) in `src/frontend/Cakra.Web/`
         emitting production static assets into `src/frontend/Cakra.Web/dist/`.
      2. Ingests compiled frontend static assets from `src/frontend/Cakra.Web/dist/`
         into `src/backend/Cakra.Api/wwwroot/`.
      3. Publishes the ASP.NET Core 8.0 modular monolith host in Release mode:
         `dotnet publish src/backend/Cakra.Api/Cakra.Api.csproj -c Release -o ./publish`
      4. Generates and verifies the IIS In-Process `web.config` (`hostingModel="inprocess"`,
         `modules="AspNetCoreModuleV2"`, `processPath="dotnet"`, `arguments=".\Cakra.Api.dll"`,
         `stdoutLogEnabled="false"`, `ASPNETCORE_ENVIRONMENT="Production"`).
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "./publish"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$FrontendDir = Join-Path $RepoRoot "src/frontend/Cakra.Web"
$FrontendDistDir = Join-Path $FrontendDir "dist"
$BackendDir = Join-Path $RepoRoot "src/backend/Cakra.Api"
$BackendWwwrootDir = Join-Path $BackendDir "wwwroot"
$PublishDir = if ([System.IO.Path]::IsPathRooted($OutputDir)) { $OutputDir } else { Join-Path $RepoRoot $OutputDir }

Push-Location $RepoRoot
try {
    Write-Host "==> [1/4] Building Vue 3 SPA frontend (src/frontend/Cakra.Web)..." -ForegroundColor Cyan
    Push-Location $FrontendDir
    try {
        if (-not (Test-Path (Join-Path $FrontendDir "node_modules"))) {
            Write-Host "    Installing npm dependencies..."
            npm ci
            if ($LASTEXITCODE -ne 0) {
                throw "npm ci failed with exit code $LASTEXITCODE"
            }
        }

        npm run build
        if ($LASTEXITCODE -ne 0) {
            throw "npm run build failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }

    $DistIndexHtml = Join-Path $FrontendDistDir "index.html"
    if (-not (Test-Path $DistIndexHtml)) {
        throw "Frontend build did not produce expected entry point at '$DistIndexHtml'."
    }

    Write-Host "==> [2/4] Ingesting static assets from src/frontend/Cakra.Web/dist/ into src/backend/Cakra.Api/wwwroot/..." -ForegroundColor Cyan
    if (-not (Test-Path $BackendWwwrootDir)) {
        New-Item -ItemType Directory -Path $BackendWwwrootDir -Force | Out-Null
    }

    Get-ChildItem -Path $BackendWwwrootDir -Force |
        Where-Object { $_.Name -ne ".gitkeep" } |
        Remove-Item -Recurse -Force

    Copy-Item -Path (Join-Path $FrontendDistDir "*") -Destination $BackendWwwrootDir -Recurse -Force

    Write-Host "==> [3/4] Publishing Cakra.Api backend host in $Configuration mode to ./publish..." -ForegroundColor Cyan
    if (Test-Path $PublishDir) {
        Remove-Item -Path $PublishDir -Recurse -Force
    }

    dotnet publish src/backend/Cakra.Api/Cakra.Api.csproj -c $Configuration -o ./publish
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    $PublishWwwrootDir = Join-Path $PublishDir "wwwroot"
    if (-not (Test-Path $PublishWwwrootDir)) {
        New-Item -ItemType Directory -Path $PublishWwwrootDir -Force | Out-Null
    }
    Copy-Item -Path (Join-Path $FrontendDistDir "*") -Destination $PublishWwwrootDir -Recurse -Force

    Write-Host "==> [4/4] Generating IIS In-Process web.config in ./publish..." -ForegroundColor Cyan
    $WebConfigPath = Join-Path $PublishDir "web.config"
    $WebConfigContent = @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\Cakra.Api.dll"
                  stdoutLogEnabled="false"
                  stdoutLogFile=".\logs\stdout"
                  hostingModel="inprocess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
'@
    Set-Content -Path $WebConfigPath -Value $WebConfigContent -Encoding UTF8

    $LogsDir = Join-Path $PublishDir "logs"
    if (-not (Test-Path $LogsDir)) {
        New-Item -ItemType Directory -Path $LogsDir -Force | Out-Null
    }

    # Verify required artifacts in ./publish
    $RequiredArtifacts = @(
        (Join-Path $PublishDir "Cakra.Api.dll"),
        (Join-Path $PublishDir "web.config"),
        (Join-Path $PublishDir "appsettings.Production.json"),
        (Join-Path $PublishWwwrootDir "index.html")
    )

    foreach ($artifact in $RequiredArtifacts) {
        if (-not (Test-Path $artifact)) {
            throw "Required deployment artifact missing after publish: '$artifact'"
        }
    }

    Write-Host "==> CAKRA IIS release package published successfully to '$PublishDir'." -ForegroundColor Green
}
finally {
    Pop-Location
}
