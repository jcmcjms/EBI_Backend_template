namespace EbiBackend.Features.Auth.Contracts;

public sealed record UserProfileResponse(
    Guid Id,
    string Email,
    string DisplayName);