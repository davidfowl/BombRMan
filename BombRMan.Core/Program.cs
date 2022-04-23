using BombRMan.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR(o => o.EnableDetailedErrors = true);
builder.Services.AddSingleton<GameState>();

var app = builder.Build();

app.UseFileServer();
app.UseRouting();

app.MapHub<GameServer>("/game");

app.Run();