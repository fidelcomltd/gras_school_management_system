using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResultComputationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pupil_term_result",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pupil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subjects_taken = table.Column<int>(type: "integer", nullable: false),
                    total_obtainable = table.Column<int>(type: "integer", nullable: false),
                    total_obtained = table.Column<int>(type: "integer", nullable: false),
                    average = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    overall_grade = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    arm_position = table.Column<int>(type: "integer", nullable: true),
                    arm_position_tied = table.Column<bool>(type: "boolean", nullable: false),
                    arm_pupil_count = table.Column<int>(type: "integer", nullable: false),
                    level_position = table.Column<int>(type: "integer", nullable: true),
                    level_position_tied = table.Column<bool>(type: "boolean", nullable: false),
                    level_pupil_count = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pupil_term_result", x => x.id);
                    table.ForeignKey(
                        name: "fk_pupil_term_result_pupils_pupil_id",
                        column: x => x.pupil_id,
                        principalTable: "pupils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pupil_term_result_result_sets_result_set_id",
                        column: x => x.result_set_id,
                        principalTable: "result_set",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subject_arm_statistic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    highest_score = table.Column<int>(type: "integer", nullable: true),
                    lowest_score = table.Column<int>(type: "integer", nullable: true),
                    class_average = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    counted_pupils = table.Column<int>(type: "integer", nullable: false),
                    ranked_pupils = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subject_arm_statistic", x => x.id);
                    table.ForeignKey(
                        name: "fk_subject_arm_statistic_result_set_result_set_id",
                        column: x => x.result_set_id,
                        principalTable: "result_set",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subject_arm_statistic_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subject_result_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pupil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ca_total = table.Column<int>(type: "integer", nullable: false),
                    exam_mark = table.Column<int>(type: "integer", nullable: true),
                    subject_total = table.Column<int>(type: "integer", nullable: false),
                    grade = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    remark = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    subject_position = table.Column<int>(type: "integer", nullable: true),
                    subject_position_tied = table.Column<bool>(type: "boolean", nullable: false),
                    is_pass = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subject_result_line", x => x.id);
                    table.ForeignKey(
                        name: "fk_subject_result_line_pupils_pupil_id",
                        column: x => x.pupil_id,
                        principalTable: "pupils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subject_result_line_result_set_result_set_id",
                        column: x => x.result_set_id,
                        principalTable: "result_set",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subject_result_line_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pupil_term_result_pupil_id",
                table: "pupil_term_result",
                column: "pupil_id");

            migrationBuilder.CreateIndex(
                name: "ix_pupil_term_result_result_set_id",
                table: "pupil_term_result",
                column: "result_set_id");

            migrationBuilder.CreateIndex(
                name: "ix_pupil_term_result_result_set_id_pupil_id_unique",
                table: "pupil_term_result",
                columns: new[] { "result_set_id", "pupil_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subject_arm_statistic_result_set_id",
                table: "subject_arm_statistic",
                column: "result_set_id");

            migrationBuilder.CreateIndex(
                name: "ix_subject_arm_statistic_result_set_id_subject_id_unique",
                table: "subject_arm_statistic",
                columns: new[] { "result_set_id", "subject_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subject_arm_statistic_subject_id",
                table: "subject_arm_statistic",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_subject_result_line_pupil_id",
                table: "subject_result_line",
                column: "pupil_id");

            migrationBuilder.CreateIndex(
                name: "ix_subject_result_line_result_set_id",
                table: "subject_result_line",
                column: "result_set_id");

            migrationBuilder.CreateIndex(
                name: "ix_subject_result_line_result_set_id_pupil_id",
                table: "subject_result_line",
                columns: new[] { "result_set_id", "pupil_id" });

            migrationBuilder.CreateIndex(
                name: "ix_subject_result_line_result_set_id_subject_id",
                table: "subject_result_line",
                columns: new[] { "result_set_id", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ix_subject_result_line_subject_id",
                table: "subject_result_line",
                column: "subject_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pupil_term_result");

            migrationBuilder.DropTable(
                name: "subject_arm_statistic");

            migrationBuilder.DropTable(
                name: "subject_result_line");
        }
    }
}
