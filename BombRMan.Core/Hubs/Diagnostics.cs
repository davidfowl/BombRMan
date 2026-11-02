namespace BombRMan.Hubs;

/// <summary>
/// Live round/player/bomb/explosion/powerup state surfaced by the <c>/diagnostics</c> endpoint.
/// Built by <see cref="GameState.GetDiagnosticsSnapshot"/>.
/// </summary>
public sealed class DiagnosticsSnapshot
{
    public DateTimeOffset TimestampUtc { get; init; }
    public string RoundState { get; init; }
    public MapDiagnostics Map { get; init; }
    public IReadOnlyList<PlayerDiagnostics> Players { get; init; }
    public IReadOnlyList<Bomb> Bombs { get; init; }
    public IReadOnlyList<Explosion> Explosions { get; init; }
    public IReadOnlyList<Powerup> Powerups { get; init; }
    public GameState.ServerStats Stats { get; init; }
}

public sealed class MapDiagnostics
{
    public int Width { get; init; }
    public int Height { get; init; }
    public int TileSize { get; init; }

    /// <summary>Current tile layout, reflecting any destroyed bricks (same encoding as <see cref="Map.RawData"/>).</summary>
    public string Tiles { get; init; }
}

public sealed class PlayerDiagnostics
{
    public int Index { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int ExactX { get; init; }
    public int ExactY { get; init; }
    public string Direction { get; init; }
    public bool IsAlive { get; init; }
    public int MaxBombs { get; init; }
    public int ActiveBombs { get; init; }
    public int PowerLevel { get; init; }
    public int Speed { get; init; }
    public int LastProcessed { get; init; }
}
