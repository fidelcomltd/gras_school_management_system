using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDevelopmentDomainSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "development_domain",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    rating_scale_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allows_indicator_comment = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_development_domain", x => x.id);
                    table.ForeignKey(
                        name: "fk_development_domain_rating_scales_rating_scale_id",
                        column: x => x.rating_scale_id,
                        principalTable: "rating_scale",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_development_domain_sections_section_id",
                        column: x => x.section_id,
                        principalTable: "sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "development_indicator",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    domain_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_development_indicator", x => x.id);
                    table.ForeignKey(
                        name: "fk_development_indicator_development_domain_domain_id",
                        column: x => x.domain_id,
                        principalTable: "development_domain",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "development_domain",
                columns: new[] { "id", "allows_indicator_comment", "display_order", "name", "rating_scale_id", "section_id", "status" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000801"), true, 1, "Maths Readiness", new Guid("00000000-0000-0000-0000-000000000601"), new Guid("00000000-0000-0000-0000-000000000301"), "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000802"), true, 2, "Language/Communication Development", new Guid("00000000-0000-0000-0000-000000000601"), new Guid("00000000-0000-0000-0000-000000000301"), "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000803"), true, 3, "Personal & Physical Development", new Guid("00000000-0000-0000-0000-000000000601"), new Guid("00000000-0000-0000-0000-000000000301"), "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000804"), true, 4, "Social & Emotional", new Guid("00000000-0000-0000-0000-000000000601"), new Guid("00000000-0000-0000-0000-000000000301"), "Active" }
                });

            migrationBuilder.InsertData(
                table: "development_indicator",
                columns: new[] { "id", "display_order", "domain_id", "name", "status" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000901"), 1, new Guid("00000000-0000-0000-0000-000000000801"), "Ability to Count", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000902"), 2, new Guid("00000000-0000-0000-0000-000000000801"), "Write Number Clearly", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000903"), 3, new Guid("00000000-0000-0000-0000-000000000801"), "Ability to Recognize numbers", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000904"), 4, new Guid("00000000-0000-0000-0000-000000000801"), "Ability to Reason and Answer question", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000905"), 1, new Guid("00000000-0000-0000-0000-000000000802"), "Ability to recite letters", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000906"), 2, new Guid("00000000-0000-0000-0000-000000000802"), "Ability to Recognize Upper/Lower case", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000907"), 3, new Guid("00000000-0000-0000-0000-000000000802"), "Can write Upper/Lower case", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000908"), 4, new Guid("00000000-0000-0000-0000-000000000802"), "Can construct simple sentence", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000909"), 5, new Guid("00000000-0000-0000-0000-000000000802"), "Can identify object", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000910"), 6, new Guid("00000000-0000-0000-0000-000000000802"), "Can recognize similarities & differences", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000911"), 7, new Guid("00000000-0000-0000-0000-000000000802"), "Know Letters and Alphabet in sequence", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000912"), 8, new Guid("00000000-0000-0000-0000-000000000802"), "Can Trace Letters & Object", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000913"), 9, new Guid("00000000-0000-0000-0000-000000000802"), "State own name and write", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000914"), 10, new Guid("00000000-0000-0000-0000-000000000802"), "Expression of own feeling and thought", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000915"), 11, new Guid("00000000-0000-0000-0000-000000000802"), "Listen attentively and contribute to discussion", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000916"), 12, new Guid("00000000-0000-0000-0000-000000000802"), "Shows interest in books", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000917"), 13, new Guid("00000000-0000-0000-0000-000000000802"), "Speaks clearly", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000918"), 14, new Guid("00000000-0000-0000-0000-000000000802"), "Able to speak with right vocabulary", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000919"), 1, new Guid("00000000-0000-0000-0000-000000000803"), "Can Recognize different Colours", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000920"), 2, new Guid("00000000-0000-0000-0000-000000000803"), "Can recognize different shapes", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000921"), 3, new Guid("00000000-0000-0000-0000-000000000803"), "Hold Pencils Correctly & firmly", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000922"), 4, new Guid("00000000-0000-0000-0000-000000000803"), "Take simply instruction/directions", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000923"), 5, new Guid("00000000-0000-0000-0000-000000000803"), "Work independently", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000924"), 6, new Guid("00000000-0000-0000-0000-000000000803"), "Can run and jump well", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000925"), 7, new Guid("00000000-0000-0000-0000-000000000803"), "Can Catch, Bounce, and throw ball", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000926"), 8, new Guid("00000000-0000-0000-0000-000000000803"), "Move all parts of the body very well", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000927"), 9, new Guid("00000000-0000-0000-0000-000000000803"), "Fit small items together", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000928"), 10, new Guid("00000000-0000-0000-0000-000000000803"), "Logical reasoning", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000929"), 11, new Guid("00000000-0000-0000-0000-000000000803"), "Cleanliness", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000930"), 12, new Guid("00000000-0000-0000-0000-000000000803"), "Persistence", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000931"), 13, new Guid("00000000-0000-0000-0000-000000000803"), "Wear cloth independently", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000932"), 14, new Guid("00000000-0000-0000-0000-000000000803"), "Potty trained", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000933"), 15, new Guid("00000000-0000-0000-0000-000000000803"), "Home work on High Quality", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000934"), 1, new Guid("00000000-0000-0000-0000-000000000804"), "Happy at School", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000935"), 2, new Guid("00000000-0000-0000-0000-000000000804"), "Behaves Well in School", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000936"), 3, new Guid("00000000-0000-0000-0000-000000000804"), "Etiquette and manners", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000937"), 4, new Guid("00000000-0000-0000-0000-000000000804"), "Expression and Emotions and Feeling", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000938"), 5, new Guid("00000000-0000-0000-0000-000000000804"), "Accept Correction", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000939"), 6, new Guid("00000000-0000-0000-0000-000000000804"), "Honesty", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000940"), 7, new Guid("00000000-0000-0000-0000-000000000804"), "Obedient to Instruction", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000941"), 8, new Guid("00000000-0000-0000-0000-000000000804"), "Behaves well in Class", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000942"), 9, new Guid("00000000-0000-0000-0000-000000000804"), "Work and Mixes well with Others", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000943"), 10, new Guid("00000000-0000-0000-0000-000000000804"), "Concentration", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000944"), 11, new Guid("00000000-0000-0000-0000-000000000804"), "Attendance to Class", "Active" },
                    { new Guid("00000000-0000-0000-0000-000000000945"), 12, new Guid("00000000-0000-0000-0000-000000000804"), "Punctuality", "Active" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_development_domain_rating_scale_id",
                table: "development_domain",
                column: "rating_scale_id");

            migrationBuilder.CreateIndex(
                name: "ix_development_domain_section_id",
                table: "development_domain",
                column: "section_id");

            migrationBuilder.CreateIndex(
                name: "ix_development_indicator_domain_id",
                table: "development_indicator",
                column: "domain_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "development_indicator");

            migrationBuilder.DropTable(
                name: "development_domain");
        }
    }
}
