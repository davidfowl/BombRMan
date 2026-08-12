namespace BombRMan.Hubs;

/// <summary>
/// Pure, testable rules that govern bomb placement, powerup effects, explosion hits,
/// and round-over detection. Kept free of SignalR/threading concerns so it can be
/// exercised directly in unit tests, mirroring how <see cref="Player.Update(in KeyboardState, Map)"/>
/// is tested without spinning up a <see cref="GameState"/>.
/// </summary>
public static class BombLogic
{
    public static bool ShouldSpawnPowerup(int roll, int spawnPercent) =>
        roll >= 0 && roll < spawnPercent;

    public static bool CanPlaceBomb(Player player, Map map, int x, int y, IReadOnlyList<Bomb> bombs)
    {
        if (!player.IsAlive)
        {
            return false;
        }

        if (player.ActiveBombs >= player.MaxBombs)
        {
            return false;
        }

        if (!map.Movable(x, y))
        {
            return false;
        }

        foreach (var bomb in bombs)
        {
            if (bomb.X == x && bomb.Y == y)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Resolves the full transitive set of bombs that detonate when <paramref name="triggers"/> go off,
    /// in detonation order. A bomb standing on another bomb's blast tile is chained in, and each bomb is
    /// returned at most once so chain loops (A blasts B, B blasts A) terminate.
    /// </summary>
    public static List<Bomb> ResolveChainReaction(IReadOnlyList<Bomb> bombs, IEnumerable<Bomb> triggers, Map map)
    {
        var detonated = new List<Bomb>();
        var pending = new Queue<Bomb>();

        foreach (var trigger in triggers)
        {
            if (bombs.Contains(trigger) && !detonated.Contains(trigger))
            {
                detonated.Add(trigger);
                pending.Enqueue(trigger);
            }
        }

        while (pending.Count > 0)
        {
            var bomb = pending.Dequeue();

            foreach (var tile in ExplosionCalculator.GetBlastTiles(map, bomb.X, bomb.Y, bomb.Power))
            {
                foreach (var other in bombs)
                {
                    if (other.X == tile.X && other.Y == tile.Y && !detonated.Contains(other))
                    {
                        detonated.Add(other);
                        pending.Enqueue(other);
                    }
                }
            }
        }

        return detonated;
    }

    public static void ApplyPowerup(Player player, PowerupType type)
    {
        switch (type)
        {
            case PowerupType.SPEED:
                player.Speed++;
                break;
            case PowerupType.BOMB:
                player.MaxBombs++;
                break;
            case PowerupType.EXPLOSION:
                player.PowerLevel++;
                break;
        }
    }

    public static bool IsHitByExplosion(Player player, IEnumerable<Explosion> explosions)
    {
        foreach (var explosion in explosions)
        {
            if (explosion.X == player.X && explosion.Y == player.Y)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the sole surviving player if the round is over (one or zero players alive
    /// among <paramref name="activePlayers"/>), otherwise null. A null return with an empty
    /// or all-dead roster is a draw; callers should treat "no candidate" and "roster count &lt;= 1
    /// alive" together with <see cref="IsRoundOver"/>.
    /// </summary>
    public static bool IsRoundOver(IReadOnlyList<Player> activePlayers, out Player winner)
    {
        winner = null;
        int aliveCount = 0;

        foreach (var player in activePlayers)
        {
            if (player.IsAlive)
            {
                aliveCount++;
                winner = player;
            }
        }

        if (activePlayers.Count < 2)
        {
            // Not enough players for a round to be meaningfully "over" yet.
            winner = null;
            return false;
        }

        if (aliveCount > 1)
        {
            winner = null;
            return false;
        }

        // aliveCount is 0 (draw, winner stays null) or 1 (winner is set above).
        return true;
    }
}
