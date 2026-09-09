using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegNumberSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "reg_number_version_number",
                table: "school_profile",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "separator",
                table: "school_profile",
                type: "character varying(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "serial_reset",
                table: "school_profile",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "serial_width",
                table: "school_profile",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "registration_counter",
                columns: table => new
                {
                    counter_key = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    last_serial = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_registration_counter", x => x.counter_key);
                });

            migrationBuilder.UpdateData(
                table: "school_profile",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "reg_number_version_number", "separator", "serial_reset", "serial_width" },
                values: new object[] { 0, "/", "PerYear", 4 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registration_counter");

            migrationBuilder.DropColumn(
                name: "reg_number_version_number",
                table: "school_profile");

            migrationBuilder.DropColumn(
                name: "separator",
                table: "school_profile");

            migrationBuilder.DropColumn(
                name: "serial_reset",
                table: "school_profile");

            migrationBuilder.DropColumn(
                name: "serial_width",
                table: "school_profile");
        }
    }
}
