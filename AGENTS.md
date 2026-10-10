# Instructions for agents

This repository is the implementation home for Paperless Importer, a private service that moves completed printer scans from an NFS-backed inbox into Paperless-ngx through its supported HTTP API.

Before making changes:

1. Read `README.md`, `docs/work-queue.md`, and every record in `docs/decisions/`.
2. Check `git status` and preserve unrelated user changes.
3. Consult the shared plan repository's confirmed development, source-control, security/privacy, and planning preferences when it is available.
4. Treat Paperless API, printer-write, NFS, package-compatibility, and deployment behaviour as assumptions until supported by current primary documentation or dated acceptance evidence.
5. Use only synthetic or explicitly approved non-sensitive documents in development and acceptance testing.

## Development preferences

- Use C# on .NET 10 with a top-level executable entry point.
- Implement a Worker Service that runs interactively, as a Windows Service, under systemd, and in Docker.
- Prefer a self-contained, single-file NativeAOT publish when the selected libraries and verified runtime behaviour permit it. Document any exception.
- Keep the importer stateless: do not add a database or durable application state unless the user revisits the decision. Use Paperless task records plus the claimed filesystem layout for duplicate avoidance and restart recovery.
- Support one or more Docker-configured directory mappings. Every input has its own processing, successful-output, and quarantine directory; the application treats host/NFS mounts as ordinary filesystem paths.
- Do not introduce Python, Node.js, Tailwind, or technology that depends on them without explicit user approval.
- Keep repository scripts compatible with Windows PowerShell 5.1.
- Add working repository-local VS Code launch and build tasks when runnable code is bootstrapped.
- Treat Windows as the primary platform and Linux as the secondary platform. macOS support is not required.
- Add tests as behaviour is implemented. Verify the published application and container before making compatibility or reliability claims.

## Reliability and security boundaries

- Never commit or log Paperless tokens, document contents, sensitive filenames, internal certificate material, production URLs, private infrastructure details, or real scanned documents.
- Load credentials and CA material at deployment time from mounted files or secrets. Never disable TLS certificate validation.
- Keep the poll loop behind an error boundary. A transient NFS, filesystem, database, or Paperless failure must not silently stop future scans.
- Establish file readiness from stable size and modification time across multiple polls, with a configurable minimum age.
- Treat open, locked, changing, or temporarily unavailable source files as deferred work. Leave them untouched and retry on a later poll.
- Isolate polling failures by directory mapping so one unavailable NFS-backed location does not prevent healthy locations from progressing.
- Claim files using an atomic move into a uniquely named processing directory on the same filesystem before upload. Preserve the original filename inside the claim so a restart can reconcile it against Paperless tasks.
- Do not dispose of a source file after HTTP acceptance. Wait until the corresponding Paperless task reports `SUCCESS`.
- Preserve or quarantine failed and uncertain imports. Before any upload or retry, query Paperless tasks for a matching recent filename; document that this stateless strategy depends on task visibility and retention.
- Expose liveness and readiness separately. Readiness must reflect recent successful scans and forward progress, not merely a running process.
- Do not expose an HTTP API or MCP server unless a documented operational consumer and security model justify it.

## Maintaining the plan

- Keep `docs/work-queue.md` task state, owners, and evidence current.
- Update `README.md` when scope, assumptions, risks, success criteria, or next actions change.
- Preserve significant decisions in `docs/decisions/`; supersede old decisions rather than silently rewriting history.
- Record test commands and observed results, distinguishing automated, synthetic, staging, and live acceptance.
- End implementation handoffs with what remains and the recommended next action.

Do not create a remote, push, deploy, access production Paperless, alter the NFS share, or change the existing Paperless consumer unless the user explicitly requests that action. Before any future push, review the complete outgoing range and all metadata for secrets, personal data, local paths, internal endpoints, binaries, and identifying information.
