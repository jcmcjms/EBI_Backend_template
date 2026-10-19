using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Template.Api.Features.Roles.Domain;

namespace Template.Api.Features.Roles.Persistence;

public sealed class PermissionGrantConfiguration : IEntityTypeConfiguration<PermissionGrant>
{
    public void Configure(EntityTypeBuilder<PermissionGrant> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(grant => new { grant.RoleId, grant.Permission });

        builder.Property(grant => grant.Permission)
            .HasMaxLength(PermissionGrant.MaxPermissionLength);
        builder.Property(grant => grant.GrantedAt).IsRequired();
    }
}
