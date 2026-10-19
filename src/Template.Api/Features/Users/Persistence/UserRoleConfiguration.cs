using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Template.Api.Features.Users.Domain;

namespace Template.Api.Features.Users.Persistence;

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");
        builder.HasKey(userRole => new { userRole.UserId, userRole.RoleId });
        builder.Property(userRole => userRole.AssignedAt).IsRequired();

        // Restrict: deleting a role that is still assigned must fail at the database level too,
        // matching the API-level guard in DeleteRoleUseCase.
        builder.HasOne(userRole => userRole.Role)
            .WithMany(role => role.UserRoles)
            .HasForeignKey(userRole => userRole.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
