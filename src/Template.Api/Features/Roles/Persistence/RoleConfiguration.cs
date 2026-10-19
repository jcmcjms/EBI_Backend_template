using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Template.Api.Features.Roles.Domain;

namespace Template.Api.Features.Roles.Persistence;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(role => role.Id);

        builder.Property(role => role.Name)
            .HasMaxLength(Role.MaxNameLength)
            .IsRequired();
        builder.HasIndex(role => role.Name).IsUnique();

        builder.Property(role => role.IsSystem).HasDefaultValue(false);
        builder.Property(role => role.CreatedAt).IsRequired();

        builder.HasMany(role => role.PermissionGrants)
            .WithOne(grant => grant.Role)
            .HasForeignKey(grant => grant.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
