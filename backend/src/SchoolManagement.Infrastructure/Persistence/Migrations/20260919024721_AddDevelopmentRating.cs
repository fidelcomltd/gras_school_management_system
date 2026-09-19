using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDevelopmentRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "development_rating",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pupil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    indicator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating_scale_point_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comment = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_development_rating", x => x.id);
                    table.ForeignKey(
                        name: "fk_development_rating_development_indicator_indicator_id",
                        column: x => x.indicator_id,
                        principalTable: "development_indicator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_development_rating_pupils_pupil_id",
                        column: x => x.pupil_id,
                        principalTable: "pupils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_development_rating_rating_scale_points_rating_scale_point_id",
                        column: x => x.rating_scale_point_id,
                        principalTable: "rating_scale_point",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_development_rating_result_sets_result_set_id",
                        column: x => x.result_set_id,
                        principalTable: "result_set",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_development_rating_indicator_id",
                table: "development_rating",
                column: "indicator_id");

            migrationBuilder.CreateIndex(
                name: "ix_development_rating_pupil_id",
                table: "development_rating",
                column: "pupil_id");

            migrationBuilder.CreateIndex(
                name: "ix_development_rating_rating_scale_point_id",
                table: "development_rating",
                column: "rating_scale_point_id");

            migrationBuilder.CreateIndex(
                name: "ix_development_rating_result_set_pupil_indicator_unique",
                table: "development_rating",
                columns: new[] { "result_set_id", "pupil_id", "indicator_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "development_rating");
        }
    }
}
