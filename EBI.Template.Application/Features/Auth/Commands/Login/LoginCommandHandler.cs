using EBI.Template.Application.Features.Auth.DTOs;
using EBI.Template.Application.Interfaces;
using EBI.Template.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EBI.Template.Application.Features.Auth.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResult>
{
    private readonly IRepository<User> _userRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPasswordHasher _passwordHasher;

    public LoginCommandHandler(
        IRepository<User> userRepository,
        IJwtTokenService jwtTokenService,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _jwtTokenService = jwtTokenService;
        _passwordHasher = passwordHasher;
    }

    public async Task<AuthResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.Query()
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return AuthResult.Failure("Invalid email or password", "INVALID_CREDENTIALS");
        }

        if (!user.IsActive)
        {
            return AuthResult.Failure("Account is disabled", "ACCOUNT_DISABLED");
        }

        var accessToken = _jwtTokenService.GenerateAccessToken(user);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();

        user.RefreshTokenHash = _jwtTokenService.HashRefreshToken(refreshToken);
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _userRepository.UpdateAsync(user, cancellationToken);

        return AuthResult.Success(new AccessTokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        });
    }
}
