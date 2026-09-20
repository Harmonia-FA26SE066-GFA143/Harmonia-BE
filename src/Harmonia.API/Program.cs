using Harmonia.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Anchor to ContentRootPath rather than the working directory: `dotnet ef` runs from the
// solution root and a bare Env.Load() would not find this file.
DotNetEnv.Env.Load(Path.Combine(builder.Environment.ContentRootPath, ".env"));

// Reload after Env.Load: CreateBuilder reads environment variables before .env is applied,
// so without this call IConfiguration misses every key defined in .env.
builder.Configuration.AddEnvironmentVariables();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
