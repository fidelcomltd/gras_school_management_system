using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeNotice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fee_label",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    show_on_portal = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fee_label", x => x.id);
                    table.ForeignKey(
                        name: "fk_fee_label_sections_section_id",
                        column: x => x.section_id,
                        principalTable: "sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "outstanding_fee",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pupil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outstanding_fee", x => x.id);
                    table.ForeignKey(
                        name: "fk_outstanding_fee_pupils_pupil_id",
                        column: x => x.pupil_id,
                        principalTable: "pupils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_outstanding_fee_result_sets_result_set_id",
                        column: x => x.result_set_id,
                        principalTable: "result_set",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fee_amount",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fee_label_id = table.Column<Guid>(type: "uuid", nullable: false),
                    term_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    modified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fee_amount", x => x.id);
                    table.ForeignKey(
                        name: "fk_fee_amount_class_levels_class_level_id",
                        column: x => x.class_level_id,
                        principalTable: "class_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_fee_amount_fee_labels_fee_label_id",
                        column: x => x.fee_label_id,
                        principalTable: "fee_label",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_fee_amount_terms_term_id",
                        column: x => x.term_id,
                        principalTable: "terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000101"),
                column: "privileges",
                value: "admin.create,admin.deactivate,admin.password.reset,admin.session.revoke,admin.suspend,admin.update,admin.view,arm.capacity.override,arm.create,arm.delete,arm.formteacher.assign,arm.update,arm.view,audit.export,audit.view,contact.create,contact.update,contact.view,fee.manage,level.create,level.deactivate,level.delete,level.update,level.view,pin.generate,pin.print,pin.revoke,pin.usage.view,pin.view,promotion.decide,promotion.reverse,promotion.run,pupil.admission.approve,pupil.admission.override,pupil.create,pupil.delete,pupil.document.manage,pupil.import,pupil.photo.update,pupil.regnumber.correct,pupil.safeguarding.update,pupil.safeguarding.view,pupil.status.update,pupil.transfer,pupil.update,pupil.view,report.export,report.view,result.annual.compute,result.approve,result.attendance.enter,result.compute,result.print,result.publish,result.remark.classteacher,result.remark.headteacher,result.return,result.score.enter,result.score.void,result.submit,result.trait.enter,result.unpublish,result.view,role.assign,role.create,role.delete,role.scope.assign,role.update,role.view,session.create,session.update,session.view,settings.abbreviation.update,settings.assessment.update,settings.developmentdomains.update,settings.grading.update,settings.identity.update,settings.pin.update,settings.ratingscales.update,settings.regnumber.update,settings.reset.defaults,settings.resultrules.update,settings.traits.update,settings.view,subject.create,subject.deactivate,subject.delete,subject.map,subject.map.arm,subject.unmap,subject.update,subject.view,term.close,term.open,weekly.enter,weekly.publish,weekly.view");

            // School Administrator and Bursar are editable by the school: ADD fee.manage rather than overwrite a list it may have changed.
            migrationBuilder.Sql("""
                UPDATE roles
                SET privileges = (
                    SELECT string_agg(code, ',' ORDER BY code COLLATE "C")
                    FROM (SELECT DISTINCT unnest(string_to_array(privileges, ',') || ARRAY['fee.manage']) AS code) AS codes)
                WHERE id = '00000000-0000-0000-0000-000000000102'
                  AND NOT ('fee.manage' = ANY (string_to_array(privileges, ',')));
                """);

            migrationBuilder.Sql("""
                UPDATE roles
                SET privileges = (
                    SELECT string_agg(code, ',' ORDER BY code COLLATE "C")
                    FROM (SELECT DISTINCT unnest(string_to_array(privileges, ',') || ARRAY['fee.manage']) AS code) AS codes)
                WHERE id = '00000000-0000-0000-0000-000000000105'
                  AND NOT ('fee.manage' = ANY (string_to_array(privileges, ',')));
                """);

            migrationBuilder.CreateIndex(
                name: "ix_fee_amount_class_level_id",
                table: "fee_amount",
                column: "class_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_fee_amount_label",
                table: "fee_amount",
                column: "fee_label_id");

            migrationBuilder.CreateIndex(
                name: "ix_fee_amount_term_level_label_unique",
                table: "fee_amount",
                columns: new[] { "term_id", "class_level_id", "fee_label_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fee_label_one_outstanding_per_section",
                table: "fee_label",
                column: "section_id",
                unique: true,
                filter: "kind = 'Outstanding'");

            migrationBuilder.CreateIndex(
                name: "ix_outstanding_fee_pupil",
                table: "outstanding_fee",
                column: "pupil_id");

            migrationBuilder.CreateIndex(
                name: "ix_outstanding_fee_result_set_pupil_unique",
                table: "outstanding_fee",
                columns: new[] { "result_set_id", "pupil_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fee_amount");

            migrationBuilder.DropTable(
                name: "outstanding_fee");

            migrationBuilder.DropTable(
                name: "fee_label");

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000101"),
                column: "privileges",
                value: "admin.create,admin.deactivate,admin.password.reset,admin.session.revoke,admin.suspend,admin.update,admin.view,arm.capacity.override,arm.create,arm.delete,arm.formteacher.assign,arm.update,arm.view,audit.export,audit.view,contact.create,contact.update,contact.view,level.create,level.deactivate,level.delete,level.update,level.view,pin.generate,pin.print,pin.revoke,pin.usage.view,pin.view,promotion.decide,promotion.reverse,promotion.run,pupil.admission.approve,pupil.admission.override,pupil.create,pupil.delete,pupil.document.manage,pupil.import,pupil.photo.update,pupil.regnumber.correct,pupil.safeguarding.update,pupil.safeguarding.view,pupil.status.update,pupil.transfer,pupil.update,pupil.view,report.export,report.view,result.annual.compute,result.approve,result.attendance.enter,result.compute,result.print,result.publish,result.remark.classteacher,result.remark.headteacher,result.return,result.score.enter,result.score.void,result.submit,result.trait.enter,result.unpublish,result.view,role.assign,role.create,role.delete,role.scope.assign,role.update,role.view,session.create,session.update,session.view,settings.abbreviation.update,settings.assessment.update,settings.developmentdomains.update,settings.grading.update,settings.identity.update,settings.pin.update,settings.ratingscales.update,settings.regnumber.update,settings.reset.defaults,settings.resultrules.update,settings.traits.update,settings.view,subject.create,subject.deactivate,subject.delete,subject.map,subject.map.arm,subject.unmap,subject.update,subject.view,term.close,term.open,weekly.enter,weekly.publish,weekly.view");

            migrationBuilder.Sql("""
                UPDATE roles
                SET privileges = array_to_string(array_remove(string_to_array(privileges, ','), 'fee.manage'), ',')
                WHERE id = '00000000-0000-0000-0000-000000000102';
                """);

            migrationBuilder.Sql("""
                UPDATE roles
                SET privileges = array_to_string(array_remove(string_to_array(privileges, ','), 'fee.manage'), ',')
                WHERE id = '00000000-0000-0000-0000-000000000105';
                """);
        }
    }
}
