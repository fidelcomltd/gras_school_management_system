using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPupils : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pupils",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    registration_number = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    surname = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    first_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    middle_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    sex = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    nationality = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    state_of_origin = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    lga = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    home_address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    previous_school = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    previous_class = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    other_information = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pupils", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pupils_registration_number_unique",
                table: "pupils",
                column: "registration_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pupils_surname_id",
                table: "pupils",
                columns: new[] { "surname", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pupils");
        }
    }
}
