namespace EbiBackend.Features.Auth.Contracts;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string DisplayName);