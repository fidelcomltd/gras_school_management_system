using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "current_logo_group_id",
                table: "school_profile",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "current_signature_group_id",
                table: "school_profile",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "school_image",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    size_variant = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    upload_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    width_pixels = table.Column<int>(type: "integer", nullable: false),
                    height_pixels = table.Column<int>(type: "integer", nullable: false),
                    content_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_school_image", x => x.id);
                });

            migrationBuilder.UpdateData(
                table: "school_profile",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "current_logo_group_id", "current_signature_group_id" },
                values: new object[] { null, null });

            migrationBuilder.CreateIndex(
                name: "ix_school_image_upload_group_id_size_variant",
                table: "school_image",
                columns: new[] { "upload_group_id", "size_variant" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "school_image");

            migrationBuilder.DropColumn(
                name: "current_logo_group_id",
                table: "school_profile");

            migrationBuilder.DropColumn(
                name: "current_signature_group_id",
                table: "school_profile");
        }
    }
}
