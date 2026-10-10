# Configurable success disposition across mounts

- Status: Accepted; partially supersedes the final-disposition portion of `2026-10-10-directory-mappings-and-nfs-recovery.md`
- Date: 2026-10-10
- Source: User direction for test deployment

## Context

Different inputs may need a successfully ingested source moved, deleted, or retained. The test deployment uses separate Docker NFS volumes for the Paperless consume directory and the importer archive, so a direct filesystem rename cannot be assumed to work across those mount points.

## Decision

Each location selects `Move`, `Delete`, or `Retain` through `SuccessDisposition`. No action occurs until the corresponding Paperless task reports `SUCCESS`.

- `Move` copies the claimed file to a claim-specific temporary destination, flushes it, atomically renames it within the destination filesystem, and then deletes the source. A retry verifies an existing destination by length and SHA-256 before deleting the source. A conflicting original filename uses a deterministic claim-specific suffix.
- `Delete` removes the claimed source and its claim directory.
- `Retain` renames the claim directory with a `completed-` prefix. Future polls skip completed claims, retaining the source without an application database.

Paperless `FAILURE` always uses the same durable cross-mount transfer into quarantine. Input and processing must remain on the same filesystem so the initial ownership claim is atomic.

## Consequences

- The output and quarantine paths may be separate mounts from input and processing.
- A crash before source deletion leaves the claim recoverable; the next poll either completes the transfer or verifies the completed destination.
- `Retain` consumes processing-volume capacity and requires an operator retention policy.
- The worker reads successful file bytes to copy them and may read them again to verify an ambiguous retry, but does not log content, hashes, or filenames.

## Validation

The 2026-10-10 Release test run passed 14 tests, including move, delete, retain-without-reprocessing, and deterministic collision behavior. Real NFS interruption and container acceptance remain outstanding.
