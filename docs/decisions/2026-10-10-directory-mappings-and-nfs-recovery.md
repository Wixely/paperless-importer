# Directory mappings and NFS recovery

- Status: Accepted
- Date: 2026-10-10
- Source: User direction during MVP hardening

## Context

The importer may receive scans from more than one directory. Docker supplies the mounts, so NFS details should be transparent to the application. Each input needs a corresponding location for successfully ingested sources. NFS outages and files that are still open or changing must not terminate polling or interfere with the writer.

## Decision

Configure an ordered array of directory mappings. Each mapping contains an input, processing, successful-output, and quarantine path. The poll interval, minimum age, and required stable observations remain global Docker-configurable settings.

Each mapping is scanned behind its own error boundary. A missing or failing mapping is reported by index and marks readiness unhealthy, but other mappings continue. Later polls retry the failed mapping with bounded backoff. The worker does not create a missing input directory, avoiding accidental writes to a local mount point when a mount is absent.

A candidate must have stable size and modification time across the configured number of polls, meet the minimum age, and permit an exclusive read open before claim. If the open or atomic move fails, the source is left in place and retried on the next poll. On platforms where native writers do not participate in .NET sharing semantics, stability and minimum age remain the primary readiness controls.

After Paperless reports `SUCCESS`, the claimed source moves to that mapping's output directory. `FAILURE` moves it to that mapping's quarantine directory. Input, processing, output, and quarantine should be paths on the same mounted filesystem so rename remains atomic.

## Alternatives

- One worker/container per directory: operationally repetitive and unnecessary for the expected scale.
- Recursive discovery under one root: rejected because it obscures explicit input/output ownership.
- Watcher-based notifications: rejected because NFS watcher reliability is the problem this polling service avoids.
- Copy-and-delete across filesystems: rejected for the MVP because it weakens atomic disposition and complicates crash recovery without durable state.

## Consequences

- Adding another source is configuration-only: add four indexed paths and a Docker mount.
- One NFS outage cannot stop unrelated mappings, but overall readiness becomes unhealthy until all configured mappings scan successfully.
- Open or changing files remain untouched and may wait additional polling intervals.
- Output naming preserves the original filename; collisions receive a random suffix.

## Validation

- Automated tests cover stable/growing files, an exclusively open Windows file, and continued progress when another mapping is unavailable.
- Deployment acceptance must exercise a real mount outage, recovery, permissions, and representative printer write behaviour with synthetic documents.
