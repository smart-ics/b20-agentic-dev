<#
.SYNOPSIS
  Installs the b20-agentic-dev SDLC framework (agents, skills, manifesto) into OpenCode on Windows.

.DESCRIPTION
  Copies   development\agents\*.md  ->  <target>\agents\
           development\skills\*\    ->  <target>\skills\
           development\manifesto\   ->  <target>\b20-manifesto\   (reference copy only)

  Global target : %USERPROFILE%\.config\opencode   (or %XDG_CONFIG_HOME%\opencode if set)
  Project target: <ProjectPath>\.opencode

  - Files are written as UTF-8 WITHOUT BOM and with LF line endings, so YAML frontmatter
    stays valid even if git checked the repo out with CRLF.
  - Anything that would be overwritten or removed is backed up first to
    <target>\.b20-backup\<timestamp>\
  - Idempotent: re-run after every `git pull` to update. Unchanged files are left alone.
  - Place this script in the repo root; content is read from development\
    (development\agents\, development\skills\, development\manifesto\).

.PARAMETER Scope        Global (default) or Project.
.PARAMETER ProjectPath  Project root, used with -Scope Project. Default: current directory.
.PARAMETER DryRun       Show what would change; write nothing.
.PARAMETER Uninstall    Remove the installed agents/skills/manifesto (backed up first).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\install-ics-agentic.ps1 -DryRun

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\install-ics-agentic.ps1

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\install-ics-agentic.ps1 -Scope Project -ProjectPath D:\Work\MyProject
#>
[CmdletBinding()]
param(
    [ValidateSet('Global', 'Project')][string]$Scope = 'Global',
    [string]$ProjectPath = (Get-Location).Path,
    [switch]$DryRun,
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

# ---------------------------------------------------------------- source
$src = Join-Path $PSScriptRoot 'development'
foreach ($d in 'agents', 'skills') {
    if (-not (Test-Path -LiteralPath (Join-Path $src $d))) {
        throw "Run this script from the b20-agentic-dev repo root: folder 'development\$d' not found in $PSScriptRoot"
    }
}
$agentFiles = @(Get-ChildItem -LiteralPath (Join-Path $src 'agents') -Filter *.md -File)
$skillDirs  = @(Get-ChildItem -LiteralPath (Join-Path $src 'skills') -Directory |
                Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'SKILL.md') })

# ---------------------------------------------------------------- target
if ($Scope -eq 'Global') {
    $cfgBase = if ($env:XDG_CONFIG_HOME) { $env:XDG_CONFIG_HOME } else { Join-Path $env:USERPROFILE '.config' }
    $target  = Join-Path $cfgBase 'opencode'
}
else {
    $target = Join-Path (Resolve-Path -LiteralPath $ProjectPath).Path '.opencode'
}
$stamp      = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupRoot = Join-Path $target ".b20-backup\$stamp"
$stats      = @{ new = 0; updated = 0; unchanged = 0; removed = 0 }

# ---------------------------------------------------------------- helpers
function Record([string]$state, [string]$rel) {
    $script:stats[$state]++
    if ($state -ne 'unchanged') { Write-Host ("  {0,-9} {1}" -f $state, $rel) }
}

function Ensure-Dir([string]$dir) {
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
}

function Backup-Item([string]$path, [string]$rel) {
    $dest = Join-Path $backupRoot $rel
    Ensure-Dir (Split-Path $dest -Parent)
    Copy-Item -LiteralPath $path -Destination $dest -Recurse -Force
}

function Read-Normalized([string]$path) {
    ([System.IO.File]::ReadAllText($path)) -replace "`r`n", "`n"
}

# returns 'new' | 'updated' | 'unchanged'
function Install-File([string]$from, [string]$to, [string]$rel) {
    $isText = [System.IO.Path]::GetExtension($from).ToLower() -in '.md', '.txt', '.json', '.yaml', '.yml'
    $exists = Test-Path -LiteralPath $to
    if ($isText) {
        $content = Read-Normalized $from
        if ($exists -and ([System.IO.File]::ReadAllText($to) -ceq $content)) { return 'unchanged' }
    }
    else {
        if ($exists -and ((Get-FileHash -LiteralPath $from).Hash -eq (Get-FileHash -LiteralPath $to).Hash)) { return 'unchanged' }
    }
    if (-not $DryRun) {
        if ($exists) { Backup-Item $to $rel }
        Ensure-Dir (Split-Path $to -Parent)
        if ($isText) { [System.IO.File]::WriteAllText($to, $content, $utf8NoBom) }
        else { Copy-Item -LiteralPath $from -Destination $to -Force }
    }
    if ($exists) { return 'updated' } else { return 'new' }
}

