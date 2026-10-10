# Mandatory startup permission gate

- Status: Accepted
- Date: 2026-10-10
- Source: User direction after development credential verification

## Context

The importer relies on two Paperless capabilities: submitting a document and viewing Paperless task records. Running with upload access but without task access would defeat duplicate avoidance and restart recovery. Running without upload access would claim files that cannot make forward progress. The first supplied development account had neither global permission and its task-list request returned HTTP 403.

## Decision

Before starting the hosted worker, every normal process invocation must:

1. Authenticate to `/api/profile/` and require `documents.add_document` (**Document: Add**) and `documents.view_paperlesstask` (**PaperlessTask: View**). A superuser implicitly satisfies this check, although a least-privilege regular account is required operationally.
2. Request `/api/tasks/?page_size=1` and require a successful response, validating the effective endpoint policy as well as the advertised permission set.

The worker does not enumerate directories or claim files until both checks succeed. A permission, authentication rejection, or endpoint-policy failure exits with code 78. A network, token-file availability, or response-contract failure exits with code 69. Startup messages may name permission codes and HTTP status codes but never tokens, URLs, response bodies, filenames, or document content.

Health-check-only invocations bypass the remote preflight so health inspection does not create a second dependency request or mask the running process state.

## Alternatives

- Log a warning and continue: rejected because it permits unsafe partial operation.
- Discover missing permissions after the first file arrives: rejected because a file may already have been claimed.
- Verify upload by submitting a fixture at startup: rejected because startup must not create documents or require destructive cleanup.

## Consequences

- Docker, systemd, and Windows Service starts fail visibly and retry according to their service policy until configuration and permissions are corrected.
- A Paperless outage during initial startup prevents the worker from starting. Once startup succeeds, the existing poll-loop error boundary handles later outages without terminating the process.
- Permission revocation after startup is detected on the next task query; the runtime loop remains resilient and does not claim new input while the global task query fails.

## Validation

The 2026-10-10 Release test run passed 19 tests, including required-permission parsing, complete missing-permission reporting, successful profile-plus-task probing, failure before the task probe when a permission is absent, and effective task-access rejection with HTTP 403.
