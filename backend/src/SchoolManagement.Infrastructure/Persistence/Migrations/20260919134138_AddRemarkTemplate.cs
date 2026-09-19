using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRemarkTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "remark_template",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    text_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_remark_template", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_remark_template_kind_text_key_unique",
                table: "remark_template",
                columns: new[] { "kind", "text_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "remark_template");
        }
    }
}
