using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGradingAndAssessmentSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "assessment_version_number",
                table: "school_profile",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "grading_version_number",
                table: "school_profile",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "assessment_component",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    short_label = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    max_mark = table.Column<int>(type: "integer", nullable: false),
                    is_examination = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessment_component", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "grading_band",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lower_bound = table.Column<int>(type: "integer", nullable: false),
                    upper_bound = table.Column<int>(type: "integer", nullable: false),
                    grade_letter = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    remark = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grading_band", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "assessment_component",
                columns: new[] { "id", "display_order", "is_examination", "max_mark", "name", "short_label" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000501"), 1, false, 20, "1st CA", "CA1" },
                    { new Guid("00000000-0000-0000-0000-000000000502"), 2, false, 20, "2nd CA", "CA2" },
                    { new Guid("00000000-0000-0000-0000-000000000503"), 3, true, 60, "Exam", "EXAM" }
                });

            migrationBuilder.InsertData(
                table: "grading_band",
                columns: new[] { "id", "display_order", "grade_letter", "lower_bound", "remark", "upper_bound" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000401"), 1, "A+", 90, "Very excellent", 100 },
                    { new Guid("00000000-0000-0000-0000-000000000402"), 2, "A", 85, "Excellent", 89 },
                    { new Guid("00000000-0000-0000-0000-000000000403"), 3, "B", 75, "Very good", 84 },
                    { new Guid("00000000-0000-0000-0000-000000000404"), 4, "B-", 70, "Good", 74 },
                    { new Guid("00000000-0000-0000-0000-000000000405"), 5, "C+", 60, "Average", 69 },
                    { new Guid("00000000-0000-0000-0000-000000000406"), 6, "C", 50, "Fair", 59 },
                    { new Guid("00000000-0000-0000-0000-000000000407"), 7, "D", 40, "More effort", 49 },
                    { new Guid("00000000-0000-0000-0000-000000000408"), 8, "E", 20, "Not Now", 39 },
                    { new Guid("00000000-0000-0000-0000-000000000409"), 9, "F", 0, "Fail", 19 }
                });

            migrationBuilder.UpdateData(
                table: "school_profile",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "assessment_version_number", "grading_version_number" },
                values: new object[] { 0, 0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assessment_component");

            migrationBuilder.DropTable(
                name: "grading_band");

            migrationBuilder.DropColumn(
                name: "assessment_version_number",
                table: "school_profile");

            migrationBuilder.DropColumn(
                name: "grading_version_number",
                table: "school_profile");
        }
    }
}
