var builder = DistributedApplication.CreateBuilder(args);

var mongo = builder.AddMongoDB("mongo")
    // withDataVolume ensures data persists across restarts
    .WithDataVolume();
var mongodb = mongo.AddDatabase("mongodb", "verb_merger");

var apiService = builder
    .AddProject<Projects.VerbMerger>("apiservice")
    .WithReference(mongodb)
    .WithEnvironment("OTEL_DOTNET_AUTO_TRACES_ADDITIONAL_SOURCES", "VerbMerger.Prompts")
    .WithEnvironment("OTEL_DOTNET_AUTO_METRICS_ADDITIONAL_SOURCES", "VerbMerger.Prompts")
    .WithExternalHttpEndpoints();

builder.Build().Run();