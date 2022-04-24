using BombRMan.Hubs;
using Microsoft.Extensions.ObjectPool;

var builder = WebApplication.CreateBuilder(args);

var provider = new DefaultObjectPoolProvider();
provider.MaximumRetained = 1000;
var pool = provider.Create(new KeyboardStatePolicyProvider());

builder.Services.AddSingleton(pool);

builder.Services.AddSignalR(o =>
{
    o.EnableDetailedErrors = builder.Environment.IsDevelopment();
})
.AddJsonProtocol(o =>
{
    o.PayloadSerializerOptions.Converters.Add(new KeyboardStateConverter(pool));
});

builder.Services.AddSingleton<GameState>();

var app = builder.Build();

app.UseFileServer();
app.UseRouting();

app.MapHub<GameServer>("/game");

app.Run();
