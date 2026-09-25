<#
.SYNOPSIS
  Installs the b20-agentic-dev SDLC framework (agents, skills, manifesto) into Google Antigravity on Windows.

.DESCRIPTION
  Installs custom skills, agent personas, and rules into Antigravity:
    - development\skills\*\   -> <target>\skills\<name>\   (Full skills with templates and assets)
    - development\agents\*.md -> <target>\skills\<name>\SKILL.md (Registered as slash-command persona skills)
    - development\agents\*.md -> <target>\rules\b20-agents\ (Role boundaries & authority rules)
    - development\manifesto\  -> <target>\rules\b20-manifesto\ (SDLC Principles, Workflow, Knowledge Lifecycle)
    - development\manifesto\  -> <target>\b20-manifesto\ (Reference documentation)

  Global target (default): %USERPROFILE%\.gemini\config
  Project target:          <ProjectPath>\.agents

  - Files are written as UTF-8 WITHOUT BOM and normalized with LF line endings.
  - Automatically adds 'name' frontmatter to agent files if missing, ensuring Antigravity registers them as skills.
  - Existing files that differ are backed up to <target>\.b20-backup\<timestamp>\ before overwrite.
  - Idempotent: re-run after git pull to update without re-writing identical files.
  - Supports -DryRun and -Uninstall.

.PARAMETER Scope        Global (default) or Project.
.PARAMETER ProjectPath  Project root, used with -Scope Project. Default: current directory.
.PARAMETER DryRun       Show what would change; write nothing.
.PARAMETER Uninstall    Remove the installed agents/skills/manifesto (backed up first).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\install-antigravity.ps1 -DryRun

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\install-antigravity.ps1

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\install-antigravity.ps1 -Scope Project
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
    $target = Join-Path $env:USERPROFILE '.gemini\config'
}
else {
    $target = Join-Path (Resolve-Path -LiteralPath $ProjectPath).Path '.agents'
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
function Install-Content([string]$content, [string]$to, [string]$rel) {
    $content = $content -replace "`r`n", "`n"
    $exists = Test-Path -LiteralPath $to
    if ($exists -and ([System.IO.File]::ReadAllText($to) -ceq $content)) { return 'unchanged' }

    if (-not $DryRun) {
        if ($exists) { Backup-Item $to $rel }
        Ensure-Dir (Split-Path $to -Parent)
        [System.IO.File]::WriteAllText($to, $content, $utf8NoBom)
    }
    if ($exists) { return 'updated' } else { return 'new' }
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

# Converts an agent markdown definition into an Antigravity-compatible skill (ensures 'name' exists in frontmatter)
function Format-AgentAsSkill([string]$filePath, [string]$agentName) {
    $raw = Read-Normalized $filePath
    if ($raw -match "(?s)^---\s*\n(.*?)\n---(.*)$") {
        $frontmatter = $matches[1]
        $body = $matches[2]
        if ($frontmatter -notmatch "(?m)^name\s*:") {
            $frontmatter = "name: $agentName`n" + $frontmatter
        }
        return "---`n$frontmatter`n---$body"
    }
    else {
        return "---`nname: $agentName`ndescription: $agentName agent persona`n---`n`n$raw"
    }
}

# ---------------------------------------------------------------- run
Write-Host ''
Write-Host "b20-agentic-dev -> Google Antigravity [$Scope]" -ForegroundColor Cyan
Write-Host "Source: $src"
Write-Host "Target: $target"
if ($DryRun) { Write-Host 'DRY RUN - nothing will be written.' -ForegroundColor Yellow }
Write-Host ''

if ($Uninstall) {
    # Uninstall skills
    foreach ($d in $skillDirs)  { Remove-Owned (Join-Path $target "skills\$($d.Name)") "skills\$($d.Name)" }
    foreach ($f in $agentFiles) { Remove-Owned (Join-Path $target "skills\$($f.BaseName)") "skills\$($f.BaseName)" }
    
    # Uninstall rules & manifesto
    Remove-Owned (Join-Path $target 'rules\b20-manifesto') 'rules\b20-manifesto'
    Remove-Owned (Join-Path $target 'rules\b20-agents') 'rules\b20-agents'
    Remove-Owned (Join-Path $target 'b20-manifesto') 'b20-manifesto'
}
else {
    Write-Host '[1/4] Installing custom skills...' -ForegroundColor Green
    foreach ($d in $skillDirs) {
        Install-Tree $d.FullName (Join-Path $target "skills\$($d.Name)") "skills\$($d.Name)"
    }

    Write-Host '[2/4] Installing agent personas as slash-command skills...' -ForegroundColor Green
    foreach ($f in $agentFiles) {
        $agentName = $f.BaseName
        $skillRel = "skills\$agentName\SKILL.md"
        $formattedSkill = Format-AgentAsSkill $f.FullName $agentName
        Record (Install-Content $formattedSkill (Join-Path $target $skillRel) $skillRel) $skillRel
    }

    Write-Host '[3/4] Installing agent roles into rules...' -ForegroundColor Green
    foreach ($f in $agentFiles) {
        $ruleRel = "rules\b20-agents\$($f.Name)"
        Record (Install-File $f.FullName (Join-Path $target $ruleRel) $ruleRel) $ruleRel
    }

    Write-Host '[4/4] Installing SDLC manifesto (rules & reference)...' -ForegroundColor Green
    if (Test-Path -LiteralPath (Join-Path $src 'manifesto')) {
        Install-Tree (Join-Path $src 'manifesto') (Join-Path $target 'rules\b20-manifesto') 'rules\b20-manifesto'
        Install-Tree (Join-Path $src 'manifesto') (Join-Path $target 'b20-manifesto') 'b20-manifesto'
    }
}

Write-Host ''
Write-Host ("Summary: {0} new, {1} updated, {2} unchanged, {3} removed" -f $stats.new, $stats.updated, $stats.unchanged, $stats.removed) -ForegroundColor Cyan
if (-not $DryRun -and (Test-Path -LiteralPath $backupRoot)) { Write-Host "Backups saved to: $backupRoot" -ForegroundColor DarkGray }

# ---------------------------------------------------------------- instructions
if (-not $DryRun -and -not $Uninstall) {
    Write-Host ''
    Write-Host 'Installation completed successfully!' -ForegroundColor Green
    Write-Host 'Antigravity automatically discovers changes in your ~/.gemini/config directory.'
    Write-Host ''
    Write-Host 'How to use in Antigravity Desktop:' -ForegroundColor Yellow
    Write-Host '  1. Skills & Customizations Sidebar: Check the left sidebar to see all installed skills & rules.'
    Write-Host '  2. Slash Commands in Chat: Type "/" to invoke any role or workflow:'
    Write-Host '     - Personas : /ica-analyst, /ica-architect, /ica-developer, /ica-tester, /ica-reviewer, /ica-issuer, /ica-implementer'
    Write-Host '     - Workflows: /ics-domain-creation, /ics-feature-creation, /ics-feasibility-assessment,'
    Write-Host '                  /ics-architecture-creation, /ics-implementation-plan, /ics-review, etc.'
}
Write-Host ''
