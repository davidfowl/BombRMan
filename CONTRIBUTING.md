# Contributing to BombRMan

Thanks for your interest in contributing! This document covers everything you
need to get set up, make a change, and open a PR.

## Getting started

1. Install the .NET SDK version pinned in [`global.json`](global.json)
   (10.0.301; `rollForward: latestFeature`, so any 10.0.3xx SDK works).
2. Clone the repo and build:

   ```bash
   dotnet build BombRMan.slnx
   ```

3. Run the server:

   ```bash
   dotnet run --project BombRMan.Core/BombRMan.Core.csproj
   ```

   Open the printed `http://localhost:…` URL in two or more browser
   tabs/windows to play a local multiplayer round (at least 2 connected
   players are required for a round to start).

See [`README.md`](README.md) for the quick-start version of these steps and
[`ARCHITECTURE.md`](ARCHITECTURE.md) for a full breakdown of the tech stack,
project layout, and game-loop/networking design.

## Project layout

- `BombRMan.Core/` — the ASP.NET Core + SignalR server, including all game
  rules (`Hubs/`) and the static client assets (`wwwroot/`).
- `BombRMan.Core.Tests/` — xUnit tests covering game rules, networking glue,
  and diagnostics.

## Making changes

- Keep gameplay rules **server-authoritative**: the canonical source of truth
  for game state lives in `GameState` and the pure-logic helpers
  (`BombLogic`, `ExplosionCalculator`). Avoid adding client-side logic that
  could diverge from server behavior.
- Prefer small, focused PRs. If a change is a significant architecture or
  data-format decision, call that out explicitly in the PR description.
- Match the existing code style (nullable reference types enabled, no
  external DI container beyond what ASP.NET Core provides, minimal
  dependencies).

## Tests

Run the full test suite before opening a PR:

```bash
dotnet test BombRMan.slnx
```

All existing tests must continue to pass. Add new xUnit tests under
`BombRMan.Core.Tests/` for any new game-rule behavior or bug fix — in
particular, regression tests for edge cases (boundary conditions, race
conditions between simultaneous events, etc.) are especially valuable given
the server-authoritative, fixed-tick design.

If you change client-side code (`BombRMan.Core/wwwroot/js/`), manually verify
in a browser with at least two tabs/windows connected, since there is
currently no automated browser-level test harness in this repository.

## Submitting a pull request

1. Branch from `main`.
2. Make your change, including tests.
3. Make sure `dotnet build BombRMan.slnx` and `dotnet test BombRMan.slnx`
   both pass locally.
4. Open a PR against `main` describing what changed and why, and link any
   related issue.
5. CI (GitHub Actions) will build and run the test suite on your PR; make
   sure it's green before requesting review.

## Reporting bugs / proposing features

Please open a GitHub issue with:

- What you expected to happen vs. what actually happened (for bugs).
- Steps to reproduce, if applicable.
- For feature proposals, the motivation and a rough sketch of the approach.

## Code of conduct

Be respectful and constructive. This is a small hobby/learning project —
friendly, low-pressure collaboration is the goal.
