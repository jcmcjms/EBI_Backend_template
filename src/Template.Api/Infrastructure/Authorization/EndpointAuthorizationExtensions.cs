using Microsoft.AspNetCore.Builder;

namespace Template.Api.Infrastructure.Authorization;

/// <summary>Endpoint convention for permission-based authorization.</summary>
public static class EndpointAuthorizationExtensions
{
    /// <summary>
    /// Requires an authenticated principal holding all specified catalog permissions.
    /// Permission strings are validated against <see cref="Permissions"/> at startup so typos fail fast.
    /// </summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, params string[] permissions)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(permissions);
        if (permissions.Length == 0)
        {
            throw new ArgumentException("At least one permission is required.", nameof(permissions));
        }

        foreach (var permission in permissions)
        {
            if (!Permissions.IsKnown(permission))
            {
                throw new ArgumentException($"Permission '{permission}' is not part of the catalog in {nameof(Permissions)}.", nameof(permissions));
            }
        }

        builder.RequireAuthorization(AuthorizationPolicies.RequirePermissions);
        builder.Add(endpointBuilder => endpointBuilder.Metadata.Add(new RequiredPermissions(permissions)));
        return builder;
    }
}
