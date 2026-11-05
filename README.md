> This project is for personal use and may not work for everyone.

## twitter-x-backup

C# tool to back up X/Twitter content (posts and media) using GraphQL endpoints, with data stored locally or on mounted storage based on configuration.

## Status

This project is under active personal development. Structure and configuration format may change.

## Requirements

- .NET SDK 10 (`net10.0`)
- Docker and Docker Compose (optional)
- Valid split configuration files in `Config/` (or an explicit config directory)

## Configuration

1. Use files in `Config.example/` as a starting point.
2. Copy them to your runtime config directory (recommended: `./Config/`) and replace all fields marked as `{REPLACE_THIS}`.
3. Adjust data/debug paths and download settings for your environment.
4. Optional host-level override:
   - Copy `Backup.Api/appsettings.example.json` to `Backup.Api/appsettings.json`
   - Copy `Backup.Cli/appsettings.example.json` to `Backup.Cli/appsettings.json`
   - Set `Backup:Configuration:ConfigDirectory` to your runtime config folder.

Configuration directory resolution order:
- `RuntimeConfigurationOptions.ConfigDirectory` (when provided in host wiring)
- `Backup:Configuration:ConfigDirectory` from host configuration (`appsettings*.json`/env/providers)
- env var `BACKUP__CONFIG__DIRECTORY`
- `./Config`
- `./Config.example`
- `./config` (legacy compatibility)
- `./config.example` (legacy compatibility)
- `<app-base>/config`

Note: runtime config files are intended for local/deployment use and should not be committed.

Media cache backend:
- `Data.Media[].Cache` is an array.
- Each cache entry supports `Id`, `Default`, `Enabled`, `Type`, `ConnectionString`, and `Path`.
- Supported `Type` values are `json`, `sqlite`, and `postgres`.
- One enabled cache can be marked `Default`; otherwise the first enabled cache is used as primary.
- Replica caches receive the same snapshot and incremental cache writes as the primary cache.
- For file-based cache entries, configure `Path`.
- For `sqlite`, the cache uses the configured `Path.File` as the primary snapshot file and stores incremental snapshots in SQLite under the media cache temp/downloaded path.
- For `postgres`, set `ConnectionString`. The cache namespace is derived automatically from the media store `Id` plus the cache `Id`.

Post store backends:
- `Data.Post[].Type` supports `local`, `sqlite`, and `postgres`.
- For `postgres`, set `Data.Post[].ConnectionString` to a valid PostgreSQL connection string.
- The post parity check compares all enabled post stores, so you can run JSON + SQLite + PostgreSQL together.

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

## Experimental features

- [Preview and prune unused media variants](docs/experimental-media-prune.md)

## Run with Docker

Build CLI image:

```bash
docker build -f Dockerfile --target cli -t twitter-x-backup-cli:latest .
```

Build API image:

```bash
docker build -f Dockerfile --target api -t twitter-x-backup-api:latest .
```

Deploy with scripts (PowerShell):

```powershell
Copy-Item .env.example .env
# Set DOCKER_REGISTRY in .env
.\release.ps1
.\release.ps1 all
.\release.ps1 cli
.\release.ps1 api
```

Create local Compose files for Linux:

```bash
cp compose.example.yml compose.yml
cp compose.linux.example.yml compose.linux.yml
cp .env.example .env
cp deploy.example.sh deploy.sh
chmod +x deploy.sh
docker compose -f compose.yml -f compose.linux.yml up -d
```

Create local Compose files for Windows:

```powershell
Copy-Item compose.example.yml compose.yml
Copy-Item compose.windows.example.yml compose.windows.yml
Copy-Item .env.example .env
docker compose -f compose.yml -f compose.windows.yml up -d
```

Edit `.env` before starting or releasing. `DOCKER_REGISTRY` controls release destination,
Linux uses `BACKUP_DATA_ROOT`, and Windows CIFS uses `SMB_DATA_DEVICE`, `SMB_DATA_USER`,
and `SMB_DATA_PASS`. Local Compose files and `.env` are ignored by Git.

CLI and API share one deployment directory, configuration, data, and debug mounts. API is
published on `API_PORT` (default `8081`). On Linux, use the deployment helper:

```bash
./deploy.sh                  # deploy CLI and API
./deploy.sh all deploy       # deploy CLI and API
./deploy.sh cli deploy       # deploy only CLI
./deploy.sh api deploy       # deploy only API
./deploy.sh all start
./deploy.sh cli stop
./deploy.sh api restart
./deploy.sh all logs
```

`stop` sends `SIGINT`, waits up to `STOP_TIMEOUT_SECONDS` (default `120`), and never sends
`SIGKILL`.

## Architecture

- `Backup.Domain/`: domain entities and core contracts.
- `Backup.Application/`: application workflows/use-cases.
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
- Keep runtime `Config/*.json` out of version control.
- Keep only sanitized sample values in `Config.example/*.json`.

## License

This repository includes a `LICENSE` file.
