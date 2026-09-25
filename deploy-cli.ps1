$ErrorActionPreference = "Stop"

$configPath = Join-Path $PSScriptRoot "config.ps1"

if (-not (Test-Path -LiteralPath $configPath)) {
    throw "Missing config.ps1. Copy config.example.ps1 to config.ps1 and configure DockerRegistry."
}

. $configPath

$image = "$DockerRegistry/twitter-x-backup-cli:latest"

docker build -f Dockerfile.Cli -t $image .

if ($LASTEXITCODE -ne 0) {
    throw "docker build failed with exit code $LASTEXITCODE"
}

docker push $image

if ($LASTEXITCODE -ne 0) {
    throw "docker push failed with exit code $LASTEXITCODE"
}
