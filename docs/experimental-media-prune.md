# Preview and prune unused media variants

> Experimental: review every preview before applying it. Keep runtime backup and download processes stopped during apply and purge operations.

This command compares loose media files recorded in PostgreSQL with paths produced by active `Medias.json` policy. Preview stores a resumable plan without changing files or cache rows. Apply moves unused variants into quarantine and removes their cache rows.

## Current scope

Media prune currently supports:

- Photo JPG and PNG variants
- Video thumbnails
- Animated GIF thumbnails
- Video MP4 variants
- Loose files represented by PostgreSQL media cache rows

Media prune does not support:

- ZIP files or backup chunks
- Direct backup copies
- HLS files
- Orphan files without PostgreSQL cache rows
- Automatic restoration from quarantine

Candidate discovery queries PostgreSQL. It does not recursively enumerate media roots. Physical checks open exact candidate and retained paths only.

## Prerequisites

Before previewing a plan:

1. Build or pull an image containing experimental media prune commands.
2. Configure an enabled PostgreSQL media cache in `Data.json`.
3. Confirm active `Medias.json` contains intended media filters.
4. Identify media store and cache IDs from `Data.json`.
5. Mount every configured media partition in the maintenance container.

Examples below use these sample IDs:

```text
media store: nas media
cache: postgres
```

Replace paths and IDs when your configuration differs.

## Preview a prune plan

Preview creates PostgreSQL run and operation records. It does not move files, delete files, or remove cache rows. Runtime may remain active, but stable input produces easier-to-review totals.

Run preview through Docker Compose:

```bash
docker compose -f compose.yml -f compose.linux.yml run --rm \
  twitter-x-backup-cli \
  maintenance media-prune preview \
  --data-config /app/config/Data.json \
  --media-store "nas media" \
  --cache postgres
```

Run preview from a local build:

```bash
dotnet run --project Backup.Cli -- \
  maintenance media-prune preview \
  --data-config ./config/Data.json \
  --media-store "nas media" \
  --cache postgres
```

`prune` is a non-destructive alias for `preview`:

```bash
docker compose -f compose.yml -f compose.linux.yml run --rm \
  twitter-x-backup-cli \
  maintenance media-prune prune \
  --data-config /app/config/Data.json \
  --media-store "nas media" \
  --cache postgres
```

Both commands return these values:

- `run`: unique plan ID used by status, apply, and cancel
- `approval`: SHA-256 approval hash covering plan operations and fingerprints
- `policy hash`: hash of active media policy
- `projection hash`: hash of desired paths produced from posts and policy
- `operations`: number of files selected for quarantine
- `planned bytes`: total candidate bytes

Save `run` and `approval`. Apply requires both values.

## Review plan status

Status displays up to 20 recent runs:

```bash
docker compose -f compose.yml -f compose.linux.yml run --rm \
  twitter-x-backup-cli \
  maintenance media-prune status \
  --data-config /app/config/Data.json \
  --media-store "nas media" \
  --cache postgres
```

Review these fields before apply:

- Run state must be `planned`
- Completed operations must be `0`
- Planned operation count must be plausible for your policy change
- Planned byte count must be plausible for your storage
- Approval must match preview output

## Apply an approved plan

Apply changes files and PostgreSQL cache rows. Stop normal runtime first. Do not run backup, media download, cache maintenance, or another prune concurrently.

Stop runtime container:

```bash
docker stop twitter-x-backup-cli
```

Apply reviewed plan:

```bash
docker compose -f compose.yml -f compose.linux.yml run --rm \
  twitter-x-backup-cli \
  maintenance media-prune apply \
  --data-config /app/config/Data.json \
  --media-store "nas media" \
  --cache postgres \
  --run-id your_run_uuid_here \
  --approval your_64_character_sha256_here \
  --batch-size 1000 \
  --runtime-stopped-confirmed
```

Apply validates these conditions before mutation:

- Approval matches stored plan
- Stored operations still match approval hash
- `Medias.json` policy hash has not changed
- Desired path projection has not changed
- PostgreSQL primary cache namespace has not changed
- Candidate files retain preview size and timestamp
- Retained files retain preview size and timestamp
- Every desired path remains present for affected groups
- Paths remain contained beneath configured media roots
- Paths do not traverse reparse points

