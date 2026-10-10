# Paperless Importer

Paperless Importer is a small .NET 10 worker that moves completed printer scans from an NFS-backed inbox into Paperless-ngx through its supported HTTP API.

- Status: MVP implemented; awaiting required Paperless account permissions and synthetic environment acceptance
- Last reviewed: 2026-10-10
- Intended host: private local GitLab; no remote has been created
- License: private/closed source

## Why this exists

Paperless-ngx normally watches a consumption directory and imports files placed there. In the environment that prompted this project, that directory is an NFS mount and Paperless's consumer is not reliably recovering from storage interruptions. Files can accumulate in the consume directory while the Paperless container and consumer process still appear healthy. Restarting Paperless starts a fresh scan and imports the backlog, but that is a manual recovery action rather than a reliable ingestion path.

The observed failure was investigated on Paperless-ngx 2.20.4 on 2026-10-09. The consumer process remained alive with queued files present, but its polling worker was no longer discovering them. The evidence was consistent with the filesystem polling worker stopping after a transient NFS error while the parent process continued running. This is a description of the observed deployment failure, not a claim that every Paperless installation or current release has the same defect.

Paperless Importer moves responsibility for inbox discovery into a small, independently monitored service. It polls explicitly, survives and retries NFS failures, waits for files to finish being written, and submits completed documents through Paperless's supported HTTP API. Paperless still performs document ingestion, OCR, classification, indexing, and storage; this service replaces only the unreliable consume-directory handoff.

## MVP behaviour

The importer is intentionally stateless. It has no database and stores no token, URL, document content, or filename in application state.

1. Poll every configured input/output directory mapping independently.
2. Require unchanged size and modification time across at least two polls and a configurable minimum age.
3. Probe for exclusive read access, then atomically move a ready file into that mapping's unique processing directory on the same filesystem. Open, locked, changing, or temporarily unavailable files stay untouched for the next poll.
4. Read Paperless task records and look for a recent matching `task_file_name` before uploading.
5. If no matching task exists, upload with `POST /api/documents/post_document/`.
6. Leave the claimed source in processing while the Paperless task is pending.
7. Apply that location's configured `Move`, `Delete`, or `Retain` action only after `SUCCESS`, or preserve a failed import in quarantine after `FAILURE`.

Each processing directory is a restart-recovery queue. Original filenames are preserved there so they can be compared with Paperless tasks. Logs contain only event categories, location indexes, counts, and redacted error codes.

Each location has its own filesystem error boundary. If one NFS-backed mapping disappears or throws during enumeration, the worker reports degraded readiness, continues servicing other mappings, applies bounded backoff, and retries the failed mapping on later polls. The application does not configure NFS and does not create a missing input directory; Docker/host mounts remain transparent filesystem paths.

## Stateless duplicate boundary

Duplicate avoidance depends on Paperless exposing task records with `task_file_name` and `date_created`. A matching task must be newer than the source file modification time, allowing the configured clock-skew margin. This avoids treating an old task for a reused filename as the current import.

There is no exactly-once guarantee without durable idempotency state. A duplicate remains possible if Paperless prunes or hides the relevant task, sanitizes the filename differently, or accepts an upload that is not visible by the next poll after the client loses the response. Paperless's content duplicate detection remains useful defence in depth. Validate task retention and representative printer filenames before production cutover.

## Required Paperless permissions

The account represented by the API token must have all three of these global permissions:

| Paperless UI permission | Permission code | Why it is required |
| --- | --- | --- |
| **Document: Add** | `documents.add_document` | Submit documents through the supported upload API |
| **PaperlessTask: View** | `documents.view_paperlesstask` | Check for prior uploads and wait for terminal task status |
| **UISettings: View** | `documents.view_uisettings` | Read the current account's effective permission list during fail-closed startup validation |

Use a dedicated regular account with only the permissions it needs; superuser access is unnecessary and discouraged.

Every normal process start performs a mandatory preflight before any directory is scanned or file is claimed. It reads `/api/ui_settings/`, verifies the three effective permissions above, and makes a one-item request to `/api/tasks/` to confirm task endpoint access. Missing permissions or a rejected settings/task request terminate the process with a non-zero exit code. An unavailable or invalid token also terminates startup. Docker therefore remains stopped or restarting instead of running an importer that cannot perform stateless reconciliation. Health-check command invocations do not repeat the remote preflight.

## Configuration

.NET configuration maps double underscores in environment variables to sections. Docker configuration supplies all deployment-specific paths, the endpoint, and the token location.

