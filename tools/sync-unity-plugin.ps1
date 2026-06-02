<#
.SYNOPSIS
    Synchronises the canonical Unity package source
    (Packages/com.unity-mcp.editor/) into the distribution mirror
    (unity-mcp/unity-plugin/).

.DESCRIPTION
    Run this script after editing files under Packages/com.unity-mcp.editor/
    to keep unity-mcp/unity-plugin/ in sync.

    Excludes .meta files and any folders not part of the package layout.

.EXAMPLE
    pwsh tools/sync-unity-plugin.ps1
#>

$ErrorActionPreference = "Stop"

$repoRoot   = Split-Path -Parent $PSScriptRoot
$source     = Join-Path $repoRoot "Packages\com.unity-mcp.editor"
$target     = Join-Path $repoRoot "unity-mcp\unity-plugin"

if (-not (Test-Path $source)) {
    Write-Error "Source not found: $source"
    exit 1
}

Write-Host "Syncing $source -> $target" -ForegroundColor Cyan

# Use robocopy for reliable mirror (Windows built-in).
# /MIR  = mirror (delete extras in target)
# /XD   = exclude directories
# /XF   = exclude files
# /NJH  = no job header
# /NJS  = no job summary
# /NP   = no progress
# /NDL  = no directory list
$robocopyArgs = @(
    $source,
    $target,
    "/MIR",
    "/NJH", "/NJS", "/NP", "/NDL",
    "/XF", "*.meta"
)

$result = & robocopy @robocopyArgs

# robocopy exit codes: 0 = no change, 1 = copied, 2 = extra, 3 = copied+extra
# anything >= 8 is an error
if ($LASTEXITCODE -ge 8) {
    Write-Error "robocopy failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "Done. Exit code: $LASTEXITCODE" -ForegroundColor Green
