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

app.Run();
