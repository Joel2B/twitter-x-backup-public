Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
Set-Location $repoRoot

$pythonScript = Join-Path $PSScriptRoot "format.py"

if ($args -contains "--check") {
    Write-Host "[format] dotnet format style (check)"
    dotnet format style . --verify-no-changes --severity info
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "[format] csharpier + custom rules (check)"
    python $pythonScript --check
    exit $LASTEXITCODE
}

Write-Host "[format] dotnet format style"
dotnet format style . --severity info
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "[format] csharpier + custom rules"
python $pythonScript
exit $LASTEXITCODE
