using EbiBackend.Features.Auth.Contracts;
using EbiBackend.Shared;

namespace EbiBackend.Features.Auth.Services;

public interface IAuthService
{
    Task<Result<UserProfileResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<Result<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<TokenResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);
    Task<Result<UserProfileResponse>> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
}