using EbiBackend.Features.Auth.Contracts;
using EbiBackend.Features.Users.Contracts;
using EbiBackend.Shared;

namespace EbiBackend.Features.Users.Services;

public interface IUserService
{
    Task<Result<List<UserProfileResponse>>> GetUsersAsync(UserFilterRequest filter, CancellationToken cancellationToken = default);
    Task<Result<UserProfileResponse>> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<UserProfileResponse>> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
}