using Template.Api.Composition;

var builder = WebApplication.CreateBuilder(args);

// Composition root: all dependency registration lives in Composition/.
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddSecurity(builder.Configuration, builder.Environment);
builder.Services.AddApplicationServices();

var app = builder.Build();

// No-op unless Database:ApplyMigrations is enabled (development and one-shot deployment jobs).
await app.InitializeDatabaseAsync();

app.ConfigurePipeline();

await app.RunAsync();

/// <summary>Entry point marker exposed to <c>WebApplicationFactory&lt;Program&gt;</c> in tests.</summary>
public partial class Program
{
}
