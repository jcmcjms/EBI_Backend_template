namespace EbiBackend.Features.Auth.Contracts;

public sealed record LoginRequest(
    string EmailOrUsername,
    string Password);