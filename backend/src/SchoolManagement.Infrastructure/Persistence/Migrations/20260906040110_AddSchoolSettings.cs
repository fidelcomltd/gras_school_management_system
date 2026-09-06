using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "config_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    snapshot = table.Column<string>(type: "jsonb", maxLength: 256, nullable: false),
                    changed_group = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    actor_admin_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_config_versions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "school_profile",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    short_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    abbreviation = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    motto = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    head_teacher_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    timezone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    identity_version_number = table.Column<int>(type: "integer", nullable: false),
                    abbreviation_version_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_school_profile", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "school_profile",
                columns: new[] { "id", "abbreviation", "abbreviation_version_number", "address", "email", "head_teacher_name", "identity_version_number", "motto", "phone", "school_name", "short_name", "timezone" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), "GRAS", 0, "", "", "", 0, null, "", "", "", "Africa/Lagos" });

            migrationBuilder.CreateIndex(
                name: "ix_config_versions_version_number_unique",
                table: "config_versions",
                column: "version_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "config_versions");

            migrationBuilder.DropTable(
                name: "school_profile");
        }
    }
}
