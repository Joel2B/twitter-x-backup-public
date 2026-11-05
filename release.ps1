param(
    [Parameter(Position = 0)]
    [ValidateSet("cli", "api", "all")]
    [string]$Target = "all"
)

$ErrorActionPreference = "Stop"

$dockerRegistry = $env:DOCKER_REGISTRY

if ([string]::IsNullOrWhiteSpace($dockerRegistry)) {
    $envPath = Join-Path $PSScriptRoot ".env"

    if (Test-Path -LiteralPath $envPath) {
        foreach ($line in Get-Content -LiteralPath $envPath) {
            $trimmedLine = $line.Trim()

            if ($trimmedLine.Length -eq 0 -or $trimmedLine.StartsWith("#")) {
                continue
            }

            $parts = $trimmedLine.Split("=", 2)

            if ($parts.Count -eq 2 -and $parts[0].Trim() -eq "DOCKER_REGISTRY") {
                $dockerRegistry = $parts[1].Trim().Trim('"').Trim("'")
                break
            }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($dockerRegistry)) {
    throw "Missing DOCKER_REGISTRY. Set it in .env or in the process environment."
}

$Target = $Target.ToLowerInvariant()
$dockerRegistry = $dockerRegistry.TrimEnd("/")
$targets = if ($Target -eq "all") { @("cli", "api") } else { @($Target) }

foreach ($currentTarget in $targets) {
    $image = "$dockerRegistry/twitter-x-backup-${currentTarget}:latest"

    docker build -f (Join-Path $PSScriptRoot "Dockerfile") --target $currentTarget -t $image $PSScriptRoot

    if ($LASTEXITCODE -ne 0) {
        throw "docker build failed for '$currentTarget' with exit code $LASTEXITCODE"
    }

    docker push $image

    if ($LASTEXITCODE -ne 0) {
        throw "docker push failed for '$currentTarget' with exit code $LASTEXITCODE"
    }
}
