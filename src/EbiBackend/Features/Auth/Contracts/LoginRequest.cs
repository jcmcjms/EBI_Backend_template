namespace EbiBackend.Features.Auth.Contracts;

public sealed record LoginRequest(
    string Email,
    string Password);