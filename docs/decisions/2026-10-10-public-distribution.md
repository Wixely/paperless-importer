# Public source and container distribution

- Status: Accepted
- Date: 2026-10-10
- Source: User direction

## Context

The initial GitHub repository and GHCR package were private. The deployment Docker host had no GHCR registry credential, so it could not pull the released image even though other public Wixely images were already deployable.

## Decision

Publish the Paperless Importer repository and its GHCR container package publicly under the Wixely account. Repository history was reviewed before the visibility change for credentials, personal data, private infrastructure details, local paths, binaries, and identifying metadata.

No software license is granted merely by making the source visible. Runtime Paperless credentials, endpoints, CA material, NFS configuration, and document data remain deployment-only secrets and must never be committed or embedded in an image.

## Consequences

- Anyone can inspect and clone the source repository.
- Once the GHCR package owner changes the package to public, Docker hosts can pull it anonymously without a Portainer registry credential.
- GitHub documents that a public package cannot subsequently be made private.
- Public distribution makes secret and metadata review a required release check.

## Validation

The repository returned public visibility through GitHub's repository API on 2026-10-10. After the separate package visibility change, the deployment Docker host pulled release `v0.1.0` anonymously and Portainer started the published image successfully.
