FROM mcr.microsoft.com/dotnet/sdk:10.0.300-noble-aot AS build
ARG TARGETARCH
ARG VERSION=0.0.0-dev
WORKDIR /src
COPY NuGet.config Directory.Build.props Directory.Packages.props global.json PaperlessImporter.slnx ./
COPY src/PaperlessImporter/PaperlessImporter.csproj src/PaperlessImporter/
RUN case "$TARGETARCH" in \
      amd64) runtime_id=linux-x64 ;; \
      arm64) runtime_id=linux-arm64 ;; \
      *) echo "Unsupported target architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac \
    && dotnet restore src/PaperlessImporter/PaperlessImporter.csproj -r "$runtime_id"
COPY src/PaperlessImporter/ src/PaperlessImporter/
RUN case "$TARGETARCH" in \
      amd64) runtime_id=linux-x64 ;; \
      arm64) runtime_id=linux-arm64 ;; \
      *) echo "Unsupported target architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac \
    && dotnet publish src/PaperlessImporter/PaperlessImporter.csproj \
      -c Release -r "$runtime_id" --self-contained true --no-restore \
      -p:Version="$VERSION" \
      -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0.12-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app/publish/ ./
USER $APP_UID
ENTRYPOINT ["/app/paperless-importer"]
HEALTHCHECK --interval=30s --timeout=10s --start-period=45s --retries=3 \
    CMD ["/app/paperless-importer", "--health-ready"]
