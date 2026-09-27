using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "promotion_batch",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    committed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    committed_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    reversed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    reversed_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    reversal_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_promotion_batch", x => x.id);
                    table.ForeignKey(
                        name: "fk_promotion_batch_academic_sessions_source_session_id",
                        column: x => x.source_session_id,
                        principalTable: "academic_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_promotion_batch_academic_sessions_target_session_id",
                        column: x => x.target_session_id,
                        principalTable: "academic_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "promotion_decision",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pupil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_arm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_arm_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    closed_enrolment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    new_enrolment_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_promotion_decision", x => x.id);
                    table.ForeignKey(
                        name: "fk_promotion_decision_promotion_batch_batch_id",
                        column: x => x.batch_id,
                        principalTable: "promotion_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_promotion_decision_pupils_pupil_id",
                        column: x => x.pupil_id,
                        principalTable: "pupils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_promotion_batch_one_committed_per_session",
                table: "promotion_batch",
                column: "source_session_id",
                unique: true,
                filter: "state = 'Committed'");

            migrationBuilder.CreateIndex(
                name: "ix_promotion_batch_target_session_id",
                table: "promotion_batch",
                column: "target_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_promotion_decision_batch_pupil_unique",
                table: "promotion_decision",
                columns: new[] { "batch_id", "pupil_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_promotion_decision_pupil",
                table: "promotion_decision",
                column: "pupil_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "promotion_decision");

            migrationBuilder.DropTable(
                name: "promotion_batch");
        }
    }
}
