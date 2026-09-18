using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResultRulesSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "result_rules_version_number",
                table: "school_profile",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "result_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    annual_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    weight_first = table.Column<int>(type: "integer", nullable: true),
                    weight_second = table.Column<int>(type: "integer", nullable: true),
                    weight_third = table.Column<int>(type: "integer", nullable: true),
                    primary_position_scope = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    show_level_position = table.Column<bool>(type: "boolean", nullable: false),
                    tie_break_rule = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    pass_mark = table.Column<int>(type: "integer", nullable: false),
                    promotion_threshold = table.Column<int>(type: "integer", nullable: false),
                    require_core_pass = table.Column<bool>(type: "boolean", nullable: false),
                    core_subject_ids = table.Column<string>(type: "text", nullable: false),
                    min_subjects_for_position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_result_rules", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "result_rules",
                columns: new[] { "id", "annual_method", "core_subject_ids", "min_subjects_for_position", "pass_mark", "primary_position_scope", "promotion_threshold", "require_core_pass", "show_level_position", "tie_break_rule", "weight_first", "weight_second", "weight_third" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000601"), "SimpleAverage", "", 1, 40, "Arm", 40, true, true, "SharedPosition", null, null, null });

            migrationBuilder.UpdateData(
                table: "school_profile",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "result_rules_version_number",
                value: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "result_rules");

            migrationBuilder.DropColumn(
                name: "result_rules_version_number",
                table: "school_profile");
        }
    }
}
