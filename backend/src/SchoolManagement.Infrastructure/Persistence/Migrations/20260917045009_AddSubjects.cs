using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subjects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    code_key = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subjects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subject_mapping",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    term_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subject_mapping", x => x.id);
                    table.ForeignKey(
                        name: "fk_subject_mapping_academic_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "academic_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subject_mapping_class_levels_class_level_id",
                        column: x => x.class_level_id,
                        principalTable: "class_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subject_mapping_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subject_mapping_terms_term_id",
                        column: x => x.term_id,
                        principalTable: "terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subject_mapping_exception",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    arm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    term_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subject_mapping_exception", x => x.id);
                    table.ForeignKey(
                        name: "fk_subject_mapping_exception_arms_arm_id",
                        column: x => x.arm_id,
                        principalTable: "arms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subject_mapping_exception_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subject_mapping_exception_terms_term_id",
                        column: x => x.term_id,
                        principalTable: "terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "subjects",
                columns: new[] { "id", "code", "code_key", "created_at_utc", "created_by", "description", "modified_at_utc", "modified_by", "name", "name_key", "status", "version" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000601"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Number work", "number work", "Active", new Guid("00000000-0000-0000-0000-000000000701") },
                    { new Guid("00000000-0000-0000-0000-000000000602"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Letter work", "letter work", "Active", new Guid("00000000-0000-0000-0000-000000000702") },
                    { new Guid("00000000-0000-0000-0000-000000000603"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Phonics", "phonics", "Active", new Guid("00000000-0000-0000-0000-000000000703") },
                    { new Guid("00000000-0000-0000-0000-000000000604"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Pre Science", "pre science", "Active", new Guid("00000000-0000-0000-0000-000000000704") },
                    { new Guid("00000000-0000-0000-0000-000000000605"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Social habit", "social habit", "Active", new Guid("00000000-0000-0000-0000-000000000705") },
                    { new Guid("00000000-0000-0000-0000-000000000606"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Health habit", "health habit", "Active", new Guid("00000000-0000-0000-0000-000000000706") },
                    { new Guid("00000000-0000-0000-0000-000000000607"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Handwriting", "handwriting", "Active", new Guid("00000000-0000-0000-0000-000000000707") },
                    { new Guid("00000000-0000-0000-0000-000000000608"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Creative skills", "creative skills", "Active", new Guid("00000000-0000-0000-0000-000000000708") },
                    { new Guid("00000000-0000-0000-0000-000000000609"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Rhyme", "rhyme", "Active", new Guid("00000000-0000-0000-0000-000000000709") },
                    { new Guid("00000000-0000-0000-0000-000000000610"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Literature", "literature", "Active", new Guid("00000000-0000-0000-0000-000000000710") },
                    { new Guid("00000000-0000-0000-0000-000000000611"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Computer Science", "computer science", "Active", new Guid("00000000-0000-0000-0000-000000000711") },
                    { new Guid("00000000-0000-0000-0000-000000000612"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Quantitative Reasoning", "quantitative reasoning", "Active", new Guid("00000000-0000-0000-0000-000000000712") },
                    { new Guid("00000000-0000-0000-0000-000000000613"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Verbal Reasoning", "verbal reasoning", "Active", new Guid("00000000-0000-0000-0000-000000000713") },
                    { new Guid("00000000-0000-0000-0000-000000000614"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Christian Religious Knowledge", "christian religious knowledge", "Active", new Guid("00000000-0000-0000-0000-000000000714") },
                    { new Guid("00000000-0000-0000-0000-000000000615"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Mathematics", "mathematics", "Active", new Guid("00000000-0000-0000-0000-000000000715") },
                    { new Guid("00000000-0000-0000-0000-000000000616"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "English Language", "english language", "Active", new Guid("00000000-0000-0000-0000-000000000716") },
                    { new Guid("00000000-0000-0000-0000-000000000617"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Phonics/Diction", "phonics/diction", "Active", new Guid("00000000-0000-0000-0000-000000000717") },
                    { new Guid("00000000-0000-0000-0000-000000000618"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Hand writing", "hand writing", "Active", new Guid("00000000-0000-0000-0000-000000000718") },
                    { new Guid("00000000-0000-0000-0000-000000000619"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Basic Science/Tech", "basic science/tech", "Active", new Guid("00000000-0000-0000-0000-000000000719") },
                    { new Guid("00000000-0000-0000-0000-000000000620"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Social Studies", "social studies", "Active", new Guid("00000000-0000-0000-0000-000000000720") },
                    { new Guid("00000000-0000-0000-0000-000000000621"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Health Education", "health education", "Active", new Guid("00000000-0000-0000-0000-000000000721") },
                    { new Guid("00000000-0000-0000-0000-000000000622"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Creative Art", "creative art", "Active", new Guid("00000000-0000-0000-0000-000000000722") },
                    { new Guid("00000000-0000-0000-0000-000000000623"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Agric Science", "agric science", "Active", new Guid("00000000-0000-0000-0000-000000000723") },
                    { new Guid("00000000-0000-0000-0000-000000000624"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Home Economics", "home economics", "Active", new Guid("00000000-0000-0000-0000-000000000724") },
                    { new Guid("00000000-0000-0000-0000-000000000625"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Civic Education", "civic education", "Active", new Guid("00000000-0000-0000-0000-000000000725") },
                    { new Guid("00000000-0000-0000-0000-000000000626"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "History", "history", "Active", new Guid("00000000-0000-0000-0000-000000000726") },
                    { new Guid("00000000-0000-0000-0000-000000000627"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "French", "french", "Active", new Guid("00000000-0000-0000-0000-000000000727") },
                    { new Guid("00000000-0000-0000-0000-000000000628"), null, null, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Igbo", "igbo", "Active", new Guid("00000000-0000-0000-0000-000000000728") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_subject_mapping_class_level_id",
                table: "subject_mapping",
                column: "class_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_subject_mapping_session_id",
                table: "subject_mapping",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_subject_mapping_subject_level_term_active_unique",
                table: "subject_mapping",
                columns: new[] { "subject_id", "class_level_id", "term_id" },
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_subject_mapping_term_id",
                table: "subject_mapping",
                column: "term_id");

            migrationBuilder.CreateIndex(
                name: "ix_subject_mapping_exception_arm_subject_term_unique",
                table: "subject_mapping_exception",
                columns: new[] { "arm_id", "subject_id", "term_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subject_mapping_exception_subject_id",
                table: "subject_mapping_exception",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_subject_mapping_exception_term_id",
                table: "subject_mapping_exception",
                column: "term_id");

            migrationBuilder.CreateIndex(
                name: "ix_subjects_code_key_unique",
                table: "subjects",
                column: "code_key",
                unique: true,
                filter: "code_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_subjects_name_key_unique",
                table: "subjects",
                column: "name_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subject_mapping");

            migrationBuilder.DropTable(
                name: "subject_mapping_exception");

            migrationBuilder.DropTable(
                name: "subjects");
        }
    }
}
