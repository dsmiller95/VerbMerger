using Microsoft.AspNetCore.Mvc;
using OpenAI.Extensions;
using VerbMerger.Merger;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// add configuration
builder.Configuration.AddJsonFile("secrets.json", optional: true, reloadOnChange: true);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenAIService();
builder.Services.AddSingleton<IMergerProompter, MergerProompter>();
builder.Services.AddSingleton<IMergePersistence, InMemoryMergePersistence>();
builder.Services.AddScoped<IMergerRepository, MergerRepository>();

var app = builder.Build();

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
        IMergerRepository repository) =>
    {
        var output = await repository.GetOutput(new MergeInput(subject, verb, @object));
        return output;
    })
    .WithName("Merge")
    .WithOpenApi();

app.MapGet("/api/admin/dump", async (IMergePersistence persistence) =>
{
    var dump = await persistence.DumpCache();
    return dump;
}).WithName("DumpCache").WithOpenApi();

app.MapDefaultEndpoints();

app.Run();
