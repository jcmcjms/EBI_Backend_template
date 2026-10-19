using Microsoft.AspNetCore.Authorization;
using Template.Api.Infrastructure.Security;

namespace Template.Api.Infrastructure.Authorization;

/// <summary>Policy names used by endpoint conventions.</summary>
public static class AuthorizationPolicies
{
    /// <summary>Requires an authenticated principal holding the endpoint's <see cref="RequiredPermissions"/>.</summary>
    public const string RequirePermissions = "RequirePermissions";
}

/// <summary>Endpoint metadata listing the permissions a caller must hold. All listed permissions are required.</summary>
public sealed record RequiredPermissions(IReadOnlyList<string> Values);

/// <summary>Marker requirement evaluated by <see cref="PermissionsAuthorizationHandler"/> against endpoint metadata.</summary>
public sealed class PermissionsRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Grants access only when the authenticated user holds every permission required by the endpoint's
/// <see cref="RequiredPermissions"/> metadata. Permissions come from signed, server-issued "perm" claims.
/// </summary>
public sealed class PermissionsAuthorizationHandler : AuthorizationHandler<PermissionsRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionsRequirement requirement)
    {
        if (context.Resource is not HttpContext httpContext)
        {
            return Task.CompletedTask;
        }

        var required = httpContext.GetEndpoint()?.Metadata.GetMetadata<RequiredPermissions>();
        if (required is null || required.Values.Count == 0)
        {
            // The policy was applied without permission metadata; authentication alone is sufficient.
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var granted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var claim in context.User.Claims)
        {
            if (claim.Type == AuthClaims.Permission)
            {
                granted.Add(claim.Value);
            }
        }

        if (required.Values.All(granted.Contains))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
