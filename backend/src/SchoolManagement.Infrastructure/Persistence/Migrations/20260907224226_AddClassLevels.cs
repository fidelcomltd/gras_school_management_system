using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClassLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "class_levels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    progression_order = table.Column<int>(type: "integer", nullable: false),
                    next_level_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_class_levels", x => x.id);
                    table.ForeignKey(
                        name: "fk_class_levels_class_levels_next_level_id",
                        column: x => x.next_level_id,
                        principalTable: "class_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sections", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "class_levels",
                columns: new[] { "id", "created_at_utc", "created_by", "modified_at_utc", "modified_by", "name", "name_key", "next_level_id", "progression_order", "section_id", "status", "version" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000319"), new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Primary 6", "primary 6", null, 9, new Guid("00000000-0000-0000-0000-000000000302"), "Active", new Guid("00000000-0000-0000-0000-000000000329") });

            migrationBuilder.InsertData(
                table: "sections",
                columns: new[] { "id", "created_at_utc", "created_by", "modified_at_utc", "modified_by", "name", "name_key", "version" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000301"), new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Nursery", "nursery", new Guid("00000000-0000-0000-0000-000000000303") },
                    { new Guid("00000000-0000-0000-0000-000000000302"), new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Primary", "primary", new Guid("00000000-0000-0000-0000-000000000304") }
                });

            migrationBuilder.InsertData(
                table: "class_levels",
                columns: new[] { "id", "created_at_utc", "created_by", "modified_at_utc", "modified_by", "name", "name_key", "next_level_id", "progression_order", "section_id", "status", "version" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000318"), new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Primary 5", "primary 5", new Guid("00000000-0000-0000-0000-000000000319"), 8, new Guid("00000000-0000-0000-0000-000000000302"), "Active", new Guid("00000000-0000-0000-0000-000000000328") },
                    { new Guid("00000000-0000-0000-0000-000000000317"), new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Primary 4", "primary 4", new Guid("00000000-0000-0000-0000-000000000318"), 7, new Guid("00000000-0000-0000-0000-000000000302"), "Active", new Guid("00000000-0000-0000-0000-000000000327") },
                    { new Guid("00000000-0000-0000-0000-000000000316"), new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Primary 3", "primary 3", new Guid("00000000-0000-0000-0000-000000000317"), 6, new Guid("00000000-0000-0000-0000-000000000302"), "Active", new Guid("00000000-0000-0000-0000-000000000326") },
                    { new Guid("00000000-0000-0000-0000-000000000315"), new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Primary 2", "primary 2", new Guid("00000000-0000-0000-0000-000000000316"), 5, new Guid("00000000-0000-0000-0000-000000000302"), "Active", new Guid("00000000-0000-0000-0000-000000000325") },
                    { new Guid("00000000-0000-0000-0000-000000000314"), new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Primary 1", "primary 1", new Guid("00000000-0000-0000-0000-000000000315"), 4, new Guid("00000000-0000-0000-0000-000000000302"), "Active", new Guid("00000000-0000-0000-0000-000000000324") },
                    { new Guid("00000000-0000-0000-0000-000000000313"), new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Nursery 3", "nursery 3", new Guid("00000000-0000-0000-0000-000000000314"), 3, new Guid("00000000-0000-0000-0000-000000000301"), "Active", new Guid("00000000-0000-0000-0000-000000000323") },
                    { new Guid("00000000-0000-0000-0000-000000000312"), new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Nursery 2", "nursery 2", new Guid("00000000-0000-0000-0000-000000000313"), 2, new Guid("00000000-0000-0000-0000-000000000301"), "Active", new Guid("00000000-0000-0000-0000-000000000322") },
                    { new Guid("00000000-0000-0000-0000-000000000311"), new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Nursery 1", "nursery 1", new Guid("00000000-0000-0000-0000-000000000312"), 1, new Guid("00000000-0000-0000-0000-000000000301"), "Active", new Guid("00000000-0000-0000-0000-000000000321") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_class_levels_name_key_unique",
                table: "class_levels",
                column: "name_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_class_levels_next_level_id",
                table: "class_levels",
                column: "next_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_class_levels_progression_order_active_unique",
                table: "class_levels",
                column: "progression_order",
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_sections_name_key_unique",
                table: "sections",
                column: "name_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "class_levels");

            migrationBuilder.DropTable(
                name: "sections");
        }
    }
}
