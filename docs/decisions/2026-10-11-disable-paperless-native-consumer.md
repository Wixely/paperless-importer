# Disable the Paperless native directory consumer

- Status: Accepted
- Date: 2026-10-11
- Source: User direction and Paperless-ngx 2.20.4 documentation

## Context

Paperless Importer now watches the mounted consume root. Leaving Paperless-ngx's native polling consumer enabled against the same directory would create two competing consumers, so either process could move a source before the other claims it.

## Decision

Set `PAPERLESS_CONSUMER_DISABLE: "true"` in the Paperless Docker service and remove `PAPERLESS_CONSUMER_POLLING`. Paperless Importer is the exclusive filesystem consumer. Paperless-ngx continues to receive documents through its supported HTTP API and runs its normal task workers.

## Consequences

- Files placed in the consume directory are discovered only by Paperless Importer.
- Paperless restarts no longer provide a fallback scan of that directory.
- Importer liveness/readiness and quarantine monitoring become operational requirements.
- Rollback removes `PAPERLESS_CONSUMER_DISABLE`, restores the prior polling setting, and redeploys Paperless only after stopping Paperless Importer to prevent a race.

## Validation

The Portainer stack redeployed with the disable setting present and the polling setting absent. Paperless returned healthy, its task API returned HTTP 200, and the importer recovered from the expected brief API interruption and resumed submitting claimed documents.
