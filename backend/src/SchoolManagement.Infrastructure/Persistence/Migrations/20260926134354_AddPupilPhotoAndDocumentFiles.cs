using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPupilPhotoAndDocumentFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "photo_asset_id",
                table: "pupils",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "photo_thumbnail_asset_id",
                table: "pupils",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "photo_updated_at_utc",
                table: "pupils",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "file_asset_id",
                table: "pupil_document",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "file_content_type",
                table: "pupil_document",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "file_size_bytes",
                table: "pupil_document",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "file_uploaded_at_utc",
                table: "pupil_document",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "file_uploaded_by",
                table: "pupil_document",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "photo_asset_id",
                table: "pupils");

            migrationBuilder.DropColumn(
                name: "photo_thumbnail_asset_id",
                table: "pupils");

            migrationBuilder.DropColumn(
                name: "photo_updated_at_utc",
                table: "pupils");

            migrationBuilder.DropColumn(
                name: "file_asset_id",
                table: "pupil_document");

            migrationBuilder.DropColumn(
                name: "file_content_type",
                table: "pupil_document");

            migrationBuilder.DropColumn(
                name: "file_size_bytes",
                table: "pupil_document");

            migrationBuilder.DropColumn(
                name: "file_uploaded_at_utc",
                table: "pupil_document");

            migrationBuilder.DropColumn(
                name: "file_uploaded_by",
                table: "pupil_document");
        }
    }
}
