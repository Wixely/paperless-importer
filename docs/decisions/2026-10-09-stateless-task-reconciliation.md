# Stateless task reconciliation

- Status: Accepted
- Date: 2026-10-09
- Source: User direction during MVP implementation

## Context

The original plan proposed SQLite and Dapper for durable import state. The user clarified that the importer does not need an application database and should check the Paperless queue to avoid duplicate submissions. Deployment keys and endpoints belong in Docker configuration.

## Decision

The importer has no database. It atomically moves each ready source into a uniquely named processing directory, preserving the original filename. On every scan it reads Paperless tasks and matches by `task_file_name`, rejecting tasks older than the file modification time minus a configurable clock-skew allowance.

A matching pending task leaves the source claimed. `SUCCESS` moves it to archive and `FAILURE` moves it to quarantine. If no recent matching task exists, the file is uploaded. After restart, the processing filesystem is scanned again and reconciled the same way.

The Paperless URL is an environment setting. The token may be an environment setting, but Docker uses a mounted secret file by default. Private CA trust is supplied as a mounted trusted bundle; TLS validation remains enabled.

## Alternatives

- SQLite/Dapper state with an upload-attempt ledger: stronger recovery and ambiguity handling, but explicitly rejected for this MVP.
- Content-hash lookup against Paperless documents: potentially stronger duplicate detection, but more API work and permissions than requested.
- No claim directory: simpler, but unsafe when multiple scans or restarts overlap.

## Consequences

- Deployment and backup are simpler; no schema or database lifecycle exists.
- The filesystem processing layout becomes the restart-recovery queue.
- Exactly-once import is not guaranteed. Duplicate protection depends on Paperless task visibility, retention, filename normalization, and creation timestamps.
- An accepted upload whose response is lost can be repeated if its task is not visible by the next scan.
- Reused filenames are protected only when the new source modification time is later than the historic task.

## Validation

- Automated tests must show that a recent matching task prevents upload and drives success/failure disposition.
- Synthetic acceptance must confirm the deployed Paperless version exposes `task_file_name`, `date_created`, and terminal status to the service account.
- Production cutover remains blocked until task retention/visibility and representative filename normalization are observed.

## Supersedes

This decision supersedes the proposed SQLite/Dapper architecture in the initial README and work queue. No earlier accepted decision record existed.
