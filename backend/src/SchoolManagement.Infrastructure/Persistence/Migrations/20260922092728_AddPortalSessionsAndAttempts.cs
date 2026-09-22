using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPortalSessionsAndAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pin_use",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pupil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    opened_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ended_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    source_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    viewed_json = table.Column<string>(type: "jsonb", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pin_use", x => x.id);
                    table.ForeignKey(
                        name: "fk_pin_use_pin_pin_id",
                        column: x => x.pin_id,
                        principalTable: "pin",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pin_use_pupils_pupil_id",
                        column: x => x.pupil_id,
                        principalTable: "pupils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "portal_attempt",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    registration_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    pin_prefix = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    pin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    attempted_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portal_attempt", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pin_use_pin_id_opened_at_utc",
                table: "pin_use",
                columns: new[] { "pin_id", "opened_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_pin_use_pupil_id",
                table: "pin_use",
                column: "pupil_id");

            migrationBuilder.CreateIndex(
                name: "ux_pin_use_token_hash",
                table: "pin_use",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_portal_attempt_pin_id",
                table: "portal_attempt",
                column: "pin_id");

            migrationBuilder.CreateIndex(
                name: "ix_portal_attempt_registration_number_attempted_at_utc",
                table: "portal_attempt",
                columns: new[] { "registration_number", "attempted_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_portal_attempt_source_address_attempted_at_utc",
                table: "portal_attempt",
                columns: new[] { "source_address", "attempted_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pin_use");

            migrationBuilder.DropTable(
                name: "portal_attempt");
        }
    }
}
