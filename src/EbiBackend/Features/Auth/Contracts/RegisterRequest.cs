namespace EbiBackend.Features.Auth.Contracts;

public sealed record RegisterRequest(
    string Email,
    string Username,
    string Password,
    string DisplayName);