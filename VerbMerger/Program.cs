using VerbMerger.Merger;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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

app.MapGet("/merge/{subject}/{verb}/{object}", async (string subject, string verb, string @object, IMergerRepository repository) =>
    {
        var output = await repository.GetOutput(new MergeInput(subject, verb, @object));
        return output;
    })
    .WithName("Merge")
    .WithOpenApi();

app.Run();
