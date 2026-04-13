# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY MarsRoverPhotos/MarsRoverPhotos.csproj MarsRoverPhotos/
RUN dotnet restore MarsRoverPhotos/MarsRoverPhotos.csproj

COPY MarsRoverPhotos/ MarsRoverPhotos/
RUN dotnet publish MarsRoverPhotos/MarsRoverPhotos.csproj \
    -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .
COPY MarsRoverPhotos/dates.txt .

# NASA API key should be injected via environment variable at runtime:
# docker run -e NASA_API_KEY=your_key_here ...
ENV NASA_API_KEY=DEMO_KEY
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "MarsRoverPhotos.dll"]
