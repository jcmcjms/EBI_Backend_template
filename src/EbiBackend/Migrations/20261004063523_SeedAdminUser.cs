using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbiBackend.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdminUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Password: Admin123! (BCrypt hash with work factor 12)
            var passwordHash = "$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj/RK.PZvO.S";
            
            migrationBuilder.Sql($@"
                IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Email] = 'admin@ebi.local')
                BEGIN
                    INSERT INTO [Users] ([Id], [Email], [Username], [DisplayName], [PasswordHash], [CreatedAt])
                    VALUES (NEWID(), 'admin@ebi.local', 'admin', 'System Administrator', '{passwordHash}', GETUTCDATE())
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [Users] WHERE [Email] = 'admin@ebi.local'");
        }
    }
}
