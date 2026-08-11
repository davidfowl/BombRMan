using BombRMan.Hubs;
using Xunit;

namespace BombRMan.Core.Tests;

public class BombLogicTests
{
    private static Map CreateOpenMap() => new(
        "2222222" +
        "2000002" +
        "2030002" +
        "2000002" +
        "2222222", 7, 5, 32);

    private static Player CreatePlayer(int x, int y, int index = 0) => new()
    {
        Index = index,
        X = x,
        Y = y,
        ExactX = x * GameState.POWER,
        ExactY = y * GameState.POWER,
        IsAlive = true,
        MaxBombs = 1,
        ActiveBombs = 0,
    };

    [Fact]
    public void CanPlaceBombAllowsPlacementOnEmptyGrassTile()
    {
        var player = CreatePlayer(1, 1);
        var map = CreateOpenMap();

        Assert.True(BombLogic.CanPlaceBomb(player, map, player.X, player.Y, new List<Bomb>()));
    }

    [Fact]
    public void CanPlaceBombRejectsWhenAtMaxBombs()
    {
        var player = CreatePlayer(1, 1);
        player.ActiveBombs = player.MaxBombs;
        var map = CreateOpenMap();

        Assert.False(BombLogic.CanPlaceBomb(player, map, player.X, player.Y, new List<Bomb>()));
    }

    [Fact]
    public void CanPlaceBombRejectsWhenTileAlreadyHasABomb()
    {
        var player = CreatePlayer(1, 1);
        var map = CreateOpenMap();
        var existing = new List<Bomb> { new() { X = 1, Y = 1 } };

        Assert.False(BombLogic.CanPlaceBomb(player, map, player.X, player.Y, existing));
    }

    [Fact]
    public void CanPlaceBombRejectsWhenPlayerIsDead()
    {
        var player = CreatePlayer(1, 1);
        player.IsAlive = false;
        var map = CreateOpenMap();

        Assert.False(BombLogic.CanPlaceBomb(player, map, player.X, player.Y, new List<Bomb>()));
    }

    [Fact]
    public void CanPlaceBombRejectsOnNonGrassTile()
    {
        var player = CreatePlayer(1, 1);
        var map = CreateOpenMap();

        // (0,0) is a WALL tile in the test map.
        Assert.False(BombLogic.CanPlaceBomb(player, map, 0, 0, new List<Bomb>()));
    }

    [Fact]
    public void GetBlastTilesIncludesOriginAndStopsAtWalls()
    {
        var map = CreateOpenMap();

        // Origin (1,1) with power 10 should never escape the surrounding walls (map is 7x5,
        // interior spans x=1..5, y=1..3).
        var tiles = ExplosionCalculator.GetBlastTiles(map, 1, 1, 10);

        Assert.Contains(tiles, p => p.X == 1 && p.Y == 1);
        Assert.DoesNotContain(tiles, p => p.X == 0 || p.Y == 0);
        Assert.DoesNotContain(tiles, p => p.X == 6 || p.Y == 4);
    }

    [Fact]
    public void GetBlastTilesIncludesBrickButStopsPropagationThroughIt()
    {
        var map = CreateOpenMap();

        // Brick sits at (2,2). Bomb placed at (1,2) blasting east with enough power to travel
        // through the brick if it didn't block propagation.
        var tiles = ExplosionCalculator.GetBlastTiles(map, 1, 2, 5);

        Assert.Contains(tiles, p => p.X == 2 && p.Y == 2);
        Assert.DoesNotContain(tiles, p => p.X == 3 && p.Y == 2);
    }

    [Fact]
    public void GetBlastTilesRespectsPowerLimit()
    {
        var map = CreateOpenMap();

        var tiles = ExplosionCalculator.GetBlastTiles(map, 1, 1, 1);

        Assert.Contains(tiles, p => p.X == 2 && p.Y == 1);
        Assert.DoesNotContain(tiles, p => p.X == 3 && p.Y == 1);
    }

    [Fact]
    public void ApplyPowerupIncreasesSpeedForSpeedPowerup()    {
        var player = CreatePlayer(1, 1);
        var initialSpeed = player.Speed;

        BombLogic.ApplyPowerup(player, PowerupType.SPEED);

        Assert.Equal(initialSpeed + 1, player.Speed);
    }

    [Fact]
    public void ApplyPowerupIncreasesMaxBombsForBombPowerup()
    {
        var player = CreatePlayer(1, 1);
        var initialMaxBombs = player.MaxBombs;

        BombLogic.ApplyPowerup(player, PowerupType.BOMB);

        Assert.Equal(initialMaxBombs + 1, player.MaxBombs);
    }

    [Fact]
    public void ApplyPowerupIncreasesPowerLevelForExplosionPowerup()
    {
        var player = CreatePlayer(1, 1);
        var initialPower = player.PowerLevel;

        BombLogic.ApplyPowerup(player, PowerupType.EXPLOSION);

        Assert.Equal(initialPower + 1, player.PowerLevel);
    }

    [Fact]
    public void IsHitByExplosionReturnsTrueWhenPlayerOverlapsExplosionTile()    {
        var player = CreatePlayer(3, 2);
        var explosions = new List<Explosion> { new() { X = 3, Y = 2, TicksRemaining = 10 } };

        Assert.True(BombLogic.IsHitByExplosion(player, explosions));
    }

    [Fact]
    public void IsHitByExplosionReturnsFalseWhenNoOverlap()
    {
        var player = CreatePlayer(3, 2);
        var explosions = new List<Explosion> { new() { X = 4, Y = 2, TicksRemaining = 10 } };

        Assert.False(BombLogic.IsHitByExplosion(player, explosions));
    }

