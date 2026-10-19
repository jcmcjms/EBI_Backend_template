using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Template.Api.Tests.Testing;

/// <summary>Minimal <see cref="IHostEnvironment"/> stub for validator tests.</summary>
public sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;

    public string ApplicationName { get; set; } = "Template.Api.Tests";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
