var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.VerbMerger>("apiservice")
    .WithExternalHttpEndpoints()
    .WithEndpoint();

builder.Build().Run();