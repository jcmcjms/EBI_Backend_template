namespace EbiBackend.Features.Auth.Contracts;

public sealed record RefreshRequest(
    string RefreshToken);