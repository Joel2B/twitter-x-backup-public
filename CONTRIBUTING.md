# Contributing

This project is maintained for personal use, but contributions are welcome if they follow the same engineering standards used in this repository.

## Prerequisites

- .NET SDK 10 (`net10.0`)
- Git
- Optional: Docker and Docker Compose

## Setup

1. Clone the repository.
2. Restore dependencies:
```bash
dotnet restore
```
3. Create local runtime config from samples:
   - Source: `config.example/`
   - Local target: `config/`

Do not commit real config values.

## Development Workflow

1. Create a feature branch from `main`.
2. Keep changes scoped and cohesive (small, reviewable commits).
3. Use clear commit messages, preferably conventional style:
   - `refactor(infrastructure): ...`
   - `fix(api): ...`
   - `docs(readme): ...`
4. Before opening a PR, run the required validation commands.

## Required Validation

Run all three commands and ensure they pass:

```bash
dotnet build Backup.Cli/Backup.Cli.csproj -c Release --no-restore
dotnet build Backup.Api/Backup.Api.csproj -c Release --no-restore
dotnet test Backup.Tests/Backup.Tests.csproj -c Release --no-restore
```

If your change affects only one area, still run the full suite before merge.

## Repository Rules

- Keep architecture boundaries:
  - `Backup.Domain`: domain model
  - `Backup.Application`: application use-cases
  - `Backup.Infrastructure`: implementations/adapters
  - `Backup.Cli`: CLI host
  - `Backup.Api`: API host
- Avoid introducing new cross-layer dependencies that bypass these boundaries.
- Keep folder structure aligned with namespaces, especially in `Backup.Infrastructure`.

## Security and Sensitive Data

- Never commit credentials, tokens, cookies, or live session data.
- Never commit local runtime config files:
  - `config/*.json`
  - `config/**/*.json`
- Keep only sanitized sample values in `config.example/`.

## Pull Request Checklist

- [ ] Build and tests pass locally.
- [ ] No sensitive data was added.
- [ ] Documentation updated (if behavior/structure changed).
- [ ] Changes are scoped and commit history is readable.
