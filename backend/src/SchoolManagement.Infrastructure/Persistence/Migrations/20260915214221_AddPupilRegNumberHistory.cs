using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPupilRegNumberHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pupil_reg_number_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pupil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_registration_number = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    corrected_by = table.Column<Guid>(type: "uuid", nullable: true),
                    corrected_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pupil_reg_number_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_pupil_reg_number_history_pupils_pupil_id",
                        column: x => x.pupil_id,
                        principalTable: "pupils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pupil_reg_number_history_old_registration_number_unique",
                table: "pupil_reg_number_history",
                column: "old_registration_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pupil_reg_number_history_pupil_id",
                table: "pupil_reg_number_history",
                column: "pupil_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pupil_reg_number_history");
        }
    }
}
