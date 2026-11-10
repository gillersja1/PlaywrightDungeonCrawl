# Build stage: restore and publish with the full SDK.
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

# Restore first, so this layer is cached until the project file changes.
COPY src/PlaywrightDungeonCrawl/PlaywrightDungeonCrawl.csproj src/PlaywrightDungeonCrawl/
RUN dotnet restore src/PlaywrightDungeonCrawl/PlaywrightDungeonCrawl.csproj

COPY src/ src/
RUN dotnet publish src/PlaywrightDungeonCrawl/PlaywrightDungeonCrawl.csproj -c Release -o /app --no-restore

# Runtime stage: only the ASP.NET runtime and the published site.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Listens on 8080 unless the host sets PORT (Render does).
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Run as the image's built-in non-root user.
USER $APP_UID
ENTRYPOINT ["dotnet", "PlaywrightDungeonCrawl.dll"]
