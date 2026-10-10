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
| Complete | Support move, delete, and retained-claim success policies across separate mounts | Implementation agent | Automated disposition and deterministic collision tests passed on 2026-10-10; `docs/decisions/2026-10-10-success-disposition.md` |
| Complete | Add redacted logs and separate liveness/readiness checks | Implementation agent | Automated health tests passed on 2026-10-09 |
| Complete | Fail startup unless Paperless upload and reconciliation permissions are effective | Implementation agent | Profile permission and task-endpoint preflight tests passed on 2026-10-10; `docs/decisions/2026-10-10-startup-permission-gate.md` |
| Complete | Add interactive, Windows Service, systemd, and Docker packaging assets | Implementation agent | Assets present; build/runtime verification tracked below |
| Complete | Add configuration and operator documentation | Implementation agent | README documents environment, mounted secret, CA, operation, health, and limitations |
| Complete | Add GitHub CI and release automation | Implementation agent | CI tests and builds AMD64/ARM64 containers; semantic-version tags publish GHCR images with SBOM/provenance and generate GitHub releases |

## Verification and acceptance

| State | Task | Owner | Exit evidence |
| --- | --- | --- | --- |
| Blocked | Publish the self-contained single-file NativeAOT executables | Implementation agent | Windows requires the missing Visual Studio C++ linker workload; Linux publish is intended to run in Docker; the workstation also reached zero free disk during a fallback publish on 2026-10-09 |
| In progress | Build and smoke-test the container | Implementation agent | GitHub Actions built and published Linux AMD64/ARM64 image `v0.1.0` on 2026-10-10; runtime health and mounted-filesystem smoke tests remain outstanding |
| Complete | Supply a dedicated Paperless endpoint/token and verify least-privilege access | User + implementation agent | Development token acquired over trusted HTTPS; UI settings and task-list requests returned HTTP 200 and advertised all three required effective permissions on 2026-10-10 |
| Blocked | Upgrade Paperless beyond the upload-authorization vulnerability | User | Deployed 2.20.4 is affected by GHSA-jqwv-hx7q-fxh3; upstream fix begins at 2.20.6 |
| Not started | Validate the Paperless task-list contract and retention | User + implementation agent | Synthetic upload exposes filename, creation time, and terminal state long enough for reconciliation |
| Not started | Observe representative printer writes | Implementation agent | Dated non-sensitive evidence describes open/write/rename behaviour and timestamp resolution |
| Not started | Validate same-filesystem atomic rename and permissions on intended NFS mount | Implementation agent | Safe synthetic fixture moves successfully under deployment UID/GID |
| Not started | Run synthetic PDF and image end-to-end acceptance | User + implementation agent | Both reach `SUCCESS`; source archives only afterward; no secrets or sensitive names logged |
| Not started | Exercise Paperless outage, NFS outage, restart, ambiguous response, and task failure | Implementation agent | Dated observations show polling recovery and retained/quarantined sources |

## Deployment decisions still required

| State | Task | Owner | Exit evidence |
| --- | --- | --- | --- |
| Complete | Choose archive retention for the test deployment | User | `Move` to the separately mounted importer archive; other mappings may select `Delete` or `Retain` |
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
- 2026-10-10, automated: `dotnet test PaperlessImporter.slnx --configuration Release` passed all 14 tests after adding configurable success dispositions and cross-mount terminal transfer.
- 2026-10-10, development API: login-to-token exchange succeeded over trusted HTTPS and `/api/profile/` succeeded. The profile reports neither document-add nor PaperlessTask-view permission; a task-list request returned HTTP 403 without inspecting task data.
- 2026-10-10, automated: `dotnet test PaperlessImporter.slnx --configuration Release` passed all 19 tests after adding the mandatory startup permission and task-endpoint preflight, including an effective-access HTTP 403 case.
- 2026-10-10, development API: `/api/profile/` on Paperless-ngx 2.20.4 was verified not to expose permissions. `/api/ui_settings/` is the installed-version contract for effective permissions and itself requires `documents.view_uisettings`; the startup preflight was corrected accordingly.
- 2026-10-10, live executable: a real process invocation exposed that source-generated binding did not populate `init`-only deployment options. Configuration-bound properties were changed to setters, host-level binding coverage was added, option failures were redacted, and default HTTP client URL logging was suppressed.
- 2026-10-10, automated: Release build passed with zero warnings and all 20 tests passed after adding host-level Paperless and directory-mapping configuration coverage.
- 2026-10-10, development API/live executable: Docker-style environment configuration reached the real Paperless preflight. The process exited 78 before filesystem access because UI-settings permission introspection returned 403, and captured output contained no endpoint URL.
- 2026-10-10, primary-source security review: the installed Paperless-ngx 2.20.4 upload view requires authentication but not `documents.add_document`; upstream advisory GHSA-jqwv-hx7q-fxh3 identifies 2.20.6 as the first patched release.
- 2026-10-10, development API: `/api/ui_settings/` and a one-item `/api/tasks/` probe both returned HTTP 200. The effective permission list contained `add_document`, `view_paperlesstask`, and `view_uisettings`; no task data, endpoint, token, or document metadata was recorded.
- 2026-10-10, GitHub Actions CI: all 20 Release tests passed and the Dockerfile built successfully for Linux AMD64 and ARM64.
- 2026-10-10, GitHub Actions release: tag `v0.1.0` published GHCR tags `0.1.0`, `0.1`, `0`, and `latest` as one multi-architecture manifest with SBOM and maximum-level provenance attestations; the GitHub release was published successfully.

## Recommended next action

Schedule the Paperless upgrade to a patched release, then establish a container image and mounted-secret delivery path for a synthetic-only deployment. Do not change Paperless or its native consumer without a separate explicit request.
