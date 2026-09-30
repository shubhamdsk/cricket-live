# Cricket Live API
#
# Host-agnostic on purpose. Nothing in here names Render, Railway, Fly or Azure, because the
# choice of host is not made yet and a container that only runs on one of them would make it
# harder to change.
#
# Build from the repository root so the context includes the whole backend folder:
#
#   docker build -f backend/Dockerfile -t cricket-live-api .

# ---- build ----------------------------------------------------------------------------------

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first, and restore before the rest of the source is copied. Dependencies change
# far less often than code does, so this layer is reused across almost every build.
COPY backend/src/CricketLive.Api/CricketLive.Api.csproj src/CricketLive.Api/
COPY backend/src/CricketLive.Application/CricketLive.Application.csproj src/CricketLive.Application/
COPY backend/src/CricketLive.Domain/CricketLive.Domain.csproj src/CricketLive.Domain/
COPY backend/src/CricketLive.Infrastructure/CricketLive.Infrastructure.csproj src/CricketLive.Infrastructure/

# The API project alone, not the solution: the solution includes the test projects, and their
# dependencies are not wanted in a production image.
RUN dotnet restore src/CricketLive.Api/CricketLive.Api.csproj

COPY backend/src/ src/

RUN dotnet publish src/CricketLive.Api/CricketLive.Api.csproj \
    -c Release \
    -o /app \
    --no-restore

# ---- run ------------------------------------------------------------------------------------

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# The archive is a SQLite file, and a container filesystem does not survive a redeploy. Mount a
# volume here or the match history starts empty every time the service restarts — which is not a
# crash, just a site that has quietly forgotten last week. See docs/deployment.md.
ENV ConnectionStrings__Archive="Data Source=/data/cricket-live.db"

# In-container HTTPS is not wanted: every host worth using terminates TLS at its edge and
# forwards plain HTTP, and a self-signed certificate in here would only break that. The app still
# sends HSTS and redirects outside development, which is the part that matters to a browser.
ENV ASPNETCORE_URLS="http://+:8080"
ENV ASPNETCORE_ENVIRONMENT="Production"

# Cuts the image down by leaving out every culture's formatting data. Safe here because nothing
# formats for a culture: dates go out as ISO 8601 and the browser decides how to show them.
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

WORKDIR /app
COPY --from=build /app .

# The image ships with a non-root `app` user. The data directory has to exist and be owned before
# the switch, because SQLite needs to create the database file and its -wal and -shm siblings.
RUN mkdir -p /data && chown -R app:app /data
USER app

VOLUME ["/data"]
EXPOSE 8080

# Deliberately no HEALTHCHECK instruction. The .NET runtime images do not ship curl or wget, so
# the usual one-liner would fail for the wrong reason and mark a healthy container unhealthy.
# Point the host's own probe at these instead:
#
#   liveness   GET /api/health/live    is the process serving? nothing else, so a dependency
#                                      being down never restarts a process that is working
#   readiness  GET /api/health/ready   the archive and the request allowance
#
# Use liveness for restarts and readiness for traffic. Never restart on readiness: the allowance
# refills on a clock, and a restart does not hurry it along.

ENTRYPOINT ["dotnet", "CricketLive.Api.dll"]
