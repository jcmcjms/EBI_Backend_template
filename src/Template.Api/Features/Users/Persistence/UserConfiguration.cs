using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Template.Api.Features.Users.Domain;

namespace Template.Api.Features.Users.Persistence;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Email)
            .HasMaxLength(User.MaxEmailLength)
            .IsRequired();
        builder.HasIndex(user => user.Email).IsUnique();

        // Nullable column + filtered unique index: expand-safe for rows created before usernames
        // existed (SQL Server unique indexes reject duplicate NULLs only without the filter).
        // Every API write path requires a username; login-by-username simply never matches NULL.
        builder.Property(user => user.Username)
            .HasMaxLength(User.MaxUsernameLength);
        builder.HasIndex(user => user.Username)
            .IsUnique()
            .HasFilter($"[{nameof(User.Username)}] IS NOT NULL");

        // Defaults keep the additive migration safe on tables that already contain rows
        // (pre-profile users land with empty names; backfill them via PUT /api/users/{id}/profile).
        builder.Property(user => user.FirstName)
            .HasMaxLength(User.MaxNameLength)
            .HasDefaultValue(string.Empty)
            .IsRequired();
        builder.Property(user => user.MiddleName)
            .HasMaxLength(User.MaxNameLength);
        builder.Property(user => user.LastName)
            .HasMaxLength(User.MaxNameLength)
            .HasDefaultValue(string.Empty)
            .IsRequired();
        builder.Property(user => user.Suffix)
            .HasMaxLength(User.MaxSuffixLength);
        builder.Property(user => user.JobTitle)
            .HasMaxLength(User.MaxJobTitleLength);

        // PBKDF2 encoded hash: prefix + iterations + base64 salt (16 bytes) + base64 subkey (32 bytes).
        builder.Property(user => user.PasswordHash)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(user => user.IsDisabled).HasDefaultValue(false);
        builder.Property(user => user.CreatedAt).IsRequired();

        builder.HasMany(user => user.UserRoles)
            .WithOne()
            .HasForeignKey(userRole => userRole.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
