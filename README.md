> This project is for personal use and may not work for everyone.

## twitter-x-backup

C# tool to back up X/Twitter content (posts and media) using GraphQL endpoints, with data stored locally or on mounted storage based on configuration.

## Status

This project is under active personal development. Structure and configuration format may change.

## Requirements

- .NET SDK 10 (`net10.0`)
- Docker and Docker Compose (optional)
- Valid split configuration files in `config/` (or an explicit config directory)

## Configuration

1. Use files in `App/Config.example/` as a starting point.
2. Copy them to your runtime config directory (recommended: `./config/`) and replace all fields marked as `{REPLACE_THIS}`.
3. Adjust data/debug paths and download settings for your environment.

Configuration directory resolution order:
- `BackupConfigurationOptions.ConfigDirectory` (when provided in host wiring)
- env var `BACKUP__CONFIG__DIRECTORY`
- `./config`
- `./App/Config`
- `./App/Config.example`
- `<app-base>/config`

Note: runtime config files are intended for local/deployment use and should not be committed.

Media cache backend:
- `Data.Media[].CacheBackend.Type` defaults to `json` (or omit `CacheBackend` entirely).
- `redis` and `postgres` are reserved for future implementation and currently fail fast on startup.

## Run Locally

```bash
dotnet restore
dotnet run --project Backup.Cli
```

Run API locally:

```bash
dotnet run --project Backup.Api
```

## Build and Test

```bash
dotnet build Backup.Cli/Backup.Cli.csproj -c Release
dotnet build Backup.Api/Backup.Api.csproj -c Release
dotnet test Backup.Tests/Backup.Tests.csproj -c Release
```

## Run with Docker

Build CLI image:

```bash
docker build -f Dockerfile.Cli -t twitter-x-backup:latest .
```

Build API image:

```bash
docker build -f Dockerfile.Api -t twitter-x-backup-api:latest .
```

Deploy with scripts (PowerShell):

```powershell
.\deploy-cli.ps1
.\deploy-api.ps1
```

Linux override:

```bash
docker compose -f compose.yml -f compose.linux.yml up -d
```

Windows override:

```bash
docker compose -f compose.yml -f compose.windows.yml up -d
```

For Windows CIFS volumes, create `.env` from `.env.example` and set your credentials/paths.

## Architecture

- `Backup.Domain/`: domain entities and core contracts.
- `Backup.Application/`: application workflows/use-cases.
- `Backup.Configuration/`: shared host configuration bootstrap (API + CLI).
- `Backup.Infrastructure/`: adapters and implementations.
  - `Backup.Infrastructure/Data/`: storage adapters (post/media/bulk/dump/proxy/partition).
  - `Backup.Infrastructure/Services/`: runtime services (post/media/bulk/proxy/config/utils).
  - `Backup.Infrastructure/Models/`: configuration and DTO models.
  - `Backup.Infrastructure/DependencyInjection/`: composition root modules.
  - `Backup.Infrastructure/Hosting/`: CLI runtime orchestration.
- `Backup.Cli/`: CLI host entry point.
- `Backup.Api/`: REST API host (controllers + Swagger).
- `Backup.Tests/`: unit/integration tests for infrastructure + API behaviors.

## Security

- Do not commit real tokens/cookies/sessions.
- Keep runtime `config/*.json` out of version control.
- Keep only sanitized sample values in `App/Config.example/*.json`.

## License

This repository includes a `LICENSE` file.
