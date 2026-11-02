using BombRMan.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR(o =>
{
    o.EnableDetailedErrors = builder.Environment.IsDevelopment();
})
.AddJsonProtocol(o =>
{
    o.PayloadSerializerOptions.Converters.Add(new KeyboardStateConverter());
});

builder.Services.AddSingleton<GameState>();

var app = builder.Build();

app.UseFileServer();
app.UseRouting();

app.MapHub<GameServer>("/game");

// Live round/player/bomb/explosion/powerup state for debugging/observability.
// See ARCHITECTURE.md "Known gaps" and tracking issue #16.
app.MapGet("/diagnostics", (GameState gameState) => Results.Ok(gameState.GetDiagnosticsSnapshot()));

app.Run();

public partial class Program;
