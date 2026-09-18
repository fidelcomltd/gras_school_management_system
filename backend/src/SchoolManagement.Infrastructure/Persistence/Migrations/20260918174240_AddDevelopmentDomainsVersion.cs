using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDevelopmentDomainsVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "development_domains_version_number",
                table: "school_profile",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000101"),
                column: "privileges",
                value: "admin.create,admin.deactivate,admin.password.reset,admin.session.revoke,admin.suspend,admin.update,admin.view,arm.capacity.override,arm.create,arm.delete,arm.formteacher.assign,arm.update,arm.view,audit.export,audit.view,contact.create,contact.update,contact.view,level.create,level.deactivate,level.delete,level.update,level.view,pin.generate,pin.print,pin.revoke,pin.usage.view,pin.view,promotion.decide,promotion.reverse,promotion.run,pupil.admission.approve,pupil.create,pupil.delete,pupil.document.manage,pupil.import,pupil.photo.update,pupil.regnumber.correct,pupil.safeguarding.update,pupil.safeguarding.view,pupil.status.update,pupil.transfer,pupil.update,pupil.view,report.export,report.view,result.annual.compute,result.approve,result.attendance.enter,result.compute,result.print,result.publish,result.remark.classteacher,result.remark.headteacher,result.return,result.score.enter,result.score.void,result.submit,result.trait.enter,result.unpublish,result.view,role.assign,role.create,role.delete,role.scope.assign,role.update,role.view,session.create,session.update,session.view,settings.abbreviation.update,settings.assessment.update,settings.developmentdomains.update,settings.grading.update,settings.identity.update,settings.pin.update,settings.ratingscales.update,settings.regnumber.update,settings.reset.defaults,settings.resultrules.update,settings.traits.update,settings.view,subject.create,subject.deactivate,subject.delete,subject.map,subject.map.arm,subject.unmap,subject.update,subject.view,term.close,term.open,weekly.enter,weekly.publish,weekly.view");

            migrationBuilder.UpdateData(
                table: "school_profile",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "development_domains_version_number",
                value: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "development_domains_version_number",
                table: "school_profile");

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000101"),
                column: "privileges",
                value: "admin.create,admin.deactivate,admin.password.reset,admin.session.revoke,admin.suspend,admin.update,admin.view,arm.capacity.override,arm.create,arm.delete,arm.formteacher.assign,arm.update,arm.view,audit.export,audit.view,contact.create,contact.update,contact.view,level.create,level.deactivate,level.delete,level.update,level.view,pin.generate,pin.print,pin.revoke,pin.usage.view,pin.view,promotion.decide,promotion.reverse,promotion.run,pupil.admission.approve,pupil.create,pupil.delete,pupil.document.manage,pupil.import,pupil.photo.update,pupil.regnumber.correct,pupil.safeguarding.update,pupil.safeguarding.view,pupil.status.update,pupil.transfer,pupil.update,pupil.view,report.export,report.view,result.annual.compute,result.approve,result.attendance.enter,result.compute,result.print,result.publish,result.remark.classteacher,result.remark.headteacher,result.return,result.score.enter,result.score.void,result.submit,result.trait.enter,result.unpublish,result.view,role.assign,role.create,role.delete,role.scope.assign,role.update,role.view,session.create,session.update,session.view,settings.abbreviation.update,settings.assessment.update,settings.grading.update,settings.identity.update,settings.pin.update,settings.ratingscales.update,settings.regnumber.update,settings.reset.defaults,settings.resultrules.update,settings.traits.update,settings.view,subject.create,subject.deactivate,subject.delete,subject.map,subject.map.arm,subject.unmap,subject.update,subject.view,term.close,term.open,weekly.enter,weekly.publish,weekly.view");
        }
    }
}
