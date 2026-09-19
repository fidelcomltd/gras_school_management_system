using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTraitRatingAndSectionRatesTraits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "rates_traits",
                table: "sections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "trait_rating",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pupil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trait_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating_scale_point_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trait_rating", x => x.id);
                    table.ForeignKey(
                        name: "fk_trait_rating_pupils_pupil_id",
                        column: x => x.pupil_id,
                        principalTable: "pupils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trait_rating_rating_scale_point_rating_scale_point_id",
                        column: x => x.rating_scale_point_id,
                        principalTable: "rating_scale_point",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trait_rating_result_set_result_set_id",
                        column: x => x.result_set_id,
                        principalTable: "result_set",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trait_rating_trait_trait_id",
                        column: x => x.trait_id,
                        principalTable: "trait",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "sections",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000301"),
                column: "rates_traits",
                value: false);

            migrationBuilder.UpdateData(
                table: "sections",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000302"),
                column: "rates_traits",
                value: true);

            migrationBuilder.CreateIndex(
                name: "ix_trait_rating_pupil_id",
                table: "trait_rating",
                column: "pupil_id");

            migrationBuilder.CreateIndex(
                name: "ix_trait_rating_rating_scale_point_id",
                table: "trait_rating",
                column: "rating_scale_point_id");

            migrationBuilder.CreateIndex(
                name: "ix_trait_rating_result_set_pupil_trait_unique",
                table: "trait_rating",
                columns: new[] { "result_set_id", "pupil_id", "trait_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trait_rating_trait_id",
                table: "trait_rating",
                column: "trait_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trait_rating");

            migrationBuilder.DropColumn(
                name: "rates_traits",
                table: "sections");
        }
    }
}
