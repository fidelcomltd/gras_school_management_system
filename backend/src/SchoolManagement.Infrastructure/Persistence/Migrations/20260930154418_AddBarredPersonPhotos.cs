using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBarredPersonPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "photo_id",
                table: "barred_person",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "barred_person_photo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pupil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_barred_person_photo", x => x.id);
                    table.ForeignKey(
                        name: "fk_barred_person_photo_pupils_pupil_id",
                        column: x => x.pupil_id,
                        principalTable: "pupils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_barred_person_photo_id",
                table: "barred_person",
                column: "photo_id");

            migrationBuilder.CreateIndex(
                name: "ix_barred_person_photo_pupil",
                table: "barred_person_photo",
                column: "pupil_id");

            migrationBuilder.AddForeignKey(
                name: "fk_barred_person_barred_person_photo_photo_id",
                table: "barred_person",
                column: "photo_id",
                principalTable: "barred_person_photo",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_barred_person_barred_person_photo_photo_id",
                table: "barred_person");

            migrationBuilder.DropTable(
                name: "barred_person_photo");

            migrationBuilder.DropIndex(
                name: "ix_barred_person_photo_id",
                table: "barred_person");

            migrationBuilder.DropColumn(
                name: "photo_id",
                table: "barred_person");
        }
    }
}
