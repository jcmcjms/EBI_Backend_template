using EbiBackend.Features.Auth.Domain;

namespace EbiBackend.Features.Auth.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}