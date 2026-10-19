using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Template.Api.Features.Auth.Domain;
using Template.Api.Features.Users.Domain;

namespace Template.Api.Features.Auth.Persistence;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(token => token.Id);

        // SHA-256 hex digest of the token secret: 64 characters.
        builder.Property(token => token.TokenHash)
            .HasMaxLength(64)
            .IsRequired();
        builder.HasIndex(token => token.TokenHash).IsUnique();

        builder.Property(token => token.UserId).IsRequired();

        // Supports "revoke all active tokens for a user" (logout everywhere, disable, password reset).
        builder.HasIndex(token => new { token.UserId, token.RevokedAt });

        builder.Property(token => token.IssuedAt).IsRequired();
        builder.Property(token => token.ExpiresAt).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
