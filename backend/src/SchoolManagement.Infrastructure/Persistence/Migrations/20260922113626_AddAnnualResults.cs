using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnualResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "annual_result",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    arm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pupil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terms_counted = table.Column<int>(type: "integer", nullable: false),
                    first_term_average = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    second_term_average = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    third_term_average = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    first_term_total = table.Column<int>(type: "integer", nullable: true),
                    second_term_total = table.Column<int>(type: "integer", nullable: true),
                    third_term_total = table.Column<int>(type: "integer", nullable: true),
                    grand_total = table.Column<int>(type: "integer", nullable: false),
                    cumulative_average = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    cumulative_grade = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    cumulative_remark = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    annual_position = table.Column<int>(type: "integer", nullable: true),
                    annual_position_tied = table.Column<bool>(type: "boolean", nullable: false),
                    annual_pupil_count = table.Column<int>(type: "integer", nullable: false),
                    subjects_json = table.Column<string>(type: "jsonb", maxLength: 256, nullable: false),
                    proposed_outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    computed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_annual_result", x => x.id);
                    table.ForeignKey(
                        name: "fk_annual_result_academic_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "academic_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_annual_result_arms_arm_id",
                        column: x => x.arm_id,
                        principalTable: "arms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_annual_result_pupils_pupil_id",
                        column: x => x.pupil_id,
                        principalTable: "pupils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_annual_result_arm_id",
                table: "annual_result",
                column: "arm_id");

            migrationBuilder.CreateIndex(
                name: "ix_annual_result_pupil_id",
                table: "annual_result",
                column: "pupil_id");

            migrationBuilder.CreateIndex(
                name: "ux_annual_result_session_id_pupil_id",
                table: "annual_result",
                columns: new[] { "session_id", "pupil_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "annual_result");
        }
    }
}
