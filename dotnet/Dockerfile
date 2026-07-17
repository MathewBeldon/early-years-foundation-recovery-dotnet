# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS restore
WORKDIR /src
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY dotnet/EarlyYears.sln dotnet/
COPY dotnet/src/EarlyYears.Domain/EarlyYears.Domain.csproj dotnet/src/EarlyYears.Domain/
COPY dotnet/src/EarlyYears.Domain/packages.lock.json dotnet/src/EarlyYears.Domain/
COPY dotnet/src/EarlyYears.Application/EarlyYears.Application.csproj dotnet/src/EarlyYears.Application/
COPY dotnet/src/EarlyYears.Application/packages.lock.json dotnet/src/EarlyYears.Application/
COPY dotnet/src/EarlyYears.Infrastructure/EarlyYears.Infrastructure.csproj dotnet/src/EarlyYears.Infrastructure/
COPY dotnet/src/EarlyYears.Infrastructure/packages.lock.json dotnet/src/EarlyYears.Infrastructure/
COPY dotnet/src/EarlyYears.Web/EarlyYears.Web.csproj dotnet/src/EarlyYears.Web/
COPY dotnet/src/EarlyYears.Web/packages.lock.json dotnet/src/EarlyYears.Web/
COPY dotnet/tests/EarlyYears.UnitTests/EarlyYears.UnitTests.csproj dotnet/tests/EarlyYears.UnitTests/
COPY dotnet/tests/EarlyYears.UnitTests/packages.lock.json dotnet/tests/EarlyYears.UnitTests/
COPY dotnet/tests/EarlyYears.IntegrationTests/EarlyYears.IntegrationTests.csproj dotnet/tests/EarlyYears.IntegrationTests/
COPY dotnet/tests/EarlyYears.IntegrationTests/packages.lock.json dotnet/tests/EarlyYears.IntegrationTests/
COPY gateway/MigrationGateway.csproj gateway/
COPY gateway/packages.lock.json gateway/
RUN dotnet restore dotnet/EarlyYears.sln --locked-mode

FROM restore AS source
COPY dotnet/ dotnet/
COPY gateway/ gateway/

FROM source AS development
WORKDIR /src/dotnet
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_USE_POLLING_FILE_WATCHER=1 \
    DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1
EXPOSE 8080
CMD ["dotnet", "watch", "--project", "src/EarlyYears.Web/EarlyYears.Web.csproj", "run", "--no-launch-profile"]

FROM source AS test
RUN dotnet test dotnet/EarlyYears.sln \
    --configuration Release \
    --no-restore \
    --logger trx \
    --results-directory /test-results

FROM source AS publish
RUN dotnet publish dotnet/src/EarlyYears.Web/EarlyYears.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.9 AS runtime
WORKDIR /app
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "EarlyYears.Web.dll"]
