# BombRMan Architecture

This document describes what exists in the repository today: the tech stack, the
runtime architecture, how to run the project locally, and known gaps.

## Tech stack

| Layer | Technology |
|---|---|
| Server | ASP.NET Core (`net10.0`, `Microsoft.NET.Sdk.Web`) |
| Realtime transport | ASP.NET Core SignalR (`/game` hub) |
| Client | Static HTML/CSS/vanilla JS (no framework dependency), `<canvas>` renderer, SignalR JS client (fetched via `libman.json`) |
| Tests | xUnit-style test project (`BombRMan.Core.Tests`), 65 tests covering game rules, networking glue, and diagnostics |
| Solution | `BombRMan.slnx` (new-style `.slnx` solution file), SDK pinned via `global.json` (10.0.301) |

There is currently **no .NET Aspire AppHost**, **no CI workflow**, **no Dockerfile**,
and **no top-level README** — see "Known gaps" below.

## Project layout

```
BombRMan.slnx                      # Solution (.slnx format)
global.json                        # Pins .NET SDK 10.0.301
BombRMan.Core/                     # ASP.NET Core server + static client assets
  Program.cs                       # Minimal hosting: SignalR + static file server
  Hubs/                            # All game logic and the SignalR hub
    GameServer.cs                  # SignalR Hub: connect/disconnect, SendKeys RPC
    GameState.cs                   # Server-authoritative game loop (singleton, background thread)
    Map.cs / Tile.cs               # Tile grid (GRASS/WALL/BRICK), destructible bricks
    Player.cs                      # Player movement/state machine, deterministic fixed-step update
    PlayerInputBuffer.cs           # Per-connection latest-input buffer (coalesces inputs, drops stale/duplicate ids)
    KeyboardState.cs / KeyboardStateConverter.cs / Keys.cs  # Compact bitfield input representation + custom JSON wire format
    Bomb.cs / Explosion.cs         # Bomb fuse / explosion lifetime state
    BombLogic.cs                   # Pure rules: placement validation, chain reactions, powerup application, round-over detection
    ExplosionCalculator.cs         # Pure rules: blast tile propagation (stops at WALL, consumes BRICK)
    Powerup.cs / PowerupType.cs    # SPEED / BOMB / EXPLOSION powerups
    RoundState.cs                  # WaitingForPlayers / InProgress / RoundOver
    Direction.cs / PlayerState.cs
  wwwroot/                         # Static client: index.htm, css, js (game.js, map.js, renderer.js, assman.js, bomber.js, main.js, constants.js, utils.js)
BombRMan.Core.Tests/                # xUnit tests: bomb logic, map, player, keyboard converter, input buffer, server integration, diagnostics
.vscode/launch.json, tasks.json     # F5 debug + build/watch/publish tasks for VS Code
```

## Runtime architecture

### Server-authoritative game loop

`GameState` is registered as a singleton and, on construction, starts a dedicated
background `Thread` (`BombRMan.GameLoop`) running a fixed-tick simulation loop
(`FPS = 60`). All mutable game state (bombs, explosions, powerups, player
positions) is only ever mutated from this single thread, so no additional locking
is needed around gameplay mutation — the loop drains per-connection input
buffers, advances players, ticks bombs/explosions, resolves chain reactions and
round-over conditions, then broadcasts deltas to clients over the SignalR hub
context.

### Networking: SignalR hub (`/game`)

`GameServer : Hub` is intentionally thin — it has no game logic of its own:

- `OnConnectedAsync`: sends the current map snapshot, assigns/initializes the
  player, and — critically — replays in-flight state (`initializeBombs`,
  `initializeExplosions`, `initializePowerups`, current `RoundState`) so a
  client that connects or reconnects *mid-round* is brought fully in sync
  rather than starting from a blank slate.
- `SendKeys(KeyboardState[] inputs)`: the only client→server RPC. Clients buffer
  and batch-send input frames; the server only keeps the latest accepted input
  per connection (`PlayerInputBuffer`), coalescing bursts and rejecting
  stale/duplicate input ids.
- `OnDisconnectedAsync`: removes the player and notifies everyone else.

Wire format: SignalR's JSON protocol with a custom `KeyboardStateConverter` that
packs the 8 input keys into/from a compact bitfield to keep input messages
small.

### Diagnostics: `GET /diagnostics`

A minimal-API endpoint (wired up in `Program.cs`) that returns a live JSON
snapshot of the single shared `GameState` instance: round state, every active
player's position/direction/stats, in-flight bombs/explosions/powerups, the
current (possibly brick-destroyed) map tile layout, and the same per-second
server counters broadcast over SignalR as `serverStats` (updates/sec,
inputs/sec, dropped inputs, tick overruns, queue depth). Built by
`GameState.GetDiagnosticsSnapshot()`; intended for debugging/observability, not
for driving gameplay. See `DiagnosticsEndpointTests` for an end-to-end example
(hits the endpoint over HTTP while a real round is in progress).