# Mirrors a folder; files that no longer exist in the source are backed up and removed.
function Install-Tree([string]$fromDir, [string]$toDir, [string]$relBase) {
    $fromDir = $fromDir.TrimEnd('\', '/')
    $keep = @{}
    foreach ($f in Get-ChildItem -LiteralPath $fromDir -Recurse -File) {
        $sub = $f.FullName.Substring($fromDir.Length).TrimStart('\', '/')
        $keep[$sub.ToLower()] = $true
        $rel = Join-Path $relBase $sub
        Record (Install-File $f.FullName (Join-Path $toDir $sub) $rel) $rel
    }
    if (Test-Path -LiteralPath $toDir) {
        $toRoot = $toDir.TrimEnd('\', '/')
        foreach ($f in Get-ChildItem -LiteralPath $toDir -Recurse -File) {
            $sub = $f.FullName.Substring($toRoot.Length).TrimStart('\', '/')
            if (-not $keep.ContainsKey($sub.ToLower())) {
                $rel = Join-Path $relBase $sub
                if (-not $DryRun) {
                    Backup-Item $f.FullName $rel
                    Remove-Item -LiteralPath $f.FullName -Force
                }
                Record 'removed' $rel
            }
        }
    }
}

function Remove-Owned([string]$path, [string]$rel) {
    if (Test-Path -LiteralPath $path) {
        if (-not $DryRun) {
            Backup-Item $path $rel
            Remove-Item -LiteralPath $path -Recurse -Force
        }
        Record 'removed' $rel
    }
}

# ---------------------------------------------------------------- run
Write-Host ''
Write-Host "b20-agentic-dev -> OpenCode [$Scope]"
Write-Host "Source: $src"
Write-Host "Target: $target"
if ($DryRun) { Write-Host 'DRY RUN - nothing will be written.' }
Write-Host ''

if ($Uninstall) {
    foreach ($f in $agentFiles) { Remove-Owned (Join-Path $target "agents\$($f.Name)") "agents\$($f.Name)" }
    foreach ($d in $skillDirs)  { Remove-Owned (Join-Path $target "skills\$($d.Name)") "skills\$($d.Name)" }
    Remove-Owned (Join-Path $target 'b20-manifesto') 'b20-manifesto'
}
else {
    foreach ($f in $agentFiles) {
        $rel = "agents\$($f.Name)"
        Record (Install-File $f.FullName (Join-Path $target $rel) $rel) $rel
    }
    foreach ($d in $skillDirs) {
        Install-Tree $d.FullName (Join-Path $target "skills\$($d.Name)") "skills\$($d.Name)"
    }
    if (Test-Path -LiteralPath (Join-Path $src 'manifesto')) {
        Install-Tree (Join-Path $src 'manifesto') (Join-Path $target 'b20-manifesto') 'b20-manifesto'
    }
}

Write-Host ''
Write-Host ("Summary: {0} new, {1} updated, {2} unchanged, {3} removed" -f $stats.new, $stats.updated, $stats.unchanged, $stats.removed)
if (-not $DryRun -and (Test-Path -LiteralPath $backupRoot)) { Write-Host "Backups: $backupRoot" }

# ---------------------------------------------------------------- verify (global install only)
if (-not $DryRun -and -not $Uninstall) {
    Write-Host ''
    Write-Host 'Restart OpenCode to pick up changes, then verify with:  opencode agent list'
    if ($Scope -eq 'Global' -and (Get-Command opencode -ErrorAction SilentlyContinue)) {
        $names = ($agentFiles | ForEach-Object { $_.BaseName }) -join '|'
        $prev = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try { $listing = (& opencode agent list 2>&1 | Out-String) -split "`r?`n" } catch { $listing = @() }
        $ErrorActionPreference = $prev
        $found = @($listing | Where-Object { $_ -match "\b($names)\b" })
        if ($found.Count -gt 0) {
            Write-Host 'OpenCode currently lists:'
            $found | ForEach-Object { Write-Host "  $_" }
        }
        else {
            Write-Host "Note: 'opencode agent list' did not show the agents yet - restart OpenCode and re-check."
        }
    }
}
Write-Host ''