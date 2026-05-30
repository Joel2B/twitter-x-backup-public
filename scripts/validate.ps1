Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
Set-Location $repoRoot

Write-Host "[validate] dotnet build"
dotnet build Backup.sln -v minimal

Write-Host "[validate] dotnet test"
dotnet test Backup.Tests/Backup.Tests.csproj -v minimal

Write-Host "[validate] namespace == folder (Backup.Infrastructure)"
$infraRoot = Join-Path $repoRoot "Backup.Infrastructure"
$mismatches = @()
$infraFiles = Get-ChildItem -Path $infraRoot -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" }

foreach ($file in $infraFiles) {
    $content = Get-Content -Raw -LiteralPath $file.FullName
    $match = [regex]::Match($content, "namespace\s+([A-Za-z0-9_.]+)")
    if (-not $match.Success) { continue }

    $actual = $match.Groups[1].Value
    $relativeDir = [System.IO.Path]::GetDirectoryName(
        $file.FullName.Substring($infraRoot.Length + 1)
    )

    $expected = "Backup.Infrastructure"
    if ($relativeDir) {
        $expected = "$expected.$($relativeDir -replace '\\', '.')"
    }

    if ($actual -ne $expected) {
        $mismatches += "$($file.FullName) | actual: $actual | expected: $expected"
    }
}

if ($mismatches.Count -gt 0) {
    Write-Host "[validate] namespace mismatches found:"
    $mismatches | ForEach-Object { Write-Host $_ }
    throw "Namespace/folder check failed."
}

Write-Host "[validate] eof newline check (*.cs)"
$eofErrors = @()
$allCs = Get-ChildItem -Path $repoRoot -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" }

foreach ($file in $allCs) {
    $content = [System.IO.File]::ReadAllText($file.FullName)
    if ($content.Length -eq 0) { continue }

    if ($content -notmatch "(\r?\n)$") {
        $eofErrors += "Missing final newline: $($file.FullName)"
        continue
    }

    if ($content -match "(\r?\n){2,}$") {
        $eofErrors += "Extra final newlines: $($file.FullName)"
    }
}

if ($eofErrors.Count -gt 0) {
    Write-Host "[validate] eof newline issues found:"
    $eofErrors | ForEach-Object { Write-Host $_ }
    throw "EOF newline check failed."
}

Write-Host "[validate] OK"
