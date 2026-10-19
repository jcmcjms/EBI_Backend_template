using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Template.Api.Infrastructure.Authorization;
using Template.Api.Infrastructure.Security;
using Template.Api.Tests.Testing;

namespace Template.Api.Tests.Authorization;

public class PermissionsAuthorizationHandlerTests
{
    private static readonly PermissionsRequirement Requirement = new();

    private static AuthorizationHandlerContext CreateContext(
        ClaimsPrincipal principal,
        params string[] requiredPermissions)
    {
        var httpContext = new DefaultHttpContext { User = principal };
        if (requiredPermissions.Length > 0)
        {
            var endpoint = new Endpoint(
                _ => Task.CompletedTask,
                new EndpointMetadataCollection(new RequiredPermissions(requiredPermissions)),
                "test-endpoint");
            httpContext.SetEndpoint(endpoint);
        }

        return new AuthorizationHandlerContext([Requirement], principal, httpContext);
    }

    private static ClaimsPrincipal UserWithPermissions(params string[] permissions) =>
        new(new ClaimsIdentity(
            [.. permissions.Select(permission => new Claim(AuthClaims.Permission, permission))],
            authenticationType: "Test"));

    [Fact]
    public async Task Succeeds_WhenUserHoldsAllRequiredPermissions()
    {
        var context = CreateContext(
            UserWithPermissions("users.read", "users.write", "roles.read"),
            requiredPermissions: ["users.read", "roles.read"]);

        await new PermissionsAuthorizationHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Fails_WhenAnyRequiredPermissionIsMissing()
    {
        var context = CreateContext(
            UserWithPermissions("users.read"),
            requiredPermissions: ["users.read", "roles.manage"]);

        await new PermissionsAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Fails_WhenUserHasNoPermissionClaims()
    {
        var context = CreateContext(
            UserWithPermissions(),
            requiredPermissions: ["users.read"]);

        await new PermissionsAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Succeeds_WhenEndpointCarriesNoPermissionMetadata()
    {
        // Policy applied without metadata: authentication alone is sufficient (policy semantics).
        var context = CreateContext(UserWithPermissions());

        await new PermissionsAuthorizationHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Fails_WhenResourceIsNotHttpContext()
    {
        var principal = UserWithPermissions("users.read");
        var context = new AuthorizationHandlerContext([Requirement], principal, resource: null);

        await new PermissionsAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task PermissionMatchingIsCaseSensitive()
    {
        var context = CreateContext(
            UserWithPermissions("USERS.READ"),
            requiredPermissions: ["users.read"]);

        await new PermissionsAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
