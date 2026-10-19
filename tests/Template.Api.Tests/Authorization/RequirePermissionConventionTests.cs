using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Template.Api.Infrastructure.Authorization;

namespace Template.Api.Tests.Authorization;

public class RequirePermissionConventionTests
{
    private sealed class RecordingConventionBuilder : IEndpointConventionBuilder
    {
        public List<Action<EndpointBuilder>> Calls { get; } = [];

        public void Add(Action<EndpointBuilder> convention) => Calls.Add(convention);
    }

    private sealed class TestEndpointBuilder : EndpointBuilder
    {
        public override Endpoint Build() =>
            new(_ => Task.CompletedTask, new EndpointMetadataCollection([.. Metadata]), DisplayName);
    }

    [Fact]
    public void RequirePermission_RejectsUnknownPermissionsAtStartup()
    {
        var builder = new RecordingConventionBuilder();

        var exception = Assert.Throws<ArgumentException>(
            () => builder.RequirePermission("users.read", "users.destroy"));

        Assert.Contains("users.destroy", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RequirePermission_RejectsEmptyPermissionList()
    {
        var builder = new RecordingConventionBuilder();

        Assert.Throws<ArgumentException>(() => builder.RequirePermission());
    }

    [Fact]
    public void RequirePermission_AddsPolicyAndMetadata()
    {
        var builder = new RecordingConventionBuilder();

        builder.RequirePermission("users.read", "roles.read");

        var endpointBuilder = new TestEndpointBuilder();
        foreach (var convention in builder.Calls)
        {
            convention(endpointBuilder);
        }

        var authorizationMetadata = endpointBuilder.Metadata.OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>().ToList();
        Assert.Contains(authorizationMetadata, data => data.Policy == AuthorizationPolicies.RequirePermissions);

        var required = endpointBuilder.Metadata.OfType<RequiredPermissions>().Single();
        Assert.Equal(["users.read", "roles.read"], required.Values);
    }
}
