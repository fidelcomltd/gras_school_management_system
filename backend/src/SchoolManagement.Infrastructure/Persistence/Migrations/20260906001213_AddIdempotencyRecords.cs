using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "idempotency_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    caller = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    fingerprint_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    response_status_code = table.Column<int>(type: "integer", nullable: true),
                    response_content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    response_body_json = table.Column<string>(type: "text", maxLength: 256, nullable: true),
                    response_location = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_records", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_records_expires_at_utc",
                table: "idempotency_records",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_records_key_hash_caller_unique",
                table: "idempotency_records",
                columns: new[] { "key_hash", "caller" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_records");
        }
    }
}
