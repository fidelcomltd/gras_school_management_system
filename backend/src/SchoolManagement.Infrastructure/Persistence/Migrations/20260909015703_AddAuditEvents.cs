using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_event",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    actor_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    before_json = table.Column<string>(type: "jsonb", maxLength: 256, nullable: true),
                    after_json = table.Column<string>(type: "jsonb", maxLength: 256, nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    source_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_event", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_event_actor_admin_id",
                table: "audit_event",
                column: "actor_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_event_occurred_at",
                table: "audit_event",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_event_outcome",
                table: "audit_event",
                column: "outcome");

            // Spec 14 §9.3: "the application's database role holds INSERT and SELECT on
            // audit_event and no UPDATE or DELETE." REVOKE FROM CURRENT_USER, not a named role: the
            // connecting role IS the application role in every environment this migration runs
            // against today (hosted Neon test database, CI's service container), and PostgreSQL
            // revoking a privilege from the object's OWNER is always a harmless no-op (ownership
            // grants are implicit, never represented as a revocable ACL entry) — so this is a
            // real guarantee wherever a future deployment provisions a genuinely separate,
            // non-owner application role, and a documented no-op everywhere it does not (root
            // CLAUDE.md §4.1 decision, 2026-09-09; ## Known drift: the separate administrative
            // pruning role is not provisioned anywhere this migration currently runs).
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON TABLE audit_event FROM CURRENT_USER;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("GRANT UPDATE, DELETE ON TABLE audit_event TO CURRENT_USER;");

            migrationBuilder.DropTable(
                name: "audit_event");
        }
    }
}
