# Build context: the repository root, filtered by backend/Dockerfile.dockerignore.
#   docker build -f backend/Dockerfile --target runtime -t helpdesk-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble AS build
WORKDIR /src

# Project files first, so the restore layer is reused until a dependency changes.
COPY global.json ./
COPY backend/Directory.Build.props backend/
COPY backend/src/Helpdesk.Domain/Helpdesk.Domain.csproj backend/src/Helpdesk.Domain/
COPY backend/src/Helpdesk.Application/Helpdesk.Application.csproj backend/src/Helpdesk.Application/
COPY backend/src/Helpdesk.Infrastructure/Helpdesk.Infrastructure.csproj backend/src/Helpdesk.Infrastructure/
COPY backend/src/Helpdesk.Api/Helpdesk.Api.csproj backend/src/Helpdesk.Api/
RUN dotnet restore backend/src/Helpdesk.Api/Helpdesk.Api.csproj

COPY backend/src/ backend/src/
RUN dotnet publish backend/src/Helpdesk.Api/Helpdesk.Api.csproj \
    --configuration Release --no-restore --output /app/publish -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Non-root user provided by the base image; it listens on ASPNETCORE_HTTP_PORTS (8080).
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Helpdesk.Api.dll"]
