using System.Text.RegularExpressions;

namespace Template.Api.Features.Users.Domain;

/// <summary>
/// Application user. Credentials and profile are owned by the Users feature; the Auth feature only
/// verifies credentials and issues tokens. Emails and usernames are stored normalized (trimmed,
/// lower-invariant) and are unique; usernames double as an alternative login identifier.
/// </summary>
public sealed partial class User
{
    public const int MaxEmailLength = 320;
    public const int MinUsernameLength = 3;
    public const int MaxUsernameLength = 64;
    public const int MaxNameLength = 64;
    public const int MaxSuffixLength = 16;
    public const int MaxJobTitleLength = 128;

    public Guid Id { get; private set; }

    public string Email { get; private set; } = null!;

    /// <summary>
    /// Unique login handle. Nullable at the storage level so the profile migration is expand-safe
    /// for pre-existing rows and rare seed conflicts; every API write path requires it.
    /// </summary>
    public string? Username { get; private set; }

    public string FirstName { get; private set; } = string.Empty;

    public string? MiddleName { get; private set; }

    public string LastName { get; private set; } = string.Empty;

    public string? Suffix { get; private set; }

    public string? JobTitle { get; private set; }

    public string PasswordHash { get; private set; } = null!;

    public bool IsDisabled { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? DisabledAt { get; private set; }

    public List<UserRole> UserRoles { get; } = [];

    private User()
    {
    }

    public static User Create(
        string email,
        string username,
        string firstName,
        string lastName,
        string passwordHash,
        DateTimeOffset now,
        string? middleName = null,
        string? suffix = null,
        string? jobTitle = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User
        {
            Id = Guid.NewGuid(),
            Email = NormalizeEmail(email),
            Username = NormalizeUsername(username),
            FirstName = NormalizeRequiredName(firstName, nameof(firstName)),
            MiddleName = NormalizeOptional(middleName, MaxNameLength, nameof(middleName)),
            LastName = NormalizeRequiredName(lastName, nameof(lastName)),
            Suffix = NormalizeOptional(suffix, MaxSuffixLength, nameof(suffix)),
            JobTitle = NormalizeOptional(jobTitle, MaxJobTitleLength, nameof(jobTitle)),
            PasswordHash = passwordHash,
            CreatedAt = now,
        };
    }

    /// <summary>Canonical form (trimmed, lower-invariant) used for storage, uniqueness, and lookups.</summary>
    public static string NormalizeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalized = email.Trim().ToLowerInvariant();
        if (normalized.Length > MaxEmailLength)
        {
            throw new ArgumentException($"Email must be at most {MaxEmailLength} characters.", nameof(email));
        }

        return normalized;
    }

    /// <summary>
    /// Canonical username form (trimmed, lower-invariant). Usernames are restricted to ASCII
    /// letters, digits, dot, underscore, and hyphen so they can never be confused with emails
    /// (which always contain '@') during identifier-based login.
    /// </summary>
    public static string NormalizeUsername(string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var normalized = username.Trim().ToLowerInvariant();
        if (!IsValidUsername(normalized))
        {
            throw new ArgumentException(
                $"Username must be {MinUsernameLength}-{MaxUsernameLength} characters and contain only ASCII letters, digits, '.', '_', or '-'.",
                nameof(username));
        }

        return normalized;
    }

    /// <summary>Boundary-friendly username check: charset and length only, no exceptions.</summary>
    public static bool IsValidUsername(string? username) =>
        username is not null &&
        username.Length >= MinUsernameLength &&
        username.Length <= MaxUsernameLength &&
        UsernamePattern().IsMatch(username);

    /// <summary>Full replacement of profile fields (username and names); optional fields are cleared when null.</summary>
    public void ChangeProfile(
        string username,
        string firstName,
        string lastName,
        string? middleName,
        string? suffix,
        string? jobTitle)
    {
        Username = NormalizeUsername(username);
        FirstName = NormalizeRequiredName(firstName, nameof(firstName));
        LastName = NormalizeRequiredName(lastName, nameof(lastName));
        MiddleName = NormalizeOptional(middleName, MaxNameLength, nameof(middleName));
        Suffix = NormalizeOptional(suffix, MaxSuffixLength, nameof(suffix));
        JobTitle = NormalizeOptional(jobTitle, MaxJobTitleLength, nameof(jobTitle));
    }

    public void RenameEmail(string email) => Email = NormalizeEmail(email);

    public void ChangePassword(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }

    public void Disable(DateTimeOffset now)
    {
        if (IsDisabled)
        {
            return;
        }

        IsDisabled = true;
        DisabledAt = now;
    }

    public void Enable()
    {
        IsDisabled = false;
        DisabledAt = null;
    }

    private static string NormalizeRequiredName(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim();
        if (normalized.Length > MaxNameLength)
        {
            throw new ArgumentException($"Value must be at most {MaxNameLength} characters.", paramName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value must be at most {maxLength} characters.", paramName);
        }

        return normalized;
    }

    [GeneratedRegex("^[a-zA-Z0-9._-]+$")]
    private static partial Regex UsernamePattern();
}
