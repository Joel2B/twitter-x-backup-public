> This project is for personal use and may not work for everyone.

## twitter-x-backup

C# tool to back up X/Twitter content (posts and media) using GraphQL endpoints, with data stored locally or on mounted storage based on configuration.

## Status

This project is under active personal development. Structure and configuration format may change.

## Requirements

- .NET SDK 10 (`net10.0`)
- Docker and Docker Compose (optional)
- Valid split configuration files in `App/Config/`

## Configuration

1. Use files in `App/Config.example/` as a starting point.
2. Copy them to `App/Config/` and replace all fields marked as `{REPLACE_THIS}`.
3. Adjust data/debug paths and download settings for your environment.

Note: files in `App/Config/` are intended for local use and should not be committed.

## Run Locally

```bash
dotnet restore
dotnet run --project Backup.Cli
```

Run API locally:

```bash
dotnet run --project Backup.Api
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

## Quick Structure

- `Backup.Cli/Program.cs`: CLI entry point
- `Backup.Api/Program.cs`: API entry point
- `App/`: services, models, utilities, and configuration
- `compose*.yml`: environment-specific deployment files
- `Dockerfile.Cli`: CLI runtime image definition
- `Dockerfile.Api`: API runtime image definition
- `deploy-cli.ps1`: build/push CLI image
- `deploy-api.ps1`: build/push API image

## Security

- Do not commit real tokens/cookies/sessions.
- Keep `App/Config/*.json` out of version control.
- Keep only sanitized sample values in `App/Config.example/*.json`.

## License

This repository includes a `LICENSE` file.
