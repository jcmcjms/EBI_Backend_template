namespace EbiBackend.Features.Users.Contracts;

public sealed record UserFilterRequest(
    int Page = 1,
    int PageSize = 20);