    [Fact]
    public void IsRoundOverReturnsFalseWithFewerThanTwoPlayers()
    {
        var players = new List<Player> { CreatePlayer(1, 1) };

        Assert.False(BombLogic.IsRoundOver(players, out var winner));
        Assert.Null(winner);
    }

    [Fact]
    public void IsRoundOverReturnsFalseWhileMultiplePlayersAlive()
    {
        var players = new List<Player> { CreatePlayer(1, 1, 0), CreatePlayer(2, 2, 1) };

        Assert.False(BombLogic.IsRoundOver(players, out var winner));
        Assert.Null(winner);
    }

    [Fact]
    public void IsRoundOverReturnsLastPlayerStandingAsWinner()
    {
        var alive = CreatePlayer(1, 1, 0);
        var dead = CreatePlayer(2, 2, 1);
        dead.IsAlive = false;
        var players = new List<Player> { alive, dead };

        Assert.True(BombLogic.IsRoundOver(players, out var winner));
        Assert.Same(alive, winner);
    }

    [Fact]
    public void IsRoundOverReturnsDrawWhenAllPlayersDead()
    {
        var p1 = CreatePlayer(1, 1, 0);
        p1.IsAlive = false;
        var p2 = CreatePlayer(2, 2, 1);
        p2.IsAlive = false;
        var players = new List<Player> { p1, p2 };

        Assert.True(BombLogic.IsRoundOver(players, out var winner));
        Assert.Null(winner);
    }

    [Fact]
    public void ResolveChainReactionDetonatesOnlyTheTriggerWhenNothingIsInRange()
    {
        var map = CreateOpenMap();
        var trigger = new Bomb { Id = 1, X = 1, Y = 1, Power = 1 };
        var faraway = new Bomb { Id = 2, X = 5, Y = 3, Power = 1 };
        var bombs = new List<Bomb> { trigger, faraway };

        var detonated = BombLogic.ResolveChainReaction(bombs, new[] { trigger }, map);

        Assert.Equal(new[] { trigger }, detonated);
    }

    [Fact]
    public void ResolveChainReactionChainsBombCaughtInTheBlast()
    {
        var map = CreateOpenMap();
        var trigger = new Bomb { Id = 1, X = 1, Y = 1, Power = 2 };
        var neighbour = new Bomb { Id = 2, X = 3, Y = 1, Power = 1 };
        var bombs = new List<Bomb> { trigger, neighbour };

        var detonated = BombLogic.ResolveChainReaction(bombs, new[] { trigger }, map);

        Assert.Equal(new[] { trigger, neighbour }, detonated);
    }

    [Fact]
    public void ResolveChainReactionPropagatesTransitivelyThroughMultipleBombs()
    {
        var map = CreateOpenMap();
        var a = new Bomb { Id = 1, X = 1, Y = 1, Power = 1 };
        var b = new Bomb { Id = 2, X = 2, Y = 1, Power = 1 };
        var c = new Bomb { Id = 3, X = 3, Y = 1, Power = 1 };
        var bombs = new List<Bomb> { a, b, c };

        var detonated = BombLogic.ResolveChainReaction(bombs, new[] { a }, map);

        Assert.Equal(new[] { a, b, c }, detonated);
    }

    [Fact]
    public void ResolveChainReactionReturnsEachBombOnlyOnceForMutuallyChainingBombs()
    {
        var map = CreateOpenMap();
        // Each bomb sits inside the other's blast, which previously recursed indefinitely.
        var a = new Bomb { Id = 1, X = 1, Y = 1, Power = 3 };
        var b = new Bomb { Id = 2, X = 2, Y = 1, Power = 3 };
        var bombs = new List<Bomb> { a, b };

        var detonated = BombLogic.ResolveChainReaction(bombs, new[] { a }, map);

        Assert.Equal(2, detonated.Count);
        Assert.Equal(new[] { a, b }, detonated);
    }

    [Fact]
    public void ResolveChainReactionDeduplicatesWhenMultipleTriggersChainIntoEachOther()
    {
        var map = CreateOpenMap();
        var a = new Bomb { Id = 1, X = 1, Y = 1, Power = 2 };
        var b = new Bomb { Id = 2, X = 2, Y = 1, Power = 2 };
        var bombs = new List<Bomb> { a, b };

        // Both bombs expire on the same tick AND blast each other - the exact repro for the
        // stale-index crash that previously killed the game loop thread.
        var detonated = BombLogic.ResolveChainReaction(bombs, new[] { a, b }, map);

        Assert.Equal(2, detonated.Count);
        Assert.Equal(new[] { a, b }, detonated);
    }

    [Fact]
    public void ResolveChainReactionIgnoresTriggersNotPresentInTheBombList()
    {
        var map = CreateOpenMap();
        var live = new Bomb { Id = 1, X = 1, Y = 1, Power = 1 };
        var stale = new Bomb { Id = 99, X = 1, Y = 3, Power = 1 };
        var bombs = new List<Bomb> { live };

        var detonated = BombLogic.ResolveChainReaction(bombs, new[] { live, stale }, map);

        Assert.Equal(new[] { live }, detonated);
    }

    [Fact]
    public void ResolveChainReactionDoesNotChainThroughAWall()
    {
        // Wall at (2,1) sits between the two bombs, so the blast cannot reach the second bomb.
        var map = new Map(
            "22222" +
            "20202" +
            "20002" +
            "22222", 5, 4, 32);

        var trigger = new Bomb { Id = 1, X = 1, Y = 1, Power = 5 };
        var shielded = new Bomb { Id = 2, X = 3, Y = 1, Power = 5 };
        var bombs = new List<Bomb> { trigger, shielded };

        var detonated = BombLogic.ResolveChainReaction(bombs, new[] { trigger }, map);

        Assert.Equal(new[] { trigger }, detonated);
    }
}
