using Microsoft.AspNetCore.Mvc;
using OpenAI.Extensions;
using VerbMerger;
using VerbMerger.Merger;
using VerbMerger.Merger.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// add configuration
builder.Configuration.AddJsonFile("secrets.json", optional: true, reloadOnChange: true);

builder.Services.Configure<VerbMergerConfig>(builder.Configuration.GetSection(nameof(VerbMergerConfig)));

builder.AddMongoDBClient("mongodb");

builder.Services.AddSingleton<Instrumentation>();
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Instrumentation.ActivitySourceName));

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMemoryCache(opts =>
{
    const int megabyte = 1 << 20;
    opts.SizeLimit = 20 * megabyte;
});
builder.Services.AddOpenAIService();
builder.Services.AddTransient<IMergeResultSeeder, MergeResultSeeder>();
builder.Services.AddTransient<IMergeResultPersistence, MongoDbMergePersistence>();
builder.Services.AddTransient<IMergeSampler, MongoDbMergePersistence>();
builder.Services.AddTransient<IMergerBatchProompter, BatchProompter>();

builder.Services.AddSingleton<IMergerProompter, MergerProompterBatchManager>();

builder.Services.AddScoped<IMergeRepository, MergeRepository>();
builder.Services.AddScoped<IMergerService, MergerService>();


var app = builder.Build();

// run index creation
var persistenceInitialize = Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    var persistence = scope.ServiceProvider.GetRequiredService<IMergeResultPersistence>();
    await persistence.Initialize();
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
    
app.MapGet("/api/merge", async (
        [FromQuery] string subject,
        [FromQuery] string verb,
        [FromQuery] string @object,
        IMergerService mergerService) =>
    {
        var output = await mergerService.GetOutput(new MergeInput(subject, verb, @object));
        if (output.TryGetSuccess(out var successOut))
        {
            return Results.Json(successOut);
        }
        else
        {
            return Results.UnprocessableEntity(new
            {
                error = "Failed to merge",
                status = output.Status.ToString()
            });
        }
    })
    .WithName("Merge")
    .WithOpenApi();

app.MapGet("/api/admin/dump", async (IMergeSampler sampler) =>
{
    var dump = await sampler.SampleExamples(100);
    return dump;
}).WithName("DumpCache").WithOpenApi();

app.MapDefaultEndpoints();

// ensure index creation finishes before app startup
try
{
    await persistenceInitialize;
}
catch
{
    await app.DisposeAsync();
}
await app.RunAsync();
