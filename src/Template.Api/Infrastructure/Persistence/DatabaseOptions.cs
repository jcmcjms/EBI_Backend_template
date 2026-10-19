namespace Template.Api.Infrastructure.Persistence;

/// <summary>Operational database settings bound from the "Database" configuration section.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public const string ConnectionStringName = "Database";

    /// <summary>Bound on every EF Core command; prevents unbounded database waits under failure.</summary>
    public const int CommandTimeoutSeconds = 15;

    /// <summary>
    /// When true, pending EF Core migrations are applied and the seeder runs at startup.
    /// Intended for development and one-shot deployment jobs — not for simultaneously rolling fleets
    /// (see README "Deployment").
    /// </summary>
    public bool ApplyMigrations { get; init; }
}

/// <summary>Optional bootstrap data, bound from the "Seed" configuration section. Seeding only runs when migrations are applied at startup.</summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>When both email and password are set, an admin user with the system Admin role is created if it does not exist.</summary>
    public string AdminEmail { get; init; } = string.Empty;

    public string AdminPassword { get; init; } = string.Empty;

    /// <summary>Login handle for the seeded admin. Defaults to the local part of AdminEmail when it is a valid username.</summary>
    public string AdminUsername { get; init; } = string.Empty;
}