If validation fails, no new operation starts. Cancel planned run and create a new preview when policy or projection changed.

## Resume interrupted apply

Apply records each completed operation in PostgreSQL. If process stops, rerun same command with same run ID and approval:

```bash
docker compose -f compose.yml -f compose.linux.yml run --rm \
  twitter-x-backup-cli \
  maintenance media-prune apply \
  --data-config /app/config/Data.json \
  --media-store "nas media" \
  --cache postgres \
  --run-id your_run_uuid_here \
  --approval your_64_character_sha256_here \
  --batch-size 1000 \
  --runtime-stopped-confirmed
```

Resume accepts these recoverable states:

- Candidate exists at active path: validate and move it into quarantine
- Candidate exists in quarantine: continue cache-row deletion and verification
- Operation state is verified: skip it

Resume rejects ambiguous state where active and quarantine copies both exist or both copies are absent.

## Cancel an unused plan

Cancel marks a `planned` run as `cancelled`. It does not touch files or cache rows:

```bash
docker compose -f compose.yml -f compose.linux.yml run --rm \
  twitter-x-backup-cli \
  maintenance media-prune cancel \
  --data-config /app/config/Data.json \
  --media-store "nas media" \
  --cache postgres \
  --run-id your_run_uuid_here
```

Cancel rejects `applying`, `quarantined`, and `purged` runs.

## Quarantine retention

Successful apply changes run state to `quarantined`. Files remain under each partition media root:

```text
.media-prune-quarantine/run_id/original_logical_path
```

Each file stays on its original volume, so movement into quarantine remains an atomic rename. PostgreSQL records purge eligibility seven days after apply.

Do not run `purge-expired` until you have checked backup runtime and retained media.

## Purge expired quarantine

Purge permanently deletes quarantine files whose seven-day retention expired. This operation is irreversible. Stop normal runtime first.

```bash
docker compose -f compose.yml -f compose.linux.yml run --rm \
  twitter-x-backup-cli \
  maintenance media-prune purge-expired \
  --data-config /app/config/Data.json \
  --media-store "nas media" \
  --cache postgres \
  --batch-size 1000 \
  --runtime-stopped-confirmed
```

Purge verifies quarantine file size and timestamp before deletion. It skips runs whose retention deadline has not elapsed.

## Restore before purge

Automatic restore is not available. If review finds a problem, keep runtime stopped and do not run `purge-expired`.

Record affected run ID and inspect corresponding PostgreSQL operations before moving files. Restore requires both actions:

1. Move each quarantine file back to its exact original logical path.
2. Restore matching media cache rows with original namespace, partition, and size.

Do not restore files without their cache rows. Do not recreate cache rows without matching files. Use a dedicated restore command when one becomes available.

## Run states

| State | Meaning | Allowed next action |
| --- | --- | --- |
| `planned` | Preview stored; no files changed | Apply or cancel |
| `applying` | One or more operations may be in progress | Resume apply |
| `quarantined` | Apply verified; retention active | Wait or purge after deadline |
| `cancelled` | Unused plan cancelled | Create another preview |
| `purged` | Eligible quarantine permanently deleted | No action |

## PostgreSQL state

Media prune creates and updates these tables:

```text
media_loose_prune_runs
media_loose_prune_operations
```

Schema updates use idempotent `CREATE TABLE IF NOT EXISTS` and `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` statements. Run records retain policy, projection, approval, progress, and purge state for audit and resume.

## Safety limits

- Preview may use substantial memory when millions of candidates qualify
- Apply hashes operations through a streaming PostgreSQL reader
- Only one planned or applying run may exist per media store and cache
- Changed policy or projection invalidates apply
- Missing desired files preserve affected groups during preview
- Duplicate normalized paths preserve affected groups during preview
- Unknown media path layouts are outside current prune scope
- Quarantine uses storage space until purge completes

Treat this feature as experimental until representative preview and resume tests have completed against your deployment.
