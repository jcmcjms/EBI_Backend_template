using EbiBackend.Composition;
using EbiBackend.Features.Auth.Endpoints;
using EbiBackend.Features.Users.Endpoints;
using EbiBackend.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.RegisterServices(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.WithTitle("EbiBackend API");
});

app.MapAuthEndpoints();
app.MapUserEndpoints();

app.Run();

public partial class Program { }