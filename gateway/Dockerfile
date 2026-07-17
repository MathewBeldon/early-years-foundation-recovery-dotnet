# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS restore
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY gateway/MigrationGateway.csproj gateway/
COPY gateway/packages.lock.json gateway/
RUN dotnet restore gateway/MigrationGateway.csproj --locked-mode

FROM restore AS build
COPY gateway/ gateway/
RUN dotnet publish gateway/MigrationGateway.csproj \
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
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "MigrationGateway.dll"]
