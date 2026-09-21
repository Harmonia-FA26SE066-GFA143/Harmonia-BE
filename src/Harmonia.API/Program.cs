using Harmonia.API.Extensions;
using Harmonia.API.Middlewares;
using Harmonia.Application;
using Harmonia.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Anchor to ContentRootPath rather than the working directory: `dotnet ef` runs from the
// solution root and a bare Env.Load() would not find this file.
DotNetEnv.Env.Load(Path.Combine(builder.Environment.ContentRootPath, ".env"));

// Reload after Env.Load: CreateBuilder reads environment variables before .env is applied,
// so without this call IConfiguration misses every key defined in .env.
builder.Configuration.AddEnvironmentVariables();

// Add services to the container.

builder.Services.AddApiControllers();
builder.Services.AddApiSwagger();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline.
app.UseApiSwagger();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
