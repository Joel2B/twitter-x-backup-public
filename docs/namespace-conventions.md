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

## What Belongs In Infrastructure

- IO concerns:
  - file read/write, directory creation, zip stream operations
  - HTTP calls and transport retries
  - database persistence and transaction boundaries
- Adapter concerns:
  - mapping infrastructure models to application/domain models
  - wiring external libraries to internal ports
- Composition concerns:
  - dependency registration and keyed/store resolution

## What Must Stay Out Of Infrastructure

- Business decisions:
  - selection rules (`which items should be removed/kept`)
  - transition rules (`when a proxy becomes inactive`)
  - planning rules (`how to assign/replicate/chunk data`)
- Cross-aggregate orchestration policies that do not require IO.
- Threshold/consistency rules that can run on plain models.

## Quick PR Checklist (Layer Guard)

1. For every new `if`/`switch`, ask:
   - does it depend on IO or external framework state?
   - if no, move it to `Backup.Application`.
2. In `Infrastructure`, keep methods shaped like:
   - load data
   - call application service/policy
   - persist/emit side effects
3. If a class does both planning and IO, split:
   - `*PlanningService` (Application)
   - `*IOService` or `*Adapter` (Infrastructure)
4. Add at least one unit test for moved business rule using fakes/in-memory data.
5. Run targeted validation:
   - `dotnet build Backup.Infrastructure/Backup.Infrastructure.csproj`
   - `dotnet build Backup.Cli/Backup.Cli.csproj`
   - `dotnet build Backup.Api/Backup.Api.csproj`
   - `dotnet test Backup.Tests/Backup.Tests.csproj`

## Refactor checklist

1. Move file to target folder.
2. Align namespace with folder.
3. Update all `using` references.
4. Run:
   - `dotnet build Backup.sln -v minimal`
   - `dotnet test Backup.Tests/Backup.Tests.csproj -v minimal`
