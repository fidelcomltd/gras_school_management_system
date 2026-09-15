using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admission_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pupil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date_application_received = table.Column<DateOnly>(type: "date", nullable: true),
                    date_admitted = table.Column<DateOnly>(type: "date", nullable: false),
                    class_admitted_into = table.Column<Guid>(type: "uuid", nullable: false),
                    admission_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    admission_type_note = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    assessment_required = table.Column<bool>(type: "boolean", nullable: false),
                    assessment_result_remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    assigned_class_teacher = table.Column<Guid>(type: "uuid", nullable: true),
                    declaration_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    declaration_signed = table.Column<bool>(type: "boolean", nullable: false),
                    declaration_date = table.Column<DateOnly>(type: "date", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    head_of_school_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    head_of_school_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admission_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_admission_records_academic_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "academic_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_admission_records_class_levels_class_admitted_into",
                        column: x => x.class_admitted_into,
                        principalTable: "class_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_admission_records_pupils_pupil_id",
                        column: x => x.pupil_id,
                        principalTable: "pupils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admission_records_class_admitted_into",
                table: "admission_records",
                column: "class_admitted_into");

            migrationBuilder.CreateIndex(
                name: "ix_admission_records_pupil_id_unique",
                table: "admission_records",
                column: "pupil_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_admission_records_session_id",
                table: "admission_records",
                column: "session_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admission_records");
        }
    }
}
