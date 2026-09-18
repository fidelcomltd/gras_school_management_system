using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRatingScaleSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "rating_scales_version_number",
                table: "school_profile",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "rating_scale",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rating_scale", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rating_scale_point",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating_scale_id = table.Column<Guid>(type: "uuid", nullable: false),
                    point_code = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    point_label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    point_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rating_scale_point", x => x.id);
                    table.ForeignKey(
                        name: "fk_rating_scale_point_rating_scale_rating_scale_id",
                        column: x => x.rating_scale_id,
                        principalTable: "rating_scale",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "rating_scale",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000601"), "Nursery development" },
                    { new Guid("00000000-0000-0000-0000-000000000602"), "Primary trait" },
                    { new Guid("00000000-0000-0000-0000-000000000603"), "Five-point numeric" }
                });

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000101"),
                column: "privileges",
                value: "admin.create,admin.deactivate,admin.password.reset,admin.session.revoke,admin.suspend,admin.update,admin.view,arm.capacity.override,arm.create,arm.delete,arm.formteacher.assign,arm.update,arm.view,audit.export,audit.view,contact.create,contact.update,contact.view,level.create,level.deactivate,level.delete,level.update,level.view,pin.generate,pin.print,pin.revoke,pin.usage.view,pin.view,promotion.decide,promotion.reverse,promotion.run,pupil.admission.approve,pupil.create,pupil.delete,pupil.document.manage,pupil.import,pupil.photo.update,pupil.regnumber.correct,pupil.safeguarding.update,pupil.safeguarding.view,pupil.status.update,pupil.transfer,pupil.update,pupil.view,report.export,report.view,result.annual.compute,result.approve,result.attendance.enter,result.compute,result.print,result.publish,result.remark.classteacher,result.remark.headteacher,result.return,result.score.enter,result.score.void,result.submit,result.trait.enter,result.unpublish,result.view,role.assign,role.create,role.delete,role.scope.assign,role.update,role.view,session.create,session.update,session.view,settings.abbreviation.update,settings.assessment.update,settings.grading.update,settings.identity.update,settings.pin.update,settings.ratingscales.update,settings.regnumber.update,settings.reset.defaults,settings.resultrules.update,settings.traits.update,settings.view,subject.create,subject.deactivate,subject.delete,subject.map,subject.map.arm,subject.unmap,subject.update,subject.view,term.close,term.open,weekly.enter,weekly.publish,weekly.view");

            migrationBuilder.UpdateData(
                table: "school_profile",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "rating_scales_version_number",
                value: 0);

            migrationBuilder.InsertData(
                table: "rating_scale_point",
                columns: new[] { "id", "point_code", "point_label", "point_order", "rating_scale_id" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000701"), "N", "Needs Improvement", 1, new Guid("00000000-0000-0000-0000-000000000601") },
                    { new Guid("00000000-0000-0000-0000-000000000702"), "I", "Improving", 2, new Guid("00000000-0000-0000-0000-000000000601") },
                    { new Guid("00000000-0000-0000-0000-000000000703"), "S", "Satisfied", 3, new Guid("00000000-0000-0000-0000-000000000601") },
                    { new Guid("00000000-0000-0000-0000-000000000704"), "E", "Excellent", 4, new Guid("00000000-0000-0000-0000-000000000601") },
                    { new Guid("00000000-0000-0000-0000-000000000705"), "N", "Needs Improvement", 1, new Guid("00000000-0000-0000-0000-000000000602") },
                    { new Guid("00000000-0000-0000-0000-000000000706"), "I", "Improving", 2, new Guid("00000000-0000-0000-0000-000000000602") },
                    { new Guid("00000000-0000-0000-0000-000000000707"), "E", "Excellent", 3, new Guid("00000000-0000-0000-0000-000000000602") },
                    { new Guid("00000000-0000-0000-0000-000000000708"), "1", "Shows no regard for observable traits.", 1, new Guid("00000000-0000-0000-0000-000000000603") },
                    { new Guid("00000000-0000-0000-0000-000000000709"), "2", "Shows minimal regard for observable traits.", 2, new Guid("00000000-0000-0000-0000-000000000603") },
                    { new Guid("00000000-0000-0000-0000-000000000710"), "3", "Shows an acceptable level of observable traits.", 3, new Guid("00000000-0000-0000-0000-000000000603") },
                    { new Guid("00000000-0000-0000-0000-000000000711"), "4", "Maintains a high level of observable traits.", 4, new Guid("00000000-0000-0000-0000-000000000603") },
                    { new Guid("00000000-0000-0000-0000-000000000712"), "5", "Maintains an excellent degree of observable traits.", 5, new Guid("00000000-0000-0000-0000-000000000603") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_rating_scale_point_rating_scale_id",
                table: "rating_scale_point",
                column: "rating_scale_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rating_scale_point");

            migrationBuilder.DropTable(
                name: "rating_scale");

            migrationBuilder.DropColumn(
                name: "rating_scales_version_number",
                table: "school_profile");

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000101"),
                column: "privileges",
                value: "admin.create,admin.deactivate,admin.password.reset,admin.session.revoke,admin.suspend,admin.update,admin.view,arm.capacity.override,arm.create,arm.delete,arm.formteacher.assign,arm.update,arm.view,audit.export,audit.view,contact.create,contact.update,contact.view,level.create,level.deactivate,level.delete,level.update,level.view,pin.generate,pin.print,pin.revoke,pin.usage.view,pin.view,promotion.decide,promotion.reverse,promotion.run,pupil.admission.approve,pupil.create,pupil.delete,pupil.document.manage,pupil.import,pupil.photo.update,pupil.regnumber.correct,pupil.safeguarding.update,pupil.safeguarding.view,pupil.status.update,pupil.transfer,pupil.update,pupil.view,report.export,report.view,result.annual.compute,result.approve,result.attendance.enter,result.compute,result.print,result.publish,result.remark.classteacher,result.remark.headteacher,result.return,result.score.enter,result.score.void,result.submit,result.trait.enter,result.unpublish,result.view,role.assign,role.create,role.delete,role.scope.assign,role.update,role.view,session.create,session.update,session.view,settings.abbreviation.update,settings.assessment.update,settings.grading.update,settings.identity.update,settings.pin.update,settings.regnumber.update,settings.reset.defaults,settings.resultrules.update,settings.traits.update,settings.view,subject.create,subject.deactivate,subject.delete,subject.map,subject.map.arm,subject.unmap,subject.update,subject.view,term.close,term.open,weekly.enter,weekly.publish,weekly.view");
        }
    }
}
