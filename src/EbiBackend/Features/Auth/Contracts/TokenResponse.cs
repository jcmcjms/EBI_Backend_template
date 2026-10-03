namespace EbiBackend.Features.Auth.Contracts;

public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt);