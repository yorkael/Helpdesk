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
COPY dotnet-tools.json ./
RUN dotnet tool restore

COPY backend/src/ backend/src/
RUN dotnet publish backend/src/Helpdesk.Api/Helpdesk.Api.csproj \
    --configuration Release --no-restore --output /app/publish -p:UseAppHost=false

FROM build AS bundle
# Building the bundle starts the API host, which validates its settings. The placeholders exist only
# for this command: no database is contacted and the random key is never written to a layer.
RUN ConnectionStrings__DefaultConnection="Host=unused" \
    Jwt__SigningKey="$(head -c 48 /dev/urandom | base64 -w 0)" \
    dotnet ef migrations bundle \
    --project backend/src/Helpdesk.Infrastructure --startup-project backend/src/Helpdesk.Api \
    --configuration Release --output /app/efbundle

# One-shot migrations runner: docker build -f backend/Dockerfile --target migrator -t helpdesk-migrator .
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble AS migrator
WORKDIR /app
COPY --from=bundle /app/efbundle .
# Running the bundle also starts the API host, which reads the non-secret Jwt settings from here.
COPY --from=build /src/backend/src/Helpdesk.Api/appsettings.json .
USER $APP_UID
ENTRYPOINT ["./efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Non-root user provided by the base image; it listens on ASPNETCORE_HTTP_PORTS (8080).
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Helpdesk.Api.dll"]
