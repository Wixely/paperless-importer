# Work queue

- Last reviewed: 2026-10-10
- Current phase: Stateless MVP verification and deployment acceptance

Task states are `Not started`, `In progress`, `Blocked`, or `Complete`. Acceptance evidence names the command or observation and date.

## MVP implementation

| State | Task | Owner | Exit evidence |
| --- | --- | --- | --- |
| Complete | Bootstrap .NET 10 solution, tests, VS Code tasks, and interactive worker | Implementation agent | `dotnet build PaperlessImporter.slnx --no-restore` passed on 2026-10-09 |
| Complete | Record the stateless Paperless-task reconciliation decision | User + implementation agent | `docs/decisions/2026-10-09-stateless-task-reconciliation.md` |
| Complete | Implement stable size/mtime observations and minimum age | Implementation agent | Automated growth, stability, and disappearance tests passed on 2026-10-09 |
| Complete | Defer open/locked sources without interrupting their writer | Implementation agent | Windows exclusive-open integration test passed on 2026-10-10; source remains untouched until a later poll |
| Complete | Support multiple independent input/output mappings | Implementation agent | Configuration array and unavailable-location isolation test passed on 2026-10-10 |
| Complete | Keep polling resilient to filesystem/NFS failures | Implementation agent | Startup directory work moved inside error boundaries; mapping failures are isolated and retried with bounded backoff |
| Complete | Atomically claim files without a database and recover claims after restart | Implementation agent | Unique processing directories are re-enumerated; automated claim reconciliation tests passed on 2026-10-09 |
| Complete | Implement Paperless upload, task parsing, and terminal disposition | Implementation agent | Contract parsing and worker reconciliation tests passed on 2026-10-09 |
| Complete | Add redacted logs and separate liveness/readiness checks | Implementation agent | Automated health tests passed on 2026-10-09 |
| Complete | Add interactive, Windows Service, systemd, and Docker packaging assets | Implementation agent | Assets present; build/runtime verification tracked below |
| Complete | Add configuration and operator documentation | Implementation agent | README documents environment, mounted secret, CA, operation, health, and limitations |

## Verification and acceptance

| State | Task | Owner | Exit evidence |
| --- | --- | --- | --- |
| Blocked | Publish the self-contained single-file NativeAOT executables | Implementation agent | Windows requires the missing Visual Studio C++ linker workload; Linux publish is intended to run in Docker; the workstation also reached zero free disk during a fallback publish on 2026-10-09 |
| Blocked | Build and smoke-test the container | Implementation agent | Docker CLI/engine is not installed on this workstation as observed on 2026-10-09 |
| Not started | Supply a dedicated Paperless endpoint/token and trusted CA bundle | User | Credentials remain external; task-list and upload permissions verified |
| Not started | Validate the Paperless task-list contract and retention | User + implementation agent | Synthetic upload exposes filename, creation time, and terminal state long enough for reconciliation |
| Not started | Observe representative printer writes | Implementation agent | Dated non-sensitive evidence describes open/write/rename behaviour and timestamp resolution |
| Not started | Validate same-filesystem atomic rename and permissions on intended NFS mount | Implementation agent | Safe synthetic fixture moves successfully under deployment UID/GID |
| Not started | Run synthetic PDF and image end-to-end acceptance | User + implementation agent | Both reach `SUCCESS`; source archives only afterward; no secrets or sensitive names logged |
| Not started | Exercise Paperless outage, NFS outage, restart, ambiguous response, and task failure | Implementation agent | Dated observations show polling recovery and retained/quarantined sources |

## Deployment decisions still required

| State | Task | Owner | Exit evidence |
| --- | --- | --- | --- |
| Not started | Choose archive retention | User | Decision record states retention/deletion and recovery implications |
| Not started | Define exclusive directory ownership and Paperless native-consumer cutover/rollback | User + implementation agent | Deployment layout prevents competing consumers |
| Not started | Confirm timing, backlog, and alert thresholds | User | Values recorded as acceptance criteria |

## Evidence log

- 2026-10-09, automated: `dotnet restore PaperlessImporter.slnx` succeeded against public NuGet only.
- 2026-10-09, automated: `dotnet build PaperlessImporter.slnx --no-restore` succeeded with zero warnings and zero errors.
- 2026-10-09, automated: `dotnet test PaperlessImporter.slnx --no-build --no-restore --verbosity quiet` passed all 9 tests.
- 2026-10-09, publish: Windows NativeAOT reached the native link step but failed because the Visual Studio C++ linker workload is absent.
- 2026-10-09, container: `docker version` could not run because Docker is not installed.
- 2026-10-09, environment: a fallback self-contained publish was stopped by zero free disk space; generated build output was cleaned, restoring approximately 15 MB, but no further publish was attempted.
- 2026-10-10, automated: `dotnet build PaperlessImporter.slnx --no-restore` succeeded with zero warnings and zero errors after multi-location/NFS hardening.
- 2026-10-10, automated: `dotnet test PaperlessImporter.slnx --no-build --no-restore --verbosity quiet` passed all 11 tests, including open-file deferral and unavailable-location isolation.

## Recommended next action

Use the local API and synthetic folder the user will provide to run the Paperless contract, open-file, output-move, restart, and mount-outage acceptance checks. Do not touch the production NFS share or change the native Paperless consumer without a separate explicit request.
