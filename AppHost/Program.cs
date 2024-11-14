using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder
    .AddProject<Projects.VerbMerger>("apiservice")
    .WithExternalHttpEndpoints();
// if (builder.Environment.IsDevelopment())
// {
//     var mongo = builder.AddMongoDB("mongo")
//         // withDataVolume ensures data persists across restarts
//         //.WithDataBindMount("mongo-merge-results")
//         .WithDataVolume("mongo-merge-results")
//         ;
//     var mongodb = mongo.AddDatabase("mongodb", "verb_merger");
//     apiService = apiService.WithReference(mongodb);
// }

builder.Build().Run();