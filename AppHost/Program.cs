var builder = DistributedApplication.CreateBuilder(args);

var mongo = builder.AddMongoDB("mongo")
    // withDataVolume ensures data persists across restarts
    //.WithDataBindMount("mongo-merge-results")
    .WithDataVolume("mongo-merge-results")
    ;

var mongodb = mongo.AddDatabase("mongodb", "verb_merger");

var apiService = builder
    .AddProject<Projects.VerbMerger>("apiservice")
    .WithReference(mongodb)
    .WithExternalHttpEndpoints();

builder.Build().Run();