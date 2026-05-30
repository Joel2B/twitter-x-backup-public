# Namespace and Folder Conventions

This project follows folder-to-namespace alignment as the default rule.

## Layers

- `Backup.Domain`: entities and pure domain rules (no infrastructure dependencies).
- `Backup.Application`: use cases/orchestration and ports.
- `Backup.Infrastructure`: adapters, storage, HTTP clients, wiring helpers.
- `Backup.Api` / `Backup.Cli`: entrypoints and host composition.

## Infrastructure namespace map

- Core cross-cutting contracts:
  - `Backup.Infrastructure.Core.Abstractions.*`
- Utility contracts:
  - `Backup.Infrastructure.Utility.Abstractions.Services`
- Feature modules:
  - `Backup.Infrastructure.Posts.*`
  - `Backup.Infrastructure.Media.*`
  - `Backup.Infrastructure.Bulk.*`
  - `Backup.Infrastructure.Dump.*`
  - `Backup.Infrastructure.Proxy.*`

## Practical rules

- Keep contracts in `*/Abstractions/*`.
- Keep implementations in `*/Data`, `*/Adapters`, or `*/Services`.
- Avoid `Interfaces` catch-all namespaces; prefer module-scoped abstractions.
- Keep one public responsibility per class.
- Prefer constructor injection through abstractions.

## Refactor checklist

1. Move file to target folder.
2. Align namespace with folder.
3. Update all `using` references.
4. Run:
   - `dotnet build Backup.sln -v minimal`
   - `dotnet test Backup.Tests/Backup.Tests.csproj -v minimal`
