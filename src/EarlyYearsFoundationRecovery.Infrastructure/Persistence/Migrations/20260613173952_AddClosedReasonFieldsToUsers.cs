using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EarlyYearsFoundationRecovery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClosedReasonFieldsToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "closed_reason",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "closed_reason_custom",
                table: "users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "closed_reason",
                table: "users");

            migrationBuilder.DropColumn(
                name: "closed_reason_custom",
                table: "users");
        }
    }
}
