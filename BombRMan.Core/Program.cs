using BombRMan.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

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

app.MapDefaultEndpoints();

app.UseFileServer();
app.UseRouting();

app.MapHub<GameServer>("/game");

app.Run();

public partial class Program;
