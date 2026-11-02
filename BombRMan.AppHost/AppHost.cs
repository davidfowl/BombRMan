var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.BombRMan_Core>("bombrman-server")
    .WithExternalHttpEndpoints();

builder.Build().Run();
