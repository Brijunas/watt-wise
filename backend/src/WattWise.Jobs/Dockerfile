# Build from the repo root: docker build -f backend/src/WattWise.Jobs/Dockerfile [--target dev] -t wattwise-jobs .
# Stages, base images and the health check: deploy/docs/images.md.

# restore: the SDK, the packages of the project closure, the folders the later stages write to.
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-resolute AS restore
ENV NUGET_PACKAGES=/nuget
RUN mkdir /src /nuget /out && chown $APP_UID:$APP_UID /src /nuget /out
USER $APP_UID
WORKDIR /src
COPY --chown=$APP_UID global.json .editorconfig ./
COPY --chown=$APP_UID backend/Directory.Build.props backend/Directory.Packages.props backend/nuget.config backend/.editorconfig backend/.globalconfig backend/
COPY --chown=$APP_UID backend/src/WattWise.Jobs/WattWise.Jobs.csproj backend/src/WattWise.Jobs/
COPY --chown=$APP_UID backend/src/WattWise.Application/WattWise.Application.csproj backend/src/WattWise.Application/
COPY --chown=$APP_UID backend/src/WattWise.Domain/WattWise.Domain.csproj backend/src/WattWise.Domain/
COPY --chown=$APP_UID backend/src/WattWise.Infrastructure/WattWise.Infrastructure.csproj backend/src/WattWise.Infrastructure/
RUN dotnet restore backend/src/WattWise.Jobs/WattWise.Jobs.csproj

# dev: no source copied; Compose bind-mounts the tree at /src.
FROM restore AS dev
ENTRYPOINT ["dotnet", "watch", "--project", "backend/src/WattWise.Jobs/WattWise.Jobs.csproj", "run", "--no-launch-profile"]

FROM restore AS build
COPY --chown=$APP_UID backend/src/ backend/src/
RUN dotnet publish backend/src/WattWise.Jobs/WattWise.Jobs.csproj -c Release -o /out --no-restore

# runtime: the last stage, so a plain docker build gives it.
FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-resolute AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build --chown=root:root --chmod=a=rX /out .
USER $APP_UID
# The first ASPNETCORE_URLS entry with a wildcard host turned into localhost, else the first HTTP port.
HEALTHCHECK --interval=30s --timeout=3s --start-period=30s --start-interval=2s --retries=3 \
    CMD port="${ASPNETCORE_HTTP_PORTS:-8080}"; url="${ASPNETCORE_URLS%%;*}"; \
        url="$(printf '%s' "${url:-http://localhost:${port%%;*}}" | sed -E 's#//(\+|\*|0\.0\.0\.0|\[::\])(:|/|$)#//localhost\2#')"; \
        curl -fsS --max-time 2 "${url%/}/health" > /dev/null
ENTRYPOINT ["dotnet", "WattWise.Jobs.dll"]
