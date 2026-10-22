using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityService.Infrastructure.Migrations
{
    public partial class AddSingleClientSession : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "active_session_id",
                table: "users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "session_id",
                table: "refresh_tokens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id_session_id",
                table: "refresh_tokens",
                columns: new[] { "user_id", "session_id" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_user_id_session_id",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "active_session_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "session_id",
                table: "refresh_tokens");
        }
    }
}
