# BombRMan

A multiplayer Bomberman clone: an ASP.NET Core + SignalR server-authoritative
game engine with a lightweight static HTML/JS client.

Grid-based movement, bomb placement with fuse timers, chain-reaction
explosions, destructible brick terrain, powerups (speed / extra bomb / bigger
blast), and server-driven round lifecycle (waiting → in progress → round
over → reset), with reconnect support that syncs a joining client to
whatever is already in progress.

See [`ARCHITECTURE.md`](ARCHITECTURE.md) for a full breakdown of the tech
stack, project layout, networking/game-loop design, and known gaps.

## Requirements

- .NET SDK matching [`global.json`](global.json) (10.0.301; `rollForward:
  latestFeature`, so any 10.0.3xx SDK works)

## Running it locally

### Option A: .NET Aspire AppHost (recommended)

The `BombRMan.AppHost` project orchestrates the game server via .NET Aspire and
surfaces it in the Aspire dashboard with health/resource status, logs, and traces.

```bash
dotnet run --project BombRMan.AppHost/BombRMan.AppHost.csproj
```

This prints a dashboard URL (e.g. `https://localhost:17266`, with a login
token) and starts the `bombrman-server` resource. Open the dashboard, click
into the `bombrman-server` resource to find its external HTTP endpoint, and
open that URL in two or more browser tabs/windows to play a local multiplayer
round (at least 2 connected players are required for a round to start).

If you have the [Aspire CLI](https://aspire.dev) installed, `aspire run` from
the repo root works too.

### Option B: run the server directly

```bash
# restore + build everything
dotnet build BombRMan.slnx

# run the game server (serves the SignalR hub and the static client together)
dotnet run --project BombRMan.Core/BombRMan.Core.csproj
```

Open the printed `http://localhost:…` URL in two or more browser tabs/windows
to play a local multiplayer round (at least 2 connected players are required
for a round to start).

VS Code users: `F5` launches the server using the provided
`.vscode/launch.json`, and `.vscode/tasks.json` exposes `build`, `publish`,
and `watch` tasks.

## Running the tests

```bash
dotnet test BombRMan.slnx
```

## Controls

Arrow keys to move, and the bomb-placement key defined in
`wwwroot/js/constants.js`/`game.js` to drop a bomb.

## Project status

This project already has a working server-authoritative game loop, full bomb/
explosion/powerup/round rules with unit test coverage, client-side input
prediction, and a .NET Aspire AppHost for one-command local orchestration.
The rest of the active roadmap (CI, Docker, diagnostics endpoint, client
modernization) is tracked in the repository's GitHub issues — see the pinned
roadmap tracking issue.

## Contributing

Issues and PRs welcome. Please make sure `dotnet build BombRMan.slnx` and
`dotnet test BombRMan.slnx` pass before opening a PR.
