using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTraitSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "traits_version_number",
                table: "school_profile",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "trait",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    domain = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trait", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trait_block",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rating_scale_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trait_block", x => x.id);
                    table.ForeignKey(
                        name: "fk_trait_block_rating_scale_rating_scale_id",
                        column: x => x.rating_scale_id,
                        principalTable: "rating_scale",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "school_profile",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "traits_version_number",
                value: 0);

            migrationBuilder.InsertData(
                table: "trait",
                columns: new[] { "id", "display_order", "domain", "name", "status" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000001001"), 1, "Affective", "Conduct", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000001002"), 2, "Affective", "Punctuality", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000001003"), 3, "Affective", "Honesty", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000001004"), 4, "Affective", "Neatness", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000001005"), 5, "Affective", "Attitude", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000001006"), 6, "Affective", "Attentiveness", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000001007"), 7, "Affective", "Co-operation", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000001008"), 8, "Affective", "Skills", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000001009"), 9, "Affective", "Perseverance", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000001010"), 10, "Affective", "Obedient", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000001011"), 11, "Affective", "Fluency", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000001012"), 1, "Psychomotor", "Sports", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000001013"), 2, "Psychomotor", "Social activities", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000001014"), 3, "Psychomotor", "Painting and drawing", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000001015"), 4, "Psychomotor", "Hand writing", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000001016"), 5, "Psychomotor", "Mathematical Skills", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000001017"), 6, "Psychomotor", "Reasoning", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000001018"), 7, "Psychomotor", "Health", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000001019"), 8, "Psychomotor", "Creativity", "Active" }
                });

            migrationBuilder.InsertData(
                table: "trait_block",
                columns: new[] { "id", "rating_scale_id" },
                values: new object[,]
                {
                    { "Affective", new Guid("00000000-0000-0000-0000-000000000602") },
                    { "Psychomotor", new Guid("00000000-0000-0000-0000-000000000602") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_trait_block_rating_scale_id",
                table: "trait_block",
                column: "rating_scale_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trait");

            migrationBuilder.DropTable(
                name: "trait_block");

            migrationBuilder.DropColumn(
                name: "traits_version_number",
                table: "school_profile");
        }
    }
}
