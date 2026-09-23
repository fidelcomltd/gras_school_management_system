using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "arm_weekly_setting",
                columns: table => new
                {
                    arm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    auto_publish = table.Column<bool>(type: "boolean", nullable: false),
                    last_auto_published_week_start = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arm_weekly_setting", x => x.arm_id);
                    table.ForeignKey(
                        name: "fk_arm_weekly_setting_arms_arm_id",
                        column: x => x.arm_id,
                        principalTable: "arms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "weekly_report",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pupil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    arm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    term_id = table.Column<Guid>(type: "uuid", nullable: false),
                    week_number = table.Column<int>(type: "integer", nullable: false),
                    week_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    week_end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    published_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_weekly_report", x => x.id);
                    table.CheckConstraint("ck_weekly_report_week_number", "week_number BETWEEN 1 AND 20");
                    table.ForeignKey(
                        name: "fk_weekly_report_arms_arm_id",
                        column: x => x.arm_id,
                        principalTable: "arms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_weekly_report_pupils_pupil_id",
                        column: x => x.pupil_id,
                        principalTable: "pupils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_weekly_report_terms_term_id",
                        column: x => x.term_id,
                        principalTable: "terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "weekly_report_day",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    weekly_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    report_date = table.Column<DateOnly>(type: "date", nullable: false),
                    behaviour = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    performance = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    dressing = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    home_work = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    eating = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    symptoms_of_illness = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    teacher_comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    parent_comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_weekly_report_day", x => x.id);
                    table.ForeignKey(
                        name: "fk_weekly_report_day_weekly_report_weekly_report_id",
                        column: x => x.weekly_report_id,
                        principalTable: "weekly_report",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_weekly_report_arm_term_week",
                table: "weekly_report",
                columns: new[] { "arm_id", "term_id", "week_number" });

            migrationBuilder.CreateIndex(
                name: "ix_weekly_report_pupil_term_week_unique",
                table: "weekly_report",
                columns: new[] { "pupil_id", "term_id", "week_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_weekly_report_term_id",
                table: "weekly_report",
                column: "term_id");

            migrationBuilder.CreateIndex(
                name: "ix_weekly_report_day_report_day_unique",
                table: "weekly_report_day",
                columns: new[] { "weekly_report_id", "day_of_week" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "arm_weekly_setting");

            migrationBuilder.DropTable(
                name: "weekly_report_day");

            migrationBuilder.DropTable(
                name: "weekly_report");
        }
    }
}
