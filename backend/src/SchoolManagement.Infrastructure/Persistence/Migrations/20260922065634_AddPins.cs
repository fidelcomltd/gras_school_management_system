using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pin_batch",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    purpose_note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    pin_length = table.Column<int>(type: "integer", nullable: false),
                    max_uses = table.Column<int>(type: "integer", nullable: false),
                    pin_count = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    plaintext_purge_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    generated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    generated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    revoked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    revoke_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pin_batch", x => x.id);
                    table.ForeignKey(
                        name: "fk_pin_batch_academic_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "academic_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pin",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pin_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    lookup_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    prefix = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    ciphertext = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    max_uses = table.Column<int>(type: "integer", nullable: false),
                    use_count = table.Column<int>(type: "integer", nullable: false),
                    distinct_pupil_count = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    state_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    revoked_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pin", x => x.id);
                    table.ForeignKey(
                        name: "fk_pin_pin_batch_batch_id",
                        column: x => x.batch_id,
                        principalTable: "pin_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pin_batch_id",
                table: "pin",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_pin_prefix",
                table: "pin",
                column: "prefix");

            migrationBuilder.CreateIndex(
                name: "ux_pin_lookup_key",
                table: "pin",
                column: "lookup_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pin_batch_session_id",
                table: "pin_batch",
                column: "session_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pin");

            migrationBuilder.DropTable(
                name: "pin_batch");
        }
    }
}
