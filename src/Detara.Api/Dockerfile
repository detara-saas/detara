FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet restore src/Detara.Api/Detara.Api.csproj
RUN dotnet restore tools/Detara.PlatformBootstrap/Detara.PlatformBootstrap.csproj
RUN dotnet publish src/Detara.Api/Detara.Api.csproj -c Release -o /app --no-restore
RUN dotnet publish tools/Detara.PlatformBootstrap/Detara.PlatformBootstrap.csproj -c Release -o /bootstrap --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install --no-install-recommends -y curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/detara/data-protection-keys \
    && chown -R "$APP_UID:$APP_UID" /var/lib/detara
COPY --from=build /app .
COPY --from=build /bootstrap /tools/platform-bootstrap
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Detara.Api.dll"]
