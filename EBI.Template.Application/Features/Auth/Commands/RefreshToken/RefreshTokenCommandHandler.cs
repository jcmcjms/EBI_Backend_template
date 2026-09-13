using EBI.Template.Application.Features.Auth.DTOs;
using EBI.Template.Application.Interfaces;
using EBI.Template.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EBI.Template.Application.Features.Auth.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResult>
{
    private readonly IRepository<User> _userRepository;
    private readonly IJwtTokenService _jwtTokenService;

    public RefreshTokenCommandHandler(
        IRepository<User> userRepository,
        IJwtTokenService jwtTokenService)
    {
        _userRepository = userRepository;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var refreshTokenHash = _jwtTokenService.HashRefreshToken(request.RefreshToken);

        var user = await _userRepository.Query()
            .FirstOrDefaultAsync(
                u => u.RefreshTokenHash == refreshTokenHash && u.RefreshTokenExpiry > DateTime.UtcNow,
                cancellationToken);

        if (user is null)
        {
            return AuthResult.Failure("Invalid or expired refresh token", "INVALID_REFRESH_TOKEN");
        }

        var newAccessToken = _jwtTokenService.GenerateAccessToken(user);
        var newRefreshToken = _jwtTokenService.GenerateRefreshToken();

        user.RefreshTokenHash = _jwtTokenService.HashRefreshToken(newRefreshToken);
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _userRepository.UpdateAsync(user, cancellationToken);

        return AuthResult.Success(new AccessTokenResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken
        });
    }
}
