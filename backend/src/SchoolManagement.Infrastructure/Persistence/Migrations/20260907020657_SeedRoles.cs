using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "roles",
                columns: new[] { "id", "created_at_utc", "created_by", "description", "is_system", "modified_at_utc", "modified_by", "name", "name_key", "privileges", "status", "version" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000101"), new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Holds every privilege school-wide. Creates other admins, builds roles, edits settings.", true, null, null, "Super Admin", "super admin", "admin.create,admin.deactivate,admin.password.reset,admin.session.revoke,admin.suspend,admin.update,admin.view,arm.capacity.override,arm.create,arm.delete,arm.formteacher.assign,arm.update,arm.view,audit.export,audit.view,contact.create,contact.update,contact.view,level.create,level.deactivate,level.delete,level.update,level.view,pin.generate,pin.print,pin.revoke,pin.usage.view,pin.view,promotion.decide,promotion.reverse,promotion.run,pupil.admission.approve,pupil.create,pupil.delete,pupil.document.manage,pupil.import,pupil.photo.update,pupil.regnumber.correct,pupil.safeguarding.update,pupil.safeguarding.view,pupil.status.update,pupil.transfer,pupil.update,pupil.view,report.export,report.view,result.annual.compute,result.approve,result.attendance.enter,result.compute,result.print,result.publish,result.remark.classteacher,result.remark.headteacher,result.return,result.score.enter,result.score.void,result.submit,result.trait.enter,result.unpublish,result.view,role.assign,role.create,role.delete,role.scope.assign,role.update,role.view,session.create,session.update,session.view,settings.abbreviation.update,settings.assessment.update,settings.grading.update,settings.identity.update,settings.pin.update,settings.regnumber.update,settings.reset.defaults,settings.resultrules.update,settings.traits.update,settings.view,subject.create,subject.deactivate,subject.delete,subject.map,subject.map.arm,subject.unmap,subject.update,subject.view,term.close,term.open,weekly.enter,weekly.publish,weekly.view", "Active", new Guid("00000000-0000-0000-0000-000000000201") },
                    { new Guid("00000000-0000-0000-0000-000000000102"), new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Runs the school's records: sessions, terms, levels, arms, subjects, pupils, guardians, promotion.", false, null, null, "School Administrator", "school administrator", "admin.view,arm.capacity.override,arm.create,arm.formteacher.assign,arm.update,arm.view,contact.create,contact.update,contact.view,level.create,level.deactivate,level.update,level.view,pin.usage.view,pin.view,promotion.run,pupil.admission.approve,pupil.create,pupil.document.manage,pupil.import,pupil.photo.update,pupil.safeguarding.update,pupil.safeguarding.view,pupil.status.update,pupil.transfer,pupil.update,pupil.view,report.export,report.view,result.print,result.view,role.scope.assign,session.create,session.update,session.view,settings.view,subject.create,subject.deactivate,subject.map,subject.map.arm,subject.unmap,subject.update,subject.view", "Active", new Guid("00000000-0000-0000-0000-000000000202") },
                    { new Guid("00000000-0000-0000-0000-000000000103"), new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Reviews computed results for an arm, writes the head teacher's remark, approves or returns, publishes.", false, null, null, "Head Teacher", "head teacher", "arm.view,contact.view,level.view,pin.view,promotion.decide,pupil.admission.approve,pupil.safeguarding.update,pupil.safeguarding.view,pupil.view,report.export,report.view,result.annual.compute,result.approve,result.compute,result.print,result.publish,result.remark.headteacher,result.return,result.view,session.view,settings.view,subject.view,weekly.enter,weekly.publish,weekly.view", "Active", new Guid("00000000-0000-0000-0000-000000000203") },
                    { new Guid("00000000-0000-0000-0000-000000000104"), new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Enters continuous assessment and examination marks, trait ratings, attendance figures and the class teacher's remark, for the arms their assignment is scoped to.", false, null, null, "Class Teacher", "class teacher", "arm.view,contact.view,level.view,pupil.photo.update,pupil.safeguarding.view,pupil.view,result.attendance.enter,result.compute,result.print,result.remark.classteacher,result.score.enter,result.submit,result.trait.enter,result.view,session.view,subject.view,weekly.enter,weekly.publish,weekly.view", "Active", new Guid("00000000-0000-0000-0000-000000000204") },
                    { new Guid("00000000-0000-0000-0000-000000000105"), new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Generates, prints, revokes and reports on access pins. Reads the pupil register. Cannot see or enter marks.", false, null, null, "Bursar", "bursar", "arm.view,contact.view,level.view,pin.generate,pin.print,pin.revoke,pin.usage.view,pin.view,pupil.view,session.view,weekly.view", "Active", new Guid("00000000-0000-0000-0000-000000000205") },
                    { new Guid("00000000-0000-0000-0000-000000000106"), new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Read-only across records, results and the audit log. Holds no write privilege at all.", false, null, null, "Auditor", "auditor", "admin.view,arm.view,audit.export,audit.view,level.view,pin.usage.view,pin.view,pupil.view,report.export,report.view,result.view,role.view,session.view,settings.view,subject.view", "Active", new Guid("00000000-0000-0000-0000-000000000206") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000101"));

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000102"));

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000103"));

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000104"));

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000105"));

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000106"));
        }
    }
}
