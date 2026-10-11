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
| Complete | Publish source and release image publicly | User + implementation agent | Repository and GHCR package are public; anonymous image pull succeeded from the deployment Docker host on 2026-10-10 |

## Verification and acceptance

| State | Task | Owner | Exit evidence |
| --- | --- | --- | --- |
| Blocked | Publish the self-contained single-file NativeAOT executables | Implementation agent | Windows requires the missing Visual Studio C++ linker workload; Linux publish is intended to run in Docker; the workstation also reached zero free disk during a fallback publish on 2026-10-09 |
| Complete | Build and smoke-test the container | Implementation agent | Portainer pulled release `v0.1.0`; CA and filesystem gates passed and the importer reported healthy on 2026-10-10 |
| Complete | Supply a dedicated Paperless endpoint/token and verify least-privilege access | User + implementation agent | Development token acquired over trusted HTTPS; UI settings and task-list requests returned HTTP 200 and advertised all three required effective permissions on 2026-10-10 |
| Blocked | Upgrade Paperless beyond the upload-authorization vulnerability | User | Deployed 2.20.4 is affected by GHSA-jqwv-hx7q-fxh3; upstream fix begins at 2.20.6 |
| In progress | Validate the Paperless task-list contract and retention | User + implementation agent | Synthetic upload exposed filename, creation time, and terminal `FAILURE`; retention duration remains to be measured |
| Not started | Observe representative printer writes | Implementation agent | Dated non-sensitive evidence describes open/write/rename behaviour and timestamp resolution |
| Complete | Validate same-filesystem atomic rename and permissions on intended NFS mount | Implementation agent | Portainer startup gate passed create/write, atomic input-to-processing rename, archive write/rename, and quarantine write probes as the non-root runtime identity on 2026-10-10 |
| Blocked | Run synthetic PDF and image end-to-end acceptance | User + implementation agent | PDF reached API acceptance and terminal task visibility, but Paperless failed while storing the parsed document with an input/output error; source was quarantined correctly |
| In progress | Exercise Paperless outage, NFS outage, restart, ambiguous response, and task failure | Implementation agent | Live Paperless task failure was observed and the source was quarantined; remaining outage and restart cases are outstanding |

## Deployment decisions still required

| State | Task | Owner | Exit evidence |
| --- | --- | --- | --- |
| Complete | Choose archive retention for the test deployment | User | `Move` to the separately mounted importer archive; other mappings may select `Delete` or `Retain` |
| Complete | Define exclusive directory ownership and Paperless native-consumer cutover/rollback | User + implementation agent | Importer watches the consume root and Paperless's native directory consumer is disabled through `PAPERLESS_CONSUMER_DISABLE`; rollback removes that setting and restores the prior polling setting |
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
- 2026-10-10, distribution: the repository and GHCR package were public, and the deployment Docker host pulled `v0.1.0` anonymously.
- 2026-10-10, Portainer/NFS: the managed stack started without a custom network. Its fail-closed gate passed non-root create/write, atomic claim rename, archive write/rename, and quarantine write checks on the intended NFS-backed mounts. The pinned CA gate passed and the importer became healthy.
- 2026-10-10, synthetic acceptance: a generated non-sensitive PDF was claimed and accepted through the Paperless API. The matching task remained visible and reached terminal `FAILURE` because Paperless reported an input/output error while storing the parsed document. The importer detected the failure and moved the source to quarantine; no document contents, private endpoint, token, CA material, or infrastructure path were recorded.
- 2026-10-11, Portainer cutover: the configured importer input changed from the isolated staging subdirectory to the mounted consume root at the user's direction. Startup filesystem and CA gates passed, the worker remained healthy, and 18 existing documents were claimed and accepted by the Paperless API. Paperless task processing remained the terminal bottleneck; native-consumer exclusivity is not yet confirmed.
- 2026-10-11, Paperless cutover: the Portainer-managed Paperless stack replaced its native-consumer polling setting with `PAPERLESS_CONSUMER_DISABLE: "true"`. Paperless returned healthy after redeployment, its task API returned HTTP 200, and Paperless Importer recovered from the brief restart outage and resumed API submission. Existing database and OAuth secrets were preserved without being read into repository content.

## Recommended next action

Diagnose and repair Paperless's document-storage input/output failure, then repeat the synthetic PDF and image acceptance until both reach `SUCCESS` and move to archive. Schedule the Paperless upgrade to a patched release before production cutover. Do not change Paperless or its native consumer without a separate explicit request.
