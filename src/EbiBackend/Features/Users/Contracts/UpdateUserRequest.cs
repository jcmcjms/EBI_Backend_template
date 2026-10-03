namespace EbiBackend.Features.Users.Contracts;

public sealed record UpdateUserRequest(
    string DisplayName,
    string Email);