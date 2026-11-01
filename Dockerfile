# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only what's needed to restore first, to maximize layer caching.
COPY global.json ./
COPY BombRMan.slnx ./
COPY BombRMan.Core/BombRMan.Core.csproj BombRMan.Core/
COPY BombRMan.Core.Tests/BombRMan.Core.Tests.csproj BombRMan.Core.Tests/
RUN dotnet restore BombRMan.Core/BombRMan.Core.csproj

# Now copy the rest of the source and publish.
COPY . .
RUN dotnet publish BombRMan.Core/BombRMan.Core.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Run as the non-root user baked into the base image.
USER app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "BombRMan.Core.dll"]
