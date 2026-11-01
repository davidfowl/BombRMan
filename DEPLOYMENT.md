# Deployment

BombRMan ships as a single ASP.NET Core process that serves both the
SignalR game hub and the static client from `wwwroot`. The easiest way to
run it anywhere Docker is available is the provided `Dockerfile`.

## Build the image

From the repository root:

```bash
docker build -t bombrman .
```

This uses a multi-stage build: the SDK image (matching the version pinned
in [`global.json`](global.json)) restores and publishes
`BombRMan.Core/BombRMan.Core.csproj` in Release configuration, and the
final image is based on the much smaller ASP.NET runtime image.

## Run the container

```bash
docker run --rm -p 8080:8080 bombrman
```

The container listens on port `8080` inside the container
(`ASPNETCORE_URLS=http://+:8080`); `-p 8080:8080` maps it to port `8080` on
the host. Pick a different host port if `8080` is already in use, e.g.
`-p 5000:8080`.

Once the container is running, open `http://localhost:8080` in two or more
browser tabs/windows to play a local multiplayer round — at least 2
connected players are required for a round to start, same as running the
server directly with `dotnet run`.

## Verifying a deployment (manual smoke test)

1. `docker build -t bombrman .`
2. `docker run --rm -p 8080:8080 bombrman`
3. Open `http://localhost:8080` in two separate browser tabs.
4. Confirm both players appear on the grid, movement/bomb placement works,
   an explosion eliminates a player, and the round resets — the same
   acceptance criteria used for local (non-containerized) runs.

## Configuration notes

- The image runs as the non-root `app` user baked into the
  `mcr.microsoft.com/dotnet/aspnet` base image.
- `ASPNETCORE_ENVIRONMENT` defaults to `Production` in the container. Pass
  `-e ASPNETCORE_ENVIRONMENT=Development` to `docker run` if you want
  development-time behavior (e.g. detailed SignalR errors).
- There is no persistent state or external dependency (database, cache,
  etc.) to configure — a single container is a complete, self-contained
  deployment of the game server.

## Known gaps / out of scope for this milestone

- No orchestration (Kubernetes manifests, Compose file, etc.) is provided
  yet; a .NET Aspire AppHost for one-command local multiplayer playtesting
  is tracked separately (see the roadmap tracking issue).
- No production TLS termination is configured; put the container behind a
  reverse proxy or load balancer that terminates TLS if deploying publicly.
