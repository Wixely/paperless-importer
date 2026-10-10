FROM mcr.microsoft.com/dotnet/sdk:10.0.300 AS build
WORKDIR /src
COPY NuGet.config Directory.Build.props Directory.Packages.props global.json PaperlessImporter.slnx ./
COPY src/PaperlessImporter/PaperlessImporter.csproj src/PaperlessImporter/
RUN dotnet restore src/PaperlessImporter/PaperlessImporter.csproj -r linux-x64
COPY src/PaperlessImporter/ src/PaperlessImporter/
RUN dotnet publish src/PaperlessImporter/PaperlessImporter.csproj \
    -c Release -r linux-x64 --self-contained true --no-restore \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0.12-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app/publish/ ./
USER $APP_UID
ENTRYPOINT ["/app/paperless-importer"]
HEALTHCHECK --interval=30s --timeout=10s --start-period=45s --retries=3 \
    CMD ["/app/paperless-importer", "--health-ready"]
