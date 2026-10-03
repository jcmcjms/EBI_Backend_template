using EbiBackend.Features.Auth.Contracts;
using EbiBackend.Features.Auth.Domain;
using EbiBackend.Features.Auth.Persistence;
using EbiBackend.Shared;
using Microsoft.EntityFrameworkCore;

namespace EbiBackend.Features.Auth.Services;

public sealed class AuthService : IAuthService
{
    private readonly AuthDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly TimeProvider _timeProvider;

    public AuthService(AuthDbContext dbContext, ITokenService tokenService, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _timeProvider = timeProvider;
    }

    public async Task<Result<UserProfileResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var existingUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (existingUser is not null)
            return Result<UserProfileResponse>.Failure("A user with this email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            DisplayName = request.DisplayName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<UserProfileResponse>.Success(new UserProfileResponse(user.Id, user.Email, user.DisplayName));
    }

    public async Task<Result<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Result<TokenResponse>.Failure("Invalid email or password.");

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = now.AddDays(7),
            CreatedAt = now
        };

        _dbContext.RefreshTokens.Add(refreshTokenEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<TokenResponse>.Success(new TokenResponse(
            accessToken,
            refreshToken,
            now.AddMinutes(15)));
    }

    public async Task<Result<TokenResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var refreshToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, cancellationToken);

        if (refreshToken is null || !refreshToken.IsActive)
            return Result<TokenResponse>.Failure("Invalid or expired refresh token.");

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == refreshToken.UserId, cancellationToken);

        if (user is null)
            return Result<TokenResponse>.Failure("User not found.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        refreshToken.RevokedAt = now;

        var accessToken = _tokenService.GenerateAccessToken(user);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        var newRefreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = newRefreshToken,
            ExpiresAt = now.AddDays(7),
            CreatedAt = now
        };

        _dbContext.RefreshTokens.Add(newRefreshTokenEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<TokenResponse>.Success(new TokenResponse(
            accessToken,
            newRefreshToken,
            now.AddMinutes(15)));
    }

    public async Task<Result<UserProfileResponse>> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
            return Result<UserProfileResponse>.Failure("User not found.");

        return Result<UserProfileResponse>.Success(new UserProfileResponse(user.Id, user.Email, user.DisplayName));
    }
}