| Setting | Default | Purpose |
| --- | --- | --- |
| `Paperless__BaseUrl` | required | HTTPS Paperless base URL, including a trailing slash |
| `Paperless__TokenFile` | required in Docker | Mounted file containing the API token |
| `Paperless__Token` | empty | Direct environment fallback; a mounted secret is preferred |
| `Importer__Locations__0__InputPath` | `data/inbox` | First printer-owned landing directory |
| `Importer__Locations__0__ProcessingPath` | `data/processing` | First importer-owned claim directory |
| `Importer__Locations__0__OutputPath` | `data/archive` | First destination after `SUCCESS` |
| `Importer__Locations__0__QuarantinePath` | `data/quarantine` | First destination after `FAILURE` |
| `Importer__Locations__0__SuccessDisposition` | `Move` | `Move`, `Delete`, or `Retain` after `SUCCESS` |
| `Importer__StablePollsRequired` | `2` | Consecutive identical observations required |
| `Importer__MinimumAgeSeconds` | `30` | Minimum source age before claim |
| `Importer__PollIntervalSeconds` | `10` | Normal polling interval |
| `Importer__MaximumFileBytes` | `104857600` | Maximum accepted file size |
| `Importer__AllowedExtensions__0` etc. | PDF and common image types | Extension allow-list |

Never put a real token, production URL, private CA, or sensitive filename in the repository. For a private CA, mount a complete trusted PEM bundle and set `SSL_CERT_FILE`; TLS validation is never disabled.

Add further mappings by incrementing the index (`Locations__1`, `Locations__2`, and so on) and mounting each root into the container. Input and processing must be on the same filesystem so claiming is atomic. Output and quarantine may be separate mounts: terminal disposition copies to a claim-specific temporary file, flushes it, atomically renames it at the destination, verifies an existing retry target by SHA-256, and only then deletes the claimed source. `Retain` leaves the file in a completed processing claim that future polls skip. Poll interval, minimum age, stable-poll count, and success disposition are configurable in Compose.

## Run and test locally

The repository pins .NET SDK 10.0.300 or its latest patch. Put a disposable token in the ignored `secrets/paperless-token.txt` only when an approved non-production endpoint is available.

```powershell
dotnet restore PaperlessImporter.slnx
dotnet build PaperlessImporter.slnx --no-restore
dotnet test PaperlessImporter.slnx --no-restore
dotnet run --project src/PaperlessImporter/PaperlessImporter.csproj
```

VS Code build, test, and debug configurations are in `.vscode/`. The debug profile uses the ignored token file and a non-routable example endpoint until locally overridden.

## Docker

Copy `.env.example` to `.env`, set the local paths and endpoint, create the ignored token file, then use the example Compose file:

```powershell
docker compose --env-file .env -f compose.example.yml build
docker compose --env-file .env -f compose.example.yml up -d
```

The image is a non-root, self-contained, single-file NativeAOT Linux executable. It exposes no network port. The example bind mount points to one filesystem containing `inbox`, `processing`, `output`, `quarantine`, and `health` directories. To add another mapping, add another bind mount and the four corresponding indexed environment settings. Mount ownership must permit the image's non-root user to rename and create files.

On startup, check `docker compose ps` and the container log. A permission error names the missing permission code and exits with code 78; Paperless/token availability or response-contract failure exits with code 69. No input file is touched before this check succeeds.

## Health

The worker writes a small transient health file containing timestamps, backlog count, and a dependency status code—never filenames or credentials. Separate commands return exit code 0 when healthy:

```powershell
paperless-importer --health-live
paperless-importer --health-ready
```

Liveness requires a recent worker heartbeat. Readiness additionally requires a recent successful scan, a healthy Paperless task query, and recent forward progress while a backlog exists. Docker uses readiness for its health check.

## Other service hosts

- Run the executable directly for interactive operation.
- `deploy/windows/install-service.ps1` installs the published executable as a Windows Service; configure service environment variables securely before starting it.
- `deploy/systemd/paperless-importer.service` is a hardened systemd template whose paths and environment file must be adapted to the host.

## Current evidence

Automated checks through 2026-10-10 cover deployment configuration binding, readiness stability, growth, disappearance/reappearance, deferral of an exclusively open file, isolation of an unavailable directory mapping, Paperless task response parsing, startup permission enforcement and task-endpoint probing, duplicate avoidance for a matching task, claim retention after upload, all three success dispositions, collision-safe output movement, and separate health semantics. The Release test run passed all 20 tests. A live Windows process accepted Docker-style environment configuration, exited with code 78 for the current account's missing introspection permission, and did not emit the configured endpoint. NativeAOT and container execution remain unverified on this workstation because the Windows C++ linker and Docker are not installed.

## What remains

- Obtain and mount a dedicated least-privilege Paperless token and trusted CA bundle if required; never retain the supplied login password in deployment configuration.
- Grant and verify `documents.add_document`, `documents.view_paperlesstask`, and `documents.view_uisettings` for the Paperless service account.
- Validate task retention, filename normalization, and queue visibility using synthetic PDF and image fixtures.
- Validate atomic rename, open-file behaviour, outage recovery, and non-root permissions on each intended NFS mount.
- Complete the exclusive cutover that prevents Paperless's native consumer racing this importer.

Recommended next action: configure a disposable/staging Paperless account and run the documented synthetic end-to-end acceptance before any production cutover. Detailed status is maintained in [docs/work-queue.md](docs/work-queue.md).
