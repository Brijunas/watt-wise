# Build from the repo root: docker build -f backend/src/WattWise.Cli/Dockerfile [--target dev] -t wattwise-cli .
# Stages and base images: deploy/docs/images.md.

# restore: the SDK, the packages of the project closure, the folders the later stages write to.
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-resolute AS restore
# bin and obj go to /artifacts, so nothing is written into /src (deploy/docs/images.md#build-output).
ENV NUGET_PACKAGES=/nuget \
    UseArtifactsOutput=true \
    ArtifactsPath=/artifacts
RUN mkdir /src /nuget /out /artifacts && chown $APP_UID:$APP_UID /src /nuget /out /artifacts
USER $APP_UID
WORKDIR /src
COPY --chown=$APP_UID global.json .editorconfig ./
COPY --chown=$APP_UID backend/Directory.Build.props backend/Directory.Packages.props backend/nuget.config backend/.editorconfig backend/.globalconfig backend/
COPY --chown=$APP_UID backend/src/WattWise.Cli/WattWise.Cli.csproj backend/src/WattWise.Cli/
COPY --chown=$APP_UID backend/src/WattWise.Application/WattWise.Application.csproj backend/src/WattWise.Application/
COPY --chown=$APP_UID backend/src/WattWise.Domain/WattWise.Domain.csproj backend/src/WattWise.Domain/
COPY --chown=$APP_UID backend/src/WattWise.Infrastructure/WattWise.Infrastructure.csproj backend/src/WattWise.Infrastructure/
RUN dotnet restore backend/src/WattWise.Cli/WattWise.Cli.csproj

# dev: no source copied; mount the tree at /src to run it (the Development stack runs the runtime target as its one-shot `cli` service).
FROM restore AS dev
ENTRYPOINT ["dotnet", "run", "--project", "backend/src/WattWise.Cli/WattWise.Cli.csproj", "--no-launch-profile", "--"]

FROM restore AS build
COPY --chown=$APP_UID backend/src/ backend/src/
RUN dotnet publish backend/src/WattWise.Cli/WattWise.Cli.csproj -c Release -o /out --no-restore

# runtime: the last stage, so a plain docker build gives it. The Cli needs only Microsoft.NETCore.App.
FROM mcr.microsoft.com/dotnet/runtime:11.0.0-resolute AS runtime
WORKDIR /app
COPY --from=build --chown=root:root --chmod=a=rX /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "WattWise.Cli.dll"]
