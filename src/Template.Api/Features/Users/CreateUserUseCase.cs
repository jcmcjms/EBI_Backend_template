using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Template.Api.Features.Users.Domain;
using Template.Api.Infrastructure.Persistence;
using Template.Api.Infrastructure.Security;

namespace Template.Api.Features.Users;

public sealed record CreateUserRequest
{
    [Required]
    [EmailAddress]
    [StringLength(User.MaxEmailLength)]
    public required string Email { get; init; }

    [Required]
    [StringLength(User.MaxUsernameLength, MinimumLength = User.MinUsernameLength)]
    public required string Username { get; init; }

    [Required]
    [StringLength(User.MaxNameLength)]
    public required string FirstName { get; init; }

    [StringLength(User.MaxNameLength)]
    public string? MiddleName { get; init; }

    [Required]
    [StringLength(User.MaxNameLength)]
    public required string LastName { get; init; }

    [StringLength(User.MaxSuffixLength)]
    public string? Suffix { get; init; }

    [StringLength(User.MaxJobTitleLength)]
    public string? JobTitle { get; init; }

    [Required]
    [StringLength(PasswordPolicyOptions.TransportMaxLength)]
    public required string Password { get; init; }

    /// <summary>Roles to assign. Unknown ids are rejected before anything is written.</summary>
    public IReadOnlyList<Guid> RoleIds { get; init; } = [];
}

public abstract record CreateUserOutcome
{
    public sealed record Success(UserResponse User) : CreateUserOutcome;

    public sealed record EmailAlreadyExists : CreateUserOutcome;

    public sealed record UsernameAlreadyExists : CreateUserOutcome;

    public sealed record InvalidUsername : CreateUserOutcome;

    public sealed record WeakPassword(int MinLength) : CreateUserOutcome;

    public sealed record UnknownRoles(IReadOnlyList<Guid> RoleIds) : CreateUserOutcome;
}

/// <summary>
/// Administrators create users (there is no public self-registration: role assignment must be a
/// deliberate server-side decision). Email and username uniqueness are enforced by unique indexes;
/// the DbUpdateException path keeps the checks race-safe between concurrent requests.
/// </summary>
public sealed class CreateUserUseCase(
    AppDbContext dbContext,
    PasswordHasher passwordHasher,
    IOptions<PasswordPolicyOptions> passwordPolicy,
    TimeProvider timeProvider)
{
    public async Task<CreateUserOutcome> ExecuteAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (request.Password.Length < passwordPolicy.Value.MinLength)
        {
            return new CreateUserOutcome.WeakPassword(passwordPolicy.Value.MinLength);
        }

        if (!User.IsValidUsername(request.Username.Trim().ToLowerInvariant()))
        {
            return new CreateUserOutcome.InvalidUsername();
        }

        var roleIds = request.RoleIds.Distinct().ToList();
        List<RoleAssignmentResponse> knownRoles = roleIds.Count == 0
            ? []
            : await dbContext.Roles
                .AsNoTracking()
                .Where(role => roleIds.Contains(role.Id))
                .Select(role => new RoleAssignmentResponse(role.Id, role.Name))
                .ToListAsync(cancellationToken);

        var unknownRoleIds = roleIds.Except(knownRoles.Select(role => role.Id)).ToList();
        if (unknownRoleIds.Count > 0)
        {
            return new CreateUserOutcome.UnknownRoles(unknownRoleIds);
        }

        var now = timeProvider.GetUtcNow();
        var user = User.Create(
            request.Email,
            request.Username,
            request.FirstName,
            request.LastName,
            passwordHasher.Hash(request.Password),
            now,
            request.MiddleName,
            request.Suffix,
            request.JobTitle);
        dbContext.Users.Add(user);
        foreach (var roleId in roleIds)
        {
            dbContext.UserRoles.Add(UserRole.Assign(user.Id, roleId, now));
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var conflict = await UserUniqueConflicts.FindAsync(dbContext, user.Email, user.Username, excludedUserId: null, cancellationToken);
            switch (conflict)
            {
                case UserConflict.Email:
                    return new CreateUserOutcome.EmailAlreadyExists();
                case UserConflict.Username:
                    return new CreateUserOutcome.UsernameAlreadyExists();
                case null:
                    throw;
            }
        }

        return new CreateUserOutcome.Success(new UserResponse(
            user.Id,
            user.Email,
            user.Username,
            user.FirstName,
            user.MiddleName,
            user.LastName,
            user.Suffix,
            user.JobTitle,
            user.IsDisabled,
            user.CreatedAt,
            user.DisabledAt,
            [.. knownRoles.OrderBy(role => role.Name, StringComparer.Ordinal)]));
    }
}