### Client-side prediction

`wwwroot/js/game.js` shows the client is not a pure dumb renderer: it tracks an
incrementing `inputId`, the last input id/time processed by the server
(`lastProcessed`/`lastProcessedTime`), and round-trip-time, which is the
scaffolding for **client-side prediction with server reconciliation** — a
classic authoritative-server netcode pattern. `Player.Update` on the server is
written as a pure, deterministic fixed-step function so the same logic can run
predictively on the client and be corrected by server state without drifting.

### Game rules (pure, unit-testable)

Gameplay rules are deliberately kept out of `GameState`/`GameServer` and placed
in static, side-effect-free helpers so they can be tested directly without
spinning up SignalR or a background thread:

- `BombLogic`: can a bomb be placed here (alive, under `MaxBombs`, tile is
  movable, no existing bomb there); transitive chain-reaction resolution (a
  bomb standing in another bomb's blast radius detonates too, with cycle-safe
  dedup); powerup effects (`SPEED` → `Player.Speed++`, `BOMB` → `MaxBombs++`,
  `EXPLOSION` → `PowerLevel++`); explosion-hit detection; round-over / winner
  detection (requires ≥2 players to have ever joined; "round over" means ≤1
  player alive).
- `ExplosionCalculator`: blast propagation along the 4 cardinal directions,
  stopping at a `WALL` (not included) or consuming-then-stopping at a `BRICK`
  (destructible terrain), up to `power` tiles.
- `Map`: tile grid parsed from a compact string encoding (`Create`/`Snapshot`),
  with `Reset()` restoring original (undestroyed) bricks between rounds.

### Round lifecycle

`RoundState` moves `WaitingForPlayers` → `InProgress` (once `MIN_PLAYERS_TO_START
= 2` have joined) → `RoundOver` (once ≤1 player remains alive), then resets after
a delay (`ROUND_RESET_DELAY_TICKS`), restoring the map and respawning players at
their initial positions.

## How to run it today

Requirements: .NET SDK matching `global.json` (10.0.301, `rollForward:
latestFeature` so any 10.0.3xx SDK works).

```bash
# restore + build
dotnet build BombRMan.slnx

# run the server (serves both the SignalR hub and the static client from wwwroot)
dotnet run --project BombRMan.Core/BombRMan.Core.csproj
# -> open the printed localhost URL in two or more browser tabs to play locally

# run the test suite
dotnet test BombRMan.slnx
```

VS Code users can use the provided `.vscode/launch.json` (F5) and
`.vscode/tasks.json` (`build`/`publish`/`watch`) instead.

There is currently one process serving everything (HTTP + SignalR + static
files); "multiplayer" today means multiple browser tabs/clients connecting to
that one process, not multiple server processes.

## Known gaps

These are the gaps between what exists today and a fully polished, easily
runnable project (tracked as GitHub issues — see the roadmap tracking issue):

1. **No Aspire AppHost.** There's no single-command way to orchestrate the
   server (and, longer-term, any additional services such as a second game
   server instance, Redis-backed backplane for multi-instance SignalR, etc.)
   for local dev/debugging. This is the primary new milestone this baseline
   adds to the roadmap.
2. **No CI.** No `.github/workflows`, so builds/tests aren't verified
   automatically on PRs.
3. **No containerization.** No `Dockerfile`, so there's no reproducible
   deployment artifact.
4. ~~**No live diagnostics endpoint.**~~ Resolved: `GET /diagnostics` returns a
   live JSON snapshot of round state, players, bombs, explosions, powerups,
   map tiles, and the per-second server stats counters (updates/sec,
   inputs/sec, dropped inputs, tick overruns — see `GameStateDiagnosticsTests`
   and `DiagnosticsEndpointTests`).
5. ~~**Client is a 2012-era jQuery UI static page.**~~ Resolved: the client is
   now plain vanilla JS/Canvas (no framework dependency as of M3), though it's
   still a single hand-rolled `index.htm` with no build step, bundler, or
   component model.
6. **No baseline README/CONTRIBUTING docs** prior to this change.
7. **Single-process, single-game-state multiplayer.** Everyone who connects to
   the one running server shares a single `GameState` instance/round — there's
   no matchmaking, multiple concurrent rooms, or horizontal scale-out (would
   need a SignalR backplane if that's ever a goal).

## Testing

`BombRMan.Core.Tests` (65 tests, all passing as of this writing) covers:
`BombLogicTests`, `MapTests`, `PlayerTests`, `PlayerInputBufferTests`,
`KeyboardStateConverterTests`, `GameServerIntegrationTests` (hub-level
integration tests), `GameStateDiagnosticsTests`, and `DiagnosticsEndpointTests`
(the `/diagnostics` HTTP endpoint).
