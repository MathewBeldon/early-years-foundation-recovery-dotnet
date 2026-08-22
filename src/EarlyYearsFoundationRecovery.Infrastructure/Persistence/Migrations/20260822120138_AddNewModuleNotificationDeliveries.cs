using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EarlyYearsFoundationRecovery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNewModuleNotificationDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dotnet_new_module_notification_deliveries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    module_release_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    template_id = table.Column<string>(type: "text", nullable: false),
                    module_position = table.Column<int>(type: "integer", nullable: false),
                    module_title = table.Column<string>(type: "text", nullable: false),
                    module_criteria = table.Column<string>(type: "text", nullable: false),
                    public_url = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dotnet_new_module_notification_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "fk_dotnet_new_module_notification_deliveries_module_releases_m",
                        column: x => x.module_release_id,
                        principalTable: "module_releases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_dotnet_new_module_notification_deliveries_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dotnet_new_module_notification_deliveries_module_release_id",
                table: "dotnet_new_module_notification_deliveries",
                columns: new[] { "module_release_id", "user_id", "template_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dotnet_new_module_notification_deliveries_user_id",
                table: "dotnet_new_module_notification_deliveries",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dotnet_new_module_notification_deliveries");
        }
    }
}
