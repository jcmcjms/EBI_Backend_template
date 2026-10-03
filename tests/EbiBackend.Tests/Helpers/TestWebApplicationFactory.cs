using EbiBackend.Features.Auth.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EbiBackend.Tests.Helpers;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("Jwt:SecretKey", "TestSecretKeyThatIsLongEnoughForHmacSha256!!");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AuthDbContext>));

            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddSingleton<DbContextOptions<AuthDbContext>>(
                new DbContextOptionsBuilder<AuthDbContext>()
                    .UseInMemoryDatabase("TestDb")
                    .Options);
        });
    }
}