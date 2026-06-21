using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EarlyYearsFoundationRecovery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_responses_user_id",
                table: "responses");

            migrationBuilder.DropIndex(
                name: "ix_notes_user_id",
                table: "notes");

            migrationBuilder.DropIndex(
                name: "ix_assessments_user_id",
                table: "assessments");

            migrationBuilder.CreateIndex(
                name: "ix_responses_user_id_training_module_assessment_id_question_na",
                table: "responses",
                columns: new[] { "user_id", "training_module", "assessment_id", "question_name" });

            migrationBuilder.CreateIndex(
                name: "ix_responses_user_id_training_module_question_name",
                table: "responses",
                columns: new[] { "user_id", "training_module", "question_name" });

            migrationBuilder.CreateIndex(
                name: "ix_notes_user_id_training_module_name",
                table: "notes",
                columns: new[] { "user_id", "training_module", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_notes_user_id_training_module_updated_at",
                table: "notes",
                columns: new[] { "user_id", "training_module", "updated_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_assessments_user_id_training_module",
                table: "assessments",
                columns: new[] { "user_id", "training_module" },
                unique: true,
                filter: "completed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_assessments_user_id_training_module_started_at",
                table: "assessments",
                columns: new[] { "user_id", "training_module", "started_at" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_responses_user_id_training_module_assessment_id_question_na",
                table: "responses");

            migrationBuilder.DropIndex(
                name: "ix_responses_user_id_training_module_question_name",
                table: "responses");

            migrationBuilder.DropIndex(
                name: "ix_notes_user_id_training_module_name",
                table: "notes");

            migrationBuilder.DropIndex(
                name: "ix_notes_user_id_training_module_updated_at",
                table: "notes");

            migrationBuilder.DropIndex(
                name: "ix_assessments_user_id_training_module",
                table: "assessments");

            migrationBuilder.DropIndex(
                name: "ix_assessments_user_id_training_module_started_at",
                table: "assessments");

            migrationBuilder.CreateIndex(
                name: "ix_responses_user_id",
                table: "responses",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_notes_user_id",
                table: "notes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessments_user_id",
                table: "assessments",
                column: "user_id");
        }
    }
}
