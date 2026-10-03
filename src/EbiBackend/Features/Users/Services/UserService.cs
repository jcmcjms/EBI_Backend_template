using EbiBackend.Features.Auth.Contracts;
using EbiBackend.Features.Auth.Persistence;
using EbiBackend.Features.Users.Contracts;
using EbiBackend.Shared;
using Microsoft.EntityFrameworkCore;

namespace EbiBackend.Features.Users.Services;

public sealed class UserService : IUserService
{
    private readonly AuthDbContext _dbContext;

    public UserService(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<List<UserProfileResponse>>> GetUsersAsync(UserFilterRequest filter, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var users = await _dbContext.Users
            .AsNoTracking()
            .OrderBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserProfileResponse(u.Id, u.Email, u.DisplayName))
            .ToListAsync(cancellationToken);

        return Result<List<UserProfileResponse>>.Success(users);
    }

    public async Task<Result<UserProfileResponse>> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserProfileResponse(u.Id, u.Email, u.DisplayName))
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return Result<UserProfileResponse>.Failure("User not found.");

        return Result<UserProfileResponse>.Success(user);
    }

    public async Task<Result<UserProfileResponse>> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
            return Result<UserProfileResponse>.Failure("User not found.");

        var emailTaken = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == request.Email && u.Id != userId, cancellationToken);

        if (emailTaken)
            return Result<UserProfileResponse>.Failure("Email is already in use.");

        user.DisplayName = request.DisplayName;
        user.Email = request.Email;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<UserProfileResponse>.Success(new UserProfileResponse(user.Id, user.Email, user.DisplayName));
    }

    public async Task<Result<bool>> DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
            return Result<bool>.Failure("User not found.");

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}