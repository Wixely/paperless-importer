# Effective permission introspection

- Status: Accepted; supersedes the endpoint and permission-list details in `2026-10-10-startup-permission-gate.md`
- Date: 2026-10-10
- Source: Live development API acceptance and Paperless-ngx 2.20.4 primary source

## Context

The initial startup gate assumed `/api/profile/` returned the current account's global permission list. Live acceptance proved that Paperless-ngx 2.20.4's profile response contains account-profile fields but no permissions. Its `/api/ui_settings/` response contains the effective permissions used by the Paperless web client, with application prefixes removed, but access to that response requires `UISettings: View`.

The development token can now read `/api/tasks/`, but `/api/ui_settings/` returns HTTP 403. Task access alone cannot prove document-add permission. Paperless-ngx versions before 2.20.6 also had an upload authorization gap, so probing the upload route without creating a document is not a trustworthy permission test for the installed 2.20.4 server.

## Decision

The fail-closed startup preflight reads `/api/ui_settings/` and requires its effective permission list to contain:

- `add_document` (`documents.add_document`, **Document: Add**)
- `view_paperlesstask` (`documents.view_paperlesstask`, **PaperlessTask: View**)
- `view_uisettings` (`documents.view_uisettings`, **UISettings: View**)

It then probes `/api/tasks/?page_size=1`. No filesystem enumeration or claim begins until both requests succeed. A superuser response is accepted by the code, but deployment documentation requires a least-privilege regular account.

## Consequences

- The service account needs one introspection permission beyond its document workflow permissions.
- Startup is compatible with the verified Paperless-ngx 2.20.4 response shape instead of relying on a nonexistent profile field.
- The three permission names must be kept visible in deployment documentation.
- Deployment remains blocked until the development account can read UI settings and advertises every required permission.

## Validation

Primary source for Paperless-ngx 2.20.4 was inspected on 2026-10-10: `ProfileSerializer` omits permissions; `UiSettingsView` returns `user.get_all_permissions()` with application prefixes removed and is guarded by model permissions. Live requests confirmed the profile shape, task HTTP 200, and UI-settings HTTP 403 for the current development token. Automated preflight tests cover the verified UI-settings response shape and effective task-access rejection.
