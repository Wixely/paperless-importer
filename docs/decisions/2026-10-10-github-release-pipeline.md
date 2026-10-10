# GitHub release and container publishing

- Status: Accepted
- Date: 2026-10-10
- Source: User direction to publish under the Wixely account and build Docker images with GitHub Actions

## Context

The development workstation has no Docker engine or Linux NativeAOT toolchain. A reproducible remote build is needed before Portainer can deploy the importer. The repository contains deployment-sensitive examples but no deployment credentials, internal endpoints, or real documents.

## Decision

Host the project in a private GitHub repository named `paperless-importer` under the Wixely account. Keep the product display name **Paperless Importer** in documentation and release titles.

GitHub Actions performs two separate workflows:

- CI restores, builds, and tests the .NET solution, then builds the container for Linux AMD64 and ARM64 without publishing it.
- Tags using semantic versioning publish versioned and `latest` multi-architecture images to GHCR, attach SBOM and maximum-level provenance attestations, and create a GitHub release only after tests and image publication succeed.

The Dockerfile uses Microsoft's NativeAOT SDK and its chiseled runtime-dependencies image for NativeAOT applications. Builds contain no Paperless endpoint, token, CA material, NFS configuration, or document fixtures. Workflow permissions are declared per job, and Dependabot checks NuGet and GitHub Actions dependencies weekly.

## Alternatives

- Build only on the development workstation: rejected because the required container engine and Linux toolchain are unavailable.
- Publish a single AMD64 image: rejected because the official base images and application support ARM64 without changing runtime behaviour.
- Put deployment secrets in GitHub Actions: rejected because image production does not need access to Paperless or NFS.
- Publish from every `main` commit: rejected for releases because immutable semantic-version tags provide a clearer deployment and rollback boundary.

## Consequences

- GHCR becomes the container delivery path for Portainer.
- The private package requires registry authentication until repository/package visibility is deliberately changed.
- A release tag is created only after the complete outgoing repository history and metadata have been reviewed.
- Live NFS and Paperless acceptance remains separate from build and release automation.

## Validation

The local Release test suite and workflow syntax checks must pass before the first push. The first GitHub CI and release runs must prove both target architectures build and that the resulting GHCR manifest, attestations, and GitHub release exist before the image is deployed.
