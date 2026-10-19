using System.Security.Claims;

namespace Template.Api.Infrastructure.Security;

/// <summary>Reads authenticated identity data from validated access-token claims.</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>Returns the authenticated user id from the signed "sub" claim.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var subject = principal.FindFirstValue(AuthClaims.Subject);
        if (!Guid.TryParse(subject, out var userId))
        {
            // Only reachable with a token from a trusted issuer that omits "sub" — a configuration error, not a client error.
            throw new InvalidOperationException("The access token does not contain a valid 'sub' claim.");
        }

        return userId;
    }
}
