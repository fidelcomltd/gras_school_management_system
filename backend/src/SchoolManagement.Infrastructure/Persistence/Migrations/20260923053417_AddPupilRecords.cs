using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPupilRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "authorised_pickup_person",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pupil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    relationship = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_authorised_pickup_person", x => x.id);
                    table.ForeignKey(
                        name: "fk_authorised_pickup_person_pupils_pupil_id",
                        column: x => x.pupil_id,
                        principalTable: "pupils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "barred_person",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pupil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    details = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_barred_person", x => x.id);
                    table.ForeignKey(
                        name: "fk_barred_person_pupils_pupil_id",
                        column: x => x.pupil_id,
                        principalTable: "pupils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "barred_person_answer",
                columns: table => new
                {
                    pupil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    has_barred_persons = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_barred_person_answer", x => x.pupil_id);
                    table.ForeignKey(
                        name: "fk_barred_person_answer_pupils_pupil_id",
                        column: x => x.pupil_id,
                        principalTable: "pupils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pupil_contact",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pupil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    full_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    relationship = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    whatsapp_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    occupation = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    is_primary_contact = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pupil_contact", x => x.id);
                    table.ForeignKey(
                        name: "fk_pupil_contact_pupils_pupil_id",
                        column: x => x.pupil_id,
                        principalTable: "pupils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pupil_document",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pupil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    other_label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    received = table.Column<bool>(type: "boolean", nullable: false),
                    received_date = table.Column<DateOnly>(type: "date", nullable: true),
                    remarks = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    received_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pupil_document", x => x.id);
                    table.ForeignKey(
                        name: "fk_pupil_document_pupils_pupil_id",
                        column: x => x.pupil_id,
                        principalTable: "pupils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pupil_health",
                columns: table => new
                {
                    pupil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    has_allergy = table.Column<bool>(type: "boolean", nullable: true),
                    allergy_details = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    has_medical_condition = table.Column<bool>(type: "boolean", nullable: true),
                    medical_condition_details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    takes_regular_medication = table.Column<bool>(type: "boolean", nullable: true),
                    medication_details = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    special_instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    preferred_hospital = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    hospital_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    blood_group = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    genotype = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pupil_health", x => x.pupil_id);
                    table.ForeignKey(
                        name: "fk_pupil_health_pupils_pupil_id",
                        column: x => x.pupil_id,
                        principalTable: "pupils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_authorised_pickup_person_pupil",
                table: "authorised_pickup_person",
                column: "pupil_id");

            migrationBuilder.CreateIndex(
                name: "ix_barred_person_pupil",
                table: "barred_person",
                column: "pupil_id");

            migrationBuilder.CreateIndex(
                name: "ix_pupil_contact_phone",
                table: "pupil_contact",
                column: "phone");

            migrationBuilder.CreateIndex(
                name: "ix_pupil_contact_pupil_role_unique",
                table: "pupil_contact",
                columns: new[] { "pupil_id", "role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pupil_document_pupil_type_unique",
                table: "pupil_document",
                columns: new[] { "pupil_id", "document_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "authorised_pickup_person");

            migrationBuilder.DropTable(
                name: "barred_person");

            migrationBuilder.DropTable(
                name: "barred_person_answer");

            migrationBuilder.DropTable(
                name: "pupil_contact");

            migrationBuilder.DropTable(
                name: "pupil_document");

            migrationBuilder.DropTable(
                name: "pupil_health");
        }
    }
}
