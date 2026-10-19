using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Template.Api.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core tooling (<c>dotnet ef migrations add</c>, <c>dotnet ef migrations script</c>).
/// Never used at runtime. The connection string is an inert placeholder: migration commands generate
/// SQL without opening a connection.
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=TemplateApi.DesignTime;User Id=design-time;Password=design-time-placeholder;TrustServerCertificate=True")
            .Options;

        return new AppDbContext(options);
    }
}
