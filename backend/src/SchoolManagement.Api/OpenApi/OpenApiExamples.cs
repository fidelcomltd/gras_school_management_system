using System.Text.Json.Nodes;
using SchoolManagement.Api.Endpoints;
using SchoolManagement.Application.Admissions;
using SchoolManagement.Application.Audit;
using SchoolManagement.Application.Auth;
using SchoolManagement.Application.Auth.AdminAccounts;
using SchoolManagement.Application.Auth.ChangePassword;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Classes;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Application.Reference.Ping;
using SchoolManagement.Application.Reference.SampleRecords;
using SchoolManagement.Application.Security.Assignments;
using SchoolManagement.Application.Security.PrivilegeRegister;
using SchoolManagement.Application.Security.Roles;
using SchoolManagement.Application.Sessions;
using SchoolManagement.Application.Settings;
using SchoolManagement.Application.Subjects;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Api.OpenApi;

/// <summary>
/// Worked examples for every contract type, declared once each.
/// </summary>
/// <remarks>
/// <para>
/// WHY EXAMPLES ARE NOT OPTIONAL. A description says what a field means; an example says what a VALUE
/// looks like, and that is what a consumer actually needs for anything with a format:
/// <c>"2026-08-03T09:30:00+00:00"</c> versus <c>"03/08/2026"</c>, an opaque ID versus a bare integer.
/// Without them, every client author guesses, and half of them guess wrong.
/// </para>
/// <para>
/// ONE DECLARATION PER TYPE. Only whole-object examples are written here.
/// <see cref="SchemaExampleTransformer"/> derives each PROPERTY's example by reading the matching field
/// out of its parent's example, so a field's example and the object's example cannot contradict each
/// other — there is nothing to keep in sync.
/// </para>
/// <para>
/// ADDING A CONTRACT TYPE: add an entry below. <c>OpenApiContractTests</c> asserts every schema in the
/// generated document has an example, so a new DTO without one fails the build rather than shipping
/// undocumented.
/// </para>
/// </remarks>
internal static class OpenApiExamples
{
    /// <summary>Canonical timestamp used by every example, and as the fallback for any date-time field.</summary>
    /// <remarks>
    /// A fixed value, never "now": an example that changes on every regeneration would make the
    /// committed contract differ on every build and turn the CI drift check into noise.
    /// </remarks>
    public const string CanonicalTimestamp = "2026-08-03T09:30:00+00:00";

    /// <summary>An example identifier, shaped like the version 7 GUIDs this service generates.</summary>
    private const string ExampleId = "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40";

    /// <summary>Example identifiers for the role-assignment examples, each a distinct entity.</summary>
    private const string ExampleAssignmentId = "0192f0c4-8d4f-7b2c-a03e-4c9f6b7d2e51";
    private const string ExampleAdminAccountId = "0192f0c4-9e50-7c3d-b14f-5d0a7c8e3f62";
    private const string ExampleSessionId = "0192f0c4-af61-7d4e-c250-6e1b8d9f4073";
    private const string ExampleArmId = "0192f0c4-c072-7e5f-d361-7f2c9e0a5184";
    private const string ExampleGrantedById = "0192f0c4-d183-7f60-e472-8030af1b6295";

    /// <summary>Example identifiers for the TASK-0070 subject/mapping examples, each a distinct entity.</summary>
    private const string ExampleSubjectId = "0192f0c4-e294-7061-f583-9141c02d7306";
    private const string ExampleSecondSubjectId = "0192f0c4-f3a5-7162-0694-a252d13e8417";
    private const string ExampleLevelId = "0192f0c4-04b6-7263-1705-b363e24f9528";
    private const string ExampleTermId = "0192f0c4-15c7-7364-2816-c474f3608639";
    private const string ExampleSubjectExceptionId = "0192f0c4-26d8-7465-3927-d585047719a0";

    /// <summary>
    /// Whole-object example JSON, keyed by contract type. Property names are camelCase, matching the
    /// wire format.
    /// </summary>
    public static IReadOnlyDictionary<Type, string> ByType { get; } = new Dictionary<Type, string>
    {
        [typeof(PingResponse)] = $$"""
            {
              "message": "Hello, Ada.",
              "serverTimeUtc": "{{CanonicalTimestamp}}",
              "apiVersion": "1.0"
            }
            """,

        [typeof(CreateSampleRecordCommand)] = """
            {
              "label": "Term 1 timetable draft",
              "note": "Carried over from the previous academic year."
            }
            """,

        [typeof(CreateSampleRecordResponse)] = $$"""
            {
              "id": "{{ExampleId}}"
            }
            """,

        [typeof(SampleRecordDto)] = $$"""
            {
              "id": "{{ExampleId}}",
              "label": "Term 1 timetable draft",
              "note": "Carried over from the previous academic year.",
              "createdAtUtc": "{{CanonicalTimestamp}}",
              "modifiedAtUtc": null
            }
            """,

        // Shows a middle page, so hasNextPage and hasPreviousPage are both meaningful rather than
        // both false as they would be on a single-page example.
        [typeof(PagedResult<SampleRecordDto>)] = $$"""
            {
              "items": [
                {
                  "id": "{{ExampleId}}",
                  "label": "Term 1 timetable draft",
                  "note": "Carried over from the previous academic year.",
                  "createdAtUtc": "{{CanonicalTimestamp}}",
                  "modifiedAtUtc": null
                }
              ],
              "page": 2,
              "pageSize": 20,
              "totalCount": 137,
              "totalPages": 7,
              "hasNextPage": true,
              "hasPreviousPage": true
            }
            """,

        [typeof(SecureArmResponse)] = $$"""
            {
              "armId": "{{ExampleId}}"
            }
            """,

        [typeof(SettingsIdentityGroupDto)] = """
            {
              "schoolName": "Golden Royal Ark School",
              "shortName": "GRAS",
              "address": "12 Ark Crescent, Lekki, Lagos",
              "phone": "+2348012345678",
              "email": "info@goldenroyalark.example",
              "motto": "Excellence Through Character",
              "headTeacherName": "Chisom Maxwell",
              "timezone": "Africa/Lagos",
              "versionNumber": 3
            }
            """,

        [typeof(SettingsDto)] = """
            {
              "identity": {
                "schoolName": "Golden Royal Ark School",
                "shortName": "GRAS",
                "address": "12 Ark Crescent, Lekki, Lagos",
                "phone": "+2348012345678",
                "email": "info@goldenroyalark.example",
                "motto": "Excellence Through Character",
                "headTeacherName": "Chisom Maxwell",
                "timezone": "Africa/Lagos",
                "versionNumber": 3
              },
              "abbreviation": {
                "abbreviation": "GRAS",
                "issuedCount": null,
                "versionNumber": 0
              },
              "regNumber": {
                "separator": "/",
                "serialWidth": 4,
                "serialReset": "PerYear",
                "yearSource": "AdmissionYear",
                "versionNumber": 0
              },
              "grading": {
                "bands": [
                  { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6001", "lowerBound": 90, "upperBound": 100, "gradeLetter": "A+", "remark": "Very excellent", "displayOrder": 1 },
                  { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6002", "lowerBound": 85, "upperBound": 89, "gradeLetter": "A", "remark": "Excellent", "displayOrder": 2 },
                  { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6003", "lowerBound": 75, "upperBound": 84, "gradeLetter": "B", "remark": "Very good", "displayOrder": 3 },
                  { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6004", "lowerBound": 70, "upperBound": 74, "gradeLetter": "B-", "remark": "Good", "displayOrder": 4 },
                  { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6005", "lowerBound": 60, "upperBound": 69, "gradeLetter": "C+", "remark": "Average", "displayOrder": 5 },
                  { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6006", "lowerBound": 50, "upperBound": 59, "gradeLetter": "C", "remark": "Fair", "displayOrder": 6 },
                  { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6007", "lowerBound": 40, "upperBound": 49, "gradeLetter": "D", "remark": "More effort", "displayOrder": 7 },
                  { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6008", "lowerBound": 20, "upperBound": 39, "gradeLetter": "E", "remark": "Not Now", "displayOrder": 8 },
                  { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6009", "lowerBound": 0, "upperBound": 19, "gradeLetter": "F", "remark": "Fail", "displayOrder": 9 }
                ],
                "versionNumber": 0
              },
              "assessment": {
                "components": [
                  { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6101", "name": "1st CA", "shortLabel": "CA1", "maxMark": 20, "isExamination": false, "displayOrder": 1 },
                  { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6102", "name": "2nd CA", "shortLabel": "CA2", "maxMark": 20, "isExamination": false, "displayOrder": 2 },
                  { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6103", "name": "Exam", "shortLabel": "EXAM", "maxMark": 60, "isExamination": true, "displayOrder": 3 }
                ],
                "versionNumber": 0
              }
            }
            """,

        [typeof(GradingBandDto)] = """
            {
              "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6001",
              "lowerBound": 90,
              "upperBound": 100,
              "gradeLetter": "A+",
              "remark": "Very excellent",
              "displayOrder": 1
            }
            """,

        [typeof(SettingsGradingGroupDto)] = """
            {
              "bands": [
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6001", "lowerBound": 90, "upperBound": 100, "gradeLetter": "A+", "remark": "Very excellent", "displayOrder": 1 },
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6002", "lowerBound": 85, "upperBound": 89, "gradeLetter": "A", "remark": "Excellent", "displayOrder": 2 },
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6003", "lowerBound": 75, "upperBound": 84, "gradeLetter": "B", "remark": "Very good", "displayOrder": 3 },
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6004", "lowerBound": 70, "upperBound": 74, "gradeLetter": "B-", "remark": "Good", "displayOrder": 4 },
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6005", "lowerBound": 60, "upperBound": 69, "gradeLetter": "C+", "remark": "Average", "displayOrder": 5 },
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6006", "lowerBound": 50, "upperBound": 59, "gradeLetter": "C", "remark": "Fair", "displayOrder": 6 },
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6007", "lowerBound": 40, "upperBound": 49, "gradeLetter": "D", "remark": "More effort", "displayOrder": 7 },
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6008", "lowerBound": 20, "upperBound": 39, "gradeLetter": "E", "remark": "Not Now", "displayOrder": 8 },
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6009", "lowerBound": 0, "upperBound": 19, "gradeLetter": "F", "remark": "Fail", "displayOrder": 9 }
              ],
              "versionNumber": 0
            }
            """,

        [typeof(GradingBandInput)] = """
            {
              "lowerBound": 90,
              "upperBound": 100,
              "gradeLetter": "A+",
              "remark": "Very excellent"
            }
            """,

        [typeof(UpdateGradingCommand)] = """
            {
              "bands": [
                { "lowerBound": 90, "upperBound": 100, "gradeLetter": "A+", "remark": "Very excellent" },
                { "lowerBound": 85, "upperBound": 89, "gradeLetter": "A", "remark": "Excellent" },
                { "lowerBound": 75, "upperBound": 84, "gradeLetter": "B", "remark": "Very good" },
                { "lowerBound": 70, "upperBound": 74, "gradeLetter": "B-", "remark": "Good" },
                { "lowerBound": 60, "upperBound": 69, "gradeLetter": "C+", "remark": "Average" },
                { "lowerBound": 50, "upperBound": 59, "gradeLetter": "C", "remark": "Fair" },
                { "lowerBound": 40, "upperBound": 49, "gradeLetter": "D", "remark": "More effort" },
                { "lowerBound": 20, "upperBound": 39, "gradeLetter": "E", "remark": "Not Now" },
                { "lowerBound": 0, "upperBound": 19, "gradeLetter": "F", "remark": "Fail" }
              ],
              "expectedVersion": 0,
              "reason": null
            }
            """,

        [typeof(ResetGradingCommand)] = """
            {
              "expectedVersion": 3,
              "reason": null
            }
            """,

        [typeof(AssessmentComponentDto)] = """
            {
              "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6101",
              "name": "1st CA",
              "shortLabel": "CA1",
              "maxMark": 20,
              "isExamination": false,
              "displayOrder": 1
            }
            """,

        [typeof(SettingsAssessmentGroupDto)] = """
            {
              "components": [
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6101", "name": "1st CA", "shortLabel": "CA1", "maxMark": 20, "isExamination": false, "displayOrder": 1 },
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6102", "name": "2nd CA", "shortLabel": "CA2", "maxMark": 20, "isExamination": false, "displayOrder": 2 },
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6103", "name": "Exam", "shortLabel": "EXAM", "maxMark": 60, "isExamination": true, "displayOrder": 3 }
              ],
              "versionNumber": 0
            }
            """,

        [typeof(AssessmentComponentSaveRequest)] = """
            {
              "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6101",
              "name": "1st CA",
              "shortLabel": "CA1",
              "maxMark": 20,
              "isExamination": false
            }
            """,

        [typeof(UpdateAssessmentCommand)] = """
            {
              "components": [
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6101", "name": "1st CA", "shortLabel": "CA1", "maxMark": 20, "isExamination": false },
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6102", "name": "2nd CA", "shortLabel": "CA2", "maxMark": 20, "isExamination": false },
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6103", "name": "Exam", "shortLabel": "EXAM", "maxMark": 60, "isExamination": true }
              ],
              "expectedVersion": 0,
              "reason": null
            }
            """,

        [typeof(SettingsAbbreviationGroupDto)] = """
            {
              "abbreviation": "GRAS",
              "issuedCount": null,
              "versionNumber": 0
            }
            """,

        [typeof(UpdateAbbreviationCommand)] = """
            {
              "abbreviation": "GRA",
              "confirmationToken": "CHANGE",
              "reason": "The school shortened its registered trading name.",
              "expectedVersion": 0
            }
            """,

        [typeof(SettingsRegNumberGroupDto)] = """
            {
              "separator": "/",
              "serialWidth": 4,
              "serialReset": "PerYear",
              "yearSource": "AdmissionYear",
              "versionNumber": 0
            }
            """,

        [typeof(UpdateRegNumberCommand)] = """
            {
              "separator": "/",
              "serialWidth": 4,
              "serialReset": "PerYear",
              "expectedVersion": 0
            }
            """,

        [typeof(RegNumberPreviewDto)] = """
            {
              "preview": "GRAS/2026/0040"
            }
            """,

        [typeof(UpdateSchoolIdentityCommand)] = """
            {
              "schoolName": "Golden Royal Ark School",
              "shortName": "GRAS",
              "address": "12 Ark Crescent, Lekki, Lagos",
              "phone": "08012345678",
              "email": "info@goldenroyalark.example",
              "motto": "Excellence Through Character",
              "headTeacherName": "Chisom Maxwell",
              "expectedVersion": 2
            }
            """,

        [typeof(ConfigVersionSummaryDto)] = $$"""
            {
              "id": "{{ExampleId}}",
              "versionNumber": 3,
              "changedGroup": "Identity",
              "actorAdminId": "0192f0c4-0000-7000-8000-000000000099",
              "createdAtUtc": "{{CanonicalTimestamp}}"
            }
            """,

        [typeof(CursorPage<ConfigVersionSummaryDto>)] = $$"""
            {
              "items": [
                {
                  "id": "{{ExampleId}}",
                  "versionNumber": 3,
                  "changedGroup": "Identity",
                  "actorAdminId": "0192f0c4-0000-7000-8000-000000000099",
                  "createdAtUtc": "{{CanonicalTimestamp}}"
                }
              ],
              "nextCursor": "MQ=="
            }
            """,

        [typeof(ConfigVersionDetailDto)] = $$"""
            {
              "id": "{{ExampleId}}",
              "versionNumber": 3,
              "changedGroup": "Identity",
              "actorAdminId": "0192f0c4-0000-7000-8000-000000000099",
              "reason": null,
              "createdAtUtc": "{{CanonicalTimestamp}}",
              "snapshot": {
                "schoolProfile": {
                  "schoolName": "Golden Royal Ark School",
                  "shortName": "GRAS",
                  "abbreviation": "GRAS",
                  "address": "12 Ark Crescent, Lekki, Lagos",
                  "phone": "+2348012345678",
                  "email": "info@goldenroyalark.example",
                  "motto": "Excellence Through Character",
                  "headTeacherName": "Chisom Maxwell",
                  "timezone": "Africa/Lagos",
                  "identityVersionNumber": 3,
                  "abbreviationVersionNumber": 0
                }
              }
            }
            """,

        [typeof(CsrfTokenResponse)] = """
            {
              "csrfToken": "CfDJ8N-example-opaque-csrf-token-value"
            }
            """,

        [typeof(SignInCommand)] = """
            {
              "email": "admin@example.com",
              "password": "correct horse battery staple 9"
            }
            """,

        [typeof(ChangePasswordCommand)] = """
            {
              "currentPassword": "correct horse battery staple 9",
              "newPassword": "another horse battery staple 4"
            }
            """,

        [typeof(EffectivePrivilegeDto)] = """
            {
              "privilege": "admin.view",
              "scope": "SchoolWide",
              "armIds": []
            }
            """,

        [typeof(AuthSessionResponse)] = $$"""
            {
              "accountId": "{{ExampleId}}",
              "email": "admin@example.com",
              "staffName": "Chisom Maxwell",
              "isSuperAdmin": true,
              "mustChangePassword": false,
              "effectivePrivileges": [
                { "privilege": "admin.view", "scope": "SchoolWide", "armIds": [] }
              ],
              "sessionExpiresAt": "{{CanonicalTimestamp}}",
              "sessionAbsoluteExpiresAt": "{{CanonicalTimestamp}}"
            }
            """,

        [typeof(CreateAdminAccountCommand)] = """
            {
              "staffName": "Ngozi Adeyemi",
              "email": "ngozi.adeyemi@example.com",
              "phone": "08012345678"
            }
            """,

        [typeof(CreateAdminAccountResponse)] = $$"""
            {
              "id": "{{ExampleId}}",
              "staffName": "Ngozi Adeyemi",
              "email": "ngozi.adeyemi@example.com",
              "phone": "+2348012345678",
              "status": "Active",
              "mustChangePassword": true,
              "createdAtUtc": "{{CanonicalTimestamp}}",
              "temporaryPassword": "aB3xQ9mK2pL7vN4wR8dT"
            }
            """,

        [typeof(AdminAccountSummaryDto)] = $$"""
            {
              "id": "{{ExampleId}}",
              "staffName": "Ngozi Adeyemi",
              "email": "ngozi.adeyemi@example.com",
              "phone": "+2348012345678",
              "status": "Active",
              "isSuperAdmin": false,
              "mustChangePassword": false,
              "lastLoginAtUtc": "{{CanonicalTimestamp}}",
              "createdAtUtc": "{{CanonicalTimestamp}}"
            }
            """,

        [typeof(CursorPage<AdminAccountSummaryDto>)] = $$"""
            {
              "items": [
                {
                  "id": "{{ExampleId}}",
                  "staffName": "Ngozi Adeyemi",
                  "email": "ngozi.adeyemi@example.com",
                  "phone": "+2348012345678",
                  "status": "Active",
                  "isSuperAdmin": false,
                  "mustChangePassword": false,
                  "lastLoginAtUtc": "{{CanonicalTimestamp}}",
                  "createdAtUtc": "{{CanonicalTimestamp}}"
                }
              ],
              "nextCursor": "MHxuZ296aSBhZGV5ZW1pfDAxOTJmMGM0LTdjM2UtN2ExYi05ZjJkLTNiOGU1YTZjMWQ0MA=="
            }
            """,

        [typeof(AdminAccountDetailDto)] = $$"""
            {
              "id": "{{ExampleId}}",
              "staffName": "Ngozi Adeyemi",
              "email": "ngozi.adeyemi@example.com",
              "phone": "+2348012345678",
              "status": "Active",
              "isSuperAdmin": false,
              "mustChangePassword": false,
              "lastLoginAtUtc": "{{CanonicalTimestamp}}",
              "createdAtUtc": "{{CanonicalTimestamp}}"
            }
            """,

        [typeof(UpdateAdminAccountCommand)] = $$"""
            {
              "id": "{{ExampleId}}",
              "staffName": "Ngozi Adeyemi-Bello",
              "email": "ngozi.adeyemi@example.com",
              "phone": "08012345678",
              "isSuperAdmin": null
            }
            """,

        [typeof(ChangeAdminAccountStatusCommand)] = $$"""
            {
              "id": "{{ExampleId}}",
              "status": "Suspended",
              "reason": null
            }
            """,

        [typeof(ResetAdminAccountPasswordResponse)] = $$"""
            {
              "id": "{{ExampleId}}",
              "temporaryPassword": "aB3xQ9mK2pL7vN4wR8dT"
            }
            """,

        [typeof(PrivilegeDescriptorDto)] = """
            {
              "code": "result.score.enter",
              "permits": "Enter and edit continuous assessment and examination marks while the result set is Draft or Returned for Correction.",
              "scopable": true
            }
            """,

        [typeof(PrivilegeGroupDto)] = """
            {
              "key": "results",
              "title": "Results",
              "privileges": [
                {
                  "code": "result.score.enter",
                  "permits": "Enter and edit continuous assessment and examination marks while the result set is Draft or Returned for Correction.",
                  "scopable": true
                }
              ]
            }
            """,

        [typeof(PrivilegeRegisterResponse)] = """
            {
              "groups": [
                {
                  "key": "results",
                  "title": "Results",
                  "privileges": [
                    {
                      "code": "result.score.enter",
                      "permits": "Enter and edit continuous assessment and examination marks while the result set is Draft or Returned for Correction.",
                      "scopable": true
                    }
                  ]
                }
              ]
            }
            """,

        [typeof(CreateRoleCommand)] = """
            {
              "name": "Class Teacher",
              "description": "Enters marks and views pupil records for an assigned arm.",
              "privileges": ["result.score.enter", "pupil.view"]
            }
            """,

        [typeof(RoleDto)] = $$"""
            {
              "id": "{{ExampleId}}",
              "name": "Class Teacher",
              "description": "Enters marks and views pupil records for an assigned arm.",
              "isSystem": false,
              "privileges": ["pupil.view", "result.score.enter"],
              "status": "Active"
            }
            """,

        [typeof(CursorPage<RoleDto>)] = $$"""
            {
              "items": [
                {
                  "id": "{{ExampleId}}",
                  "name": "Class Teacher",
                  "description": "Enters marks and views pupil records for an assigned arm.",
                  "isSystem": false,
                  "privileges": ["pupil.view", "result.score.enter"],
                  "status": "Active"
                }
              ],
              "nextCursor": "Y2xhc3MgdGVhY2hlch8wMTkyZjBjNC03YzNlLTdhMWItOWYyZC0zYjhlNWE2YzFkNDA="
            }
            """,

        [typeof(UpdateRoleCommand)] = $$"""
            {
              "id": "{{ExampleId}}",
              "name": "Senior Class Teacher",
              "description": null,
              "privileges": null,
              "status": null
            }
            """,

        [typeof(CreateRoleAssignmentCommand)] = $$"""
            {
              "adminAccountId": "{{ExampleAdminAccountId}}",
              "roleId": "{{ExampleId}}",
              "sessionId": "{{ExampleSessionId}}",
              "scopeType": "ArmList",
              "armIds": ["{{ExampleArmId}}"]
            }
            """,

        [typeof(RoleAssignmentDto)] = $$"""
            {
              "id": "{{ExampleAssignmentId}}",
              "adminAccountId": "{{ExampleAdminAccountId}}",
              "roleId": "{{ExampleId}}",
              "sessionId": "{{ExampleSessionId}}",
              "scopeType": "ArmList",
              "armIds": ["{{ExampleArmId}}"],
              "grantedBy": "{{ExampleGrantedById}}",
              "status": "Active",
              "createdAtUtc": "{{CanonicalTimestamp}}"
            }
            """,

        // The error contract matters more to a client author than any success shape: it is what they
        // have to handle and cannot easily provoke on demand. Both framework types are given examples
        // showing the extension members this API adds — errorCode and traceId — which a consumer would
        // otherwise not know exist, because they are additionalProperties in the schema.
        [typeof(Microsoft.AspNetCore.Mvc.ProblemDetails)] = """
            {
              "type": "urn:schoolmanagement:error:sample_record.label_taken",
              "title": "Conflict with current state",
              "status": 409,
              "detail": "A record with that label already exists.",
              "instance": "/api/v1/reference/records",
              "errorCode": "sample_record.label_taken",
              "traceId": "0af7651916cd43dd8448eb211c80319c"
            }
            """,

        [typeof(Microsoft.AspNetCore.Http.HttpValidationProblemDetails)] = """
            {
              "type": "urn:schoolmanagement:error:request.validation_failed",
              "title": "Validation failed",
              "status": 422,
              "detail": "One or more validation errors occurred.",
              "instance": "/api/v1/reference/records",
              "errorCode": "request.validation_failed",
              "traceId": "0af7651916cd43dd8448eb211c80319c",
              "errors": {
                "Label": [
                  "Label is required."
                ],
                "PageSize": [
                  "PageSize must be at most 100."
                ]
              }
            }
            """,

        [typeof(CreateSessionTermInput)] = """
            {
              "startDate": "2026-09-14",
              "endDate": "2026-12-18",
              "nextResumptionDate": "2027-01-05"
            }
            """,

        [typeof(CreateSessionCommand)] = """
            {
              "name": "2026/2027",
              "startDate": "2026-09-14",
              "endDate": "2027-07-25",
              "term1": { "startDate": "2026-09-14", "endDate": "2026-12-18", "nextResumptionDate": "2027-01-05" },
              "term2": { "startDate": "2027-01-05", "endDate": "2027-04-02", "nextResumptionDate": "2027-04-20" },
              "term3": { "startDate": "2027-04-20", "endDate": "2027-07-25", "nextResumptionDate": null }
            }
            """,

        [typeof(TermDto)] = $$"""
            {
              "id": "{{ExampleId}}",
              "sessionId": "{{ExampleId}}",
              "ordinal": 1,
              "name": "First Term",
              "startDate": "2026-09-14",
              "endDate": "2026-12-18",
              "timesSchoolOpened": null,
              "nextResumptionDate": "2027-01-05",
              "state": "Upcoming",
              "closedAtUtc": null,
              "closedBy": null
            }
            """,

        [typeof(SessionDto)] = """
            {
              "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
              "name": "2026/2027",
              "startDate": "2026-09-14",
              "endDate": "2027-07-25",
              "state": "Upcoming",
              "armCount": 0
            }
            """,

        [typeof(SessionDetailDto)] = $$"""
            {
              "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
              "name": "2026/2027",
              "startDate": "2026-09-14",
              "endDate": "2027-07-25",
              "state": "Active",
              "armCount": 24,
              "terms": [
                {
                  "id": "{{ExampleId}}",
                  "sessionId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
                  "ordinal": 1,
                  "name": "First Term",
                  "startDate": "2026-09-14",
                  "endDate": "2026-12-18",
                  "timesSchoolOpened": 62,
                  "nextResumptionDate": "2027-01-05",
                  "state": "Closed",
                  "closedAtUtc": "{{CanonicalTimestamp}}",
                  "closedBy": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d42"
                },
                {
                  "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d43",
                  "sessionId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
                  "ordinal": 2,
                  "name": "Second Term",
                  "startDate": "2027-01-05",
                  "endDate": "2027-04-02",
                  "timesSchoolOpened": null,
                  "nextResumptionDate": "2027-04-20",
                  "state": "Active",
                  "closedAtUtc": null,
                  "closedBy": null
                },
                {
                  "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d44",
                  "sessionId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
                  "ordinal": 3,
                  "name": "Third Term",
                  "startDate": "2027-04-20",
                  "endDate": "2027-07-25",
                  "timesSchoolOpened": null,
                  "nextResumptionDate": null,
                  "state": "Upcoming",
                  "closedAtUtc": null,
                  "closedBy": null
                }
              ]
            }
            """,

        [typeof(CursorPage<SessionDto>)] = """
            {
              "items": [
                {
                  "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
                  "name": "2026/2027",
                  "startDate": "2026-09-14",
                  "endDate": "2027-07-25",
                  "state": "Active",
                  "armCount": 24
                }
              ],
              "nextCursor": "MjAyNi8yMDI3"
            }
            """,

        [typeof(UpdateSessionCommand)] = $$"""
            {
              "id": "{{ExampleId}}",
              "name": "2026/2027",
              "startDate": null,
              "endDate": null
            }
            """,

        [typeof(UpdateTermCommand)] = $$"""
            {
              "id": "{{ExampleId}}",
              "name": null,
              "startDate": null,
              "endDate": null,
              "timesSchoolOpened": 118,
              "nextResumptionDate": "2027-01-05"
            }
            """,

        [typeof(ReopenTermCommand)] = $$"""
            {
              "id": "{{ExampleId}}",
              "reason": "A mark was entered against the wrong subject and discovered after publication."
            }
            """,

        [typeof(CreateSectionCommand)] = """
            {
              "name": "Secondary"
            }
            """,

        [typeof(UpdateSectionCommand)] = $$"""
            {
              "id": "{{ExampleId}}",
              "name": "Secondary"
            }
            """,

        [typeof(SectionDto)] = $$"""
            {
              "id": "{{ExampleId}}",
              "name": "Primary"
            }
            """,

        [typeof(SectionListResponse)] = $$"""
            {
              "sections": [
                { "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d45", "name": "Nursery" },
                { "id": "{{ExampleId}}", "name": "Primary" }
              ]
            }
            """,

        [typeof(CreateLevelCommand)] = $$"""
            {
              "name": "Reception",
              "sectionId": "{{ExampleId}}",
              "progressionOrder": null,
              "nextLevelId": null,
              "insertAfterLevelId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d46"
            }
            """,

        [typeof(LevelDto)] = $$"""
            {
              "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d46",
              "name": "Primary 1",
              "sectionId": "{{ExampleId}}",
              "section": "Primary",
              "progressionOrder": 4,
              "nextLevelId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d47",
              "isEntryLevel": false,
              "isGraduatingLevel": false,
              "status": "Active"
            }
            """,

        [typeof(CursorPage<LevelDto>)] = $$"""
            {
              "items": [
                {
                  "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d46",
                  "name": "Primary 1",
                  "sectionId": "{{ExampleId}}",
                  "section": "Primary",
                  "progressionOrder": 4,
                  "nextLevelId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d47",
                  "isEntryLevel": false,
                  "isGraduatingLevel": false,
                  "status": "Active"
                }
              ],
              "nextCursor": "BB8xMDE5MmYwYzQtN2MzZS03YTFiLTlmMmQtM2I4ZTVhNmMxZDQ2"
            }
            """,

        [typeof(UpdateLevelCommand)] = $$"""
            {
              "id": "{{ExampleId}}",
              "name": null,
              "sectionId": null,
              "nextLevelId": null,
              "progressionOrder": null,
              "status": null
            }
            """,

        [typeof(ReorderLevelsCommand)] = $$"""
            {
              "orderedLevelIds": [
                "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d46",
                "{{ExampleId}}"
              ]
            }
            """,

        [typeof(CreateArmCommand)] = $$"""
            {
              "classLevelId": "{{ExampleId}}",
              "sessionId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
              "label": "C",
              "capacity": 22,
              "formTeacherAdminId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d42"
            }
            """,

        [typeof(ArmDto)] = $$"""
            {
              "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d48",
              "classLevelId": "{{ExampleId}}",
              "classLevel": "Primary 2",
              "sessionId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
              "label": "C",
              "displayName": "Primary 2C",
              "capacity": 22,
              "formTeacherAdminId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d42",
              "status": "Active"
            }
            """,

        [typeof(CursorPage<ArmDto>)] = $$"""
            {
              "items": [
                {
                  "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d48",
                  "classLevelId": "{{ExampleId}}",
                  "classLevel": "Primary 2",
                  "sessionId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
                  "label": "C",
                  "displayName": "Primary 2C",
                  "capacity": 22,
                  "formTeacherAdminId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d42",
                  "status": "Active"
                }
              ],
              "nextCursor": null
            }
            """,

        [typeof(NextArmLabelResponse)] = """
            {
              "label": "C"
            }
            """,

        [typeof(UpdateArmCommand)] = $$"""
            {
              "id": "{{ExampleId}}",
              "label": null,
              "capacity": 25,
              "formTeacherAdminId": null,
              "status": null
            }
            """,

        [typeof(BulkCreateArmsLevelEntry)] = $$"""
            {
              "levelId": "{{ExampleId}}",
              "armCount": 3,
              "capacity": 30
            }
            """,

        [typeof(BulkCreateArmsCommand)] = $$"""
            {
              "sessionId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
              "levels": [
                { "levelId": "{{ExampleId}}", "armCount": 3, "capacity": 30 }
              ],
              "dryRun": false
            }
            """,

        [typeof(BulkCreateArmsResponse)] = $$"""
            {
              "created": [
                {
                  "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d48",
                  "classLevelId": "{{ExampleId}}",
                  "classLevel": "Primary 2",
                  "sessionId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
                  "label": "A",
                  "displayName": "Primary 2A",
                  "capacity": 30,
                  "formTeacherAdminId": null,
                  "status": "Active"
                }
              ]
            }
            """,

        [typeof(SubjectDto)] = $$"""
            {
              "id": "{{ExampleSubjectId}}",
              "name": "Mathematics",
              "code": null,
              "description": null,
              "status": "Active",
              "mappedLevelCount": 6,
              "armExceptionCount": 1,
              "pupilsTakingCount": 182
            }
            """,

        [typeof(CursorPage<SubjectDto>)] = $$"""
            {
              "items": [
                {
                  "id": "{{ExampleSubjectId}}",
                  "name": "Mathematics",
                  "code": null,
                  "description": null,
                  "status": "Active",
                  "mappedLevelCount": 6,
                  "armExceptionCount": 1,
                  "pupilsTakingCount": 182
                }
              ],
              "nextCursor": null
            }
            """,

        [typeof(CreateSubjectCommand)] = """
            {
              "name": "Mathematics",
              "code": null,
              "description": null
            }
            """,

        [typeof(UpdateSubjectCommand)] = $$"""
            {
              "id": "{{ExampleSubjectId}}",
              "name": null,
              "code": null,
              "description": null,
              "status": null
            }
            """,

        [typeof(SubjectMappingGridLevelDto)] = $$"""
            {
              "classLevelId": "{{ExampleLevelId}}",
              "classLevelName": "Primary 2",
              "progressionOrder": 5
            }
            """,

        [typeof(SubjectMappingGridCellDto)] = $$"""
            {
              "classLevelId": "{{ExampleLevelId}}",
              "mapped": true,
              "displayOrder": 1
            }
            """,

        [typeof(SubjectMappingGridSubjectRowDto)] = $$"""
            {
              "subjectId": "{{ExampleSubjectId}}",
              "subjectName": "Mathematics",
              "subjectCode": null,
              "cells": [
                { "classLevelId": "{{ExampleLevelId}}", "mapped": true, "displayOrder": 1 }
              ]
            }
            """,

        [typeof(SubjectMappingGridArmExceptionSummaryDto)] = $$"""
            {
              "armId": "{{ExampleArmId}}",
              "armDisplayName": "Primary 2C",
              "includeCount": 1,
              "excludeCount": 0
            }
            """,

        [typeof(SubjectMappingGridDto)] = $$"""
            {
              "levels": [
                { "classLevelId": "{{ExampleLevelId}}", "classLevelName": "Primary 2", "progressionOrder": 5 }
              ],
              "subjects": [
                {
                  "subjectId": "{{ExampleSubjectId}}",
                  "subjectName": "Mathematics",
                  "subjectCode": null,
                  "cells": [
                    { "classLevelId": "{{ExampleLevelId}}", "mapped": true, "displayOrder": 1 }
                  ]
                }
              ],
              "armExceptions": [
                { "armId": "{{ExampleArmId}}", "armDisplayName": "Primary 2C", "includeCount": 1, "excludeCount": 0 }
              ]
            }
            """,

        [typeof(SubjectMappingGridEntryInput)] = $$"""
            {
              "subjectId": "{{ExampleSubjectId}}",
              "classLevelId": "{{ExampleLevelId}}",
              "displayOrder": 1
            }
            """,

        [typeof(SaveSubjectMappingGridCommand)] = $$"""
            {
              "termId": "{{ExampleTermId}}",
              "entries": [
                { "subjectId": "{{ExampleSubjectId}}", "classLevelId": "{{ExampleLevelId}}", "displayOrder": 1 }
              ],
              "dryRun": false
            }
            """,

        [typeof(SubjectMappingChangeDto)] = $$"""
            {
              "subjectId": "{{ExampleSubjectId}}",
              "subjectName": "Mathematics",
              "classLevelId": "{{ExampleLevelId}}",
              "classLevelName": "Primary 2"
            }
            """,

        [typeof(SaveSubjectMappingGridResponse)] = $$"""
            {
              "additions": [
                { "subjectId": "{{ExampleSubjectId}}", "subjectName": "Mathematics", "classLevelId": "{{ExampleLevelId}}", "classLevelName": "Primary 2" }
              ],
              "endings": [],
              "dryRun": false
            }
            """,

        [typeof(CopySubjectMappingsCommand)] = $$"""
            {
              "sourceTermId": "{{ExampleTermId}}",
              "destinationTermId": "0192f0c4-37e9-7566-4a38-e696158802b1",
              "dryRun": true
            }
            """,

        [typeof(PrefillSubjectMappingsCommand)] = $$"""
            {
              "termId": "{{ExampleTermId}}",
              "dryRun": true
            }
            """,

        [typeof(ArmSubjectDto)] = $$"""
            {
              "subjectId": "{{ExampleSubjectId}}",
              "subjectName": "Mathematics",
              "subjectCode": null,
              "displayOrder": 1,
              "source": "LevelInherited"
            }
            """,

        [typeof(CreateSubjectExceptionCommand)] = $$"""
            {
              "armId": "{{ExampleArmId}}",
              "subjectId": "{{ExampleSecondSubjectId}}",
              "termId": "{{ExampleTermId}}",
              "mode": "Include",
              "reason": "This arm runs a French club this term."
            }
            """,

        [typeof(SubjectExceptionDto)] = $$"""
            {
              "id": "{{ExampleSubjectExceptionId}}",
              "armId": "{{ExampleArmId}}",
              "subjectId": "{{ExampleSecondSubjectId}}",
              "subjectName": "French",
              "termId": "{{ExampleTermId}}",
              "mode": "Include",
              "reason": "This arm runs a French club this term."
            }
            """,

        [typeof(CreatePupilCommand)] = """
            {
              "surname": "Okafor",
              "firstName": "Chidera",
              "middleName": "Ngozi",
              "sex": "Female",
              "dateOfBirth": "2020-05-03",
              "nationality": "Nigerian",
              "stateOfOrigin": "Anambra",
              "lga": "Awka South",
              "homeAddress": "14 Zik Avenue, Awka",
              "previousSchool": null,
              "previousClass": null,
              "otherInformation": null,
              "admission": {
                "sessionId": null,
                "dateApplicationReceived": "2026-08-01",
                "dateAdmitted": null,
                "classAdmittedInto": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d30",
                "admissionType": "New",
                "admissionTypeNote": null,
                "assessmentRequired": false
              }
            }
            """,

        [typeof(AdmissionRecordDto)] = """
            {
              "sessionId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
              "dateApplicationReceived": "2026-08-01",
              "dateAdmitted": "2026-09-08",
              "classAdmittedInto": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d30",
              "classAdmittedIntoName": "Primary 2",
              "admissionType": "New",
              "admissionTypeNote": null,
              "assessmentRequired": false,
              "assessmentResultRemarks": null,
              "assignedClassTeacher": null,
              "declarationName": "Chinwe Okafor",
              "declarationSigned": true,
              "declarationDate": "2026-09-08",
              "approvedBy": null,
              "approvedAt": null,
              "headOfSchoolConfirmed": false,
              "headOfSchoolName": null
            }
            """,

        [typeof(UpdateAdmissionRecordCommand)] = """
            {
              "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d50",
              "sessionId": null,
              "dateApplicationReceived": null,
              "dateAdmitted": null,
              "classAdmittedInto": null,
              "admissionType": null,
              "admissionTypeNote": null,
              "assessmentRequired": null,
              "assessmentResultRemarks": null,
              "assignedClassTeacher": null,
              "declarationName": "Chinwe Okafor",
              "declarationSigned": true,
              "declarationDate": "2026-09-08",
              "headOfSchoolConfirmed": null,
              "headOfSchoolName": null
            }
            """,

        [typeof(ApproveAdmissionCommand)] = """
            {
              "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d50",
              "armId": "0192f0c4-9e50-7c3d-b14f-5d0a7c8e3f70",
              "assessmentResultRemarks": "Passed the entrance assessment.",
              "headOfSchoolConfirmed": true,
              "headOfSchoolName": null
            }
            """,

        [typeof(DeclineAdmissionCommand)] = """
            {
              "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d50",
              "reason": "Family relocated before the intake began."
            }
            """,

        [typeof(PupilDto)] = """
            {
              "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d50",
              "registrationNumber": null,
              "surname": "Okafor",
              "firstName": "Chidera",
              "middleName": "Ngozi",
              "sex": "Female",
              "dateOfBirth": "2020-05-03",
              "ageYears": 6,
              "nationality": "Nigerian",
              "stateOfOrigin": "Anambra",
              "lga": "Awka South",
              "homeAddress": "14 Zik Avenue, Awka",
              "previousSchool": null,
              "previousClass": null,
              "status": "Pending",
              "otherInformation": null,
              "matchedField": null,
              "createdAtUtc": "2026-08-03T09:30:00+00:00",
              "createdBy": "0192f0c4-9e50-7c3d-b14f-5d0a7c8e3f62",
              "admission": {
                "sessionId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41",
                "dateApplicationReceived": "2026-08-01",
                "dateAdmitted": "2026-09-08",
                "classAdmittedInto": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d30",
                "classAdmittedIntoName": "Primary 2",
                "admissionType": "New",
                "admissionTypeNote": null,
                "assessmentRequired": false,
                "assessmentResultRemarks": null,
                "assignedClassTeacher": null,
                "declarationName": "Chinwe Okafor",
                "declarationSigned": true,
                "declarationDate": "2026-09-08",
                "approvedBy": null,
                "approvedAt": null,
                "headOfSchoolConfirmed": false,
                "headOfSchoolName": null
              },
              "levelAppliedFor": "Primary 2",
              "dateApplicationReceived": "2026-08-01",
              "missing": [
                "Declaration (Section I)"
              ]
            }
            """,

        [typeof(CursorPage<PupilDto>)] = """
            {
              "items": [
                {
                  "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d50",
                  "registrationNumber": null,
                  "surname": "Okafor",
                  "firstName": "Chidera",
                  "middleName": "Ngozi",
                  "sex": "Female",
                  "dateOfBirth": "2020-05-03",
                  "ageYears": 6,
                  "nationality": "Nigerian",
                  "stateOfOrigin": "Anambra",
                  "lga": "Awka South",
                  "homeAddress": "14 Zik Avenue, Awka",
                  "previousSchool": null,
                  "previousClass": null,
                  "status": "Pending",
                  "otherInformation": null,
                  "matchedField": null,
                  "createdAtUtc": "2026-08-03T09:30:00+00:00",
                  "createdBy": "0192f0c4-9e50-7c3d-b14f-5d0a7c8e3f62",
                  "admission": null,
                  "levelAppliedFor": "Primary 2",
                  "dateApplicationReceived": "2026-08-01",
                  "missing": [
                    "Declaration (Section I)"
                  ]
                }
              ],
              "nextCursor": null
            }
            """,

        [typeof(UpdatePupilBiographicalCommand)] = """
            {
              "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d50",
              "surname": "Okafor",
              "firstName": "Chidera",
              "middleName": null,
              "sex": null,
              "dateOfBirth": null,
              "nationality": null,
              "stateOfOrigin": null,
              "lga": null,
              "homeAddress": "22 Zik Avenue, Awka",
              "previousSchool": null,
              "previousClass": null,
              "otherInformation": null,
              "registrationNumber": null
            }
            """,

        [typeof(CorrectRegistrationNumberCommand)] = """
            {
              "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d50",
              "registrationNumber": "GRAS/2026/0041",
              "reason": "Wrong admission year was entered at approval; corrected to 2026."
            }
            """,

        [typeof(AuditEventDto)] = $$"""
            {
              "id": "48213",
              "occurredAtUtc": "{{CanonicalTimestamp}}",
              "actorAdminId": "0192f0c4-9e50-7c3d-b14f-5d0a7c8e3f62",
              "actorLabel": "Chisom Maxwell <chisom.maxwell@example.com>",
              "action": "settings.grading.update",
              "entityType": "grading_band",
              "entityId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
              "outcome": "Success",
              "beforeJson": null,
              "afterJson": null,
              "reason": null,
              "sourceIp": "197.210.64.0/24",
              "userAgent": "Mozilla/5.0"
            }
            """,

        [typeof(CursorPage<AuditEventDto>)] = $$"""
            {
              "items": [
                {
                  "id": "48213",
                  "occurredAtUtc": "{{CanonicalTimestamp}}",
                  "actorAdminId": "0192f0c4-9e50-7c3d-b14f-5d0a7c8e3f62",
                  "actorLabel": "Chisom Maxwell <chisom.maxwell@example.com>",
                  "action": "settings.grading.update",
                  "entityType": "grading_band",
                  "entityId": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40",
                  "outcome": "Success",
                  "beforeJson": null,
                  "afterJson": null,
                  "reason": null,
                  "sourceIp": "197.210.64.0/24",
                  "userAgent": "Mozilla/5.0"
                }
              ],
              "nextCursor": null
            }
            """,
    };

    /// <summary>
    /// Descriptions for contract types that cannot carry an XML doc comment.
    /// </summary>
    /// <remarks>
    /// Almost every schema's description comes from its <c>///</c> comment automatically. These two are
    /// FRAMEWORK types — we do not own the source, so there is nowhere to put a comment, and they would
    /// otherwise be the only undescribed schemas in the document. They also happen to be the most
    /// important ones for a client author: the error shape is what they must handle and cannot easily
    /// provoke on demand.
    /// </remarks>
    public static IReadOnlyDictionary<Type, string> DescriptionsByType { get; } = new Dictionary<Type, string>
    {
        [typeof(Microsoft.AspNetCore.Mvc.ProblemDetails)] =
            "An RFC 9457 problem response. Returned for every error. Branch on the `errorCode` " +
            "extension member — it is stable — and never on `detail`, which is human-readable prose that " +
            "may be reworded. `traceId` identifies this specific occurrence in the server logs; quote it " +
            "when reporting a problem. `type` is a stable URN of the form " +
            "`urn:schoolmanagement:error:<code>`.",

        [typeof(Microsoft.AspNetCore.Http.HttpValidationProblemDetails)] =
            "An RFC 9457 problem response for a validation failure, returned with status 422. Extends " +
            "the standard problem shape with `errors`: an object keyed by request property name, whose " +
            "values are the messages for that property, suitable for attaching to form fields. A 400 " +
            "(rather than 422) means the request itself could not be parsed.",

        // TASK-0005a: config_versions/{id}'s snapshot is deliberately free-form (spec 6.2.9 — "the
        // whole serialised configuration," a shape every future settings card adds a new section to),
        // so it is typed as System.Text.Json.JsonElement rather than a fixed DTO. A framework type
        // again — see this dictionary's own remarks above.
        [typeof(System.Text.Json.JsonElement)] =
            "The whole serialised configuration as of this version (spec 6.2.9) — free-form JSON, " +
            "because every settings card adds its own section to the same snapshot shape. Read it as " +
            "an opaque object; do not assume today's set of keys is complete.",
    };

    /// <summary>
    /// Parses the example for <paramref name="type"/>, or returns <c>null</c> if none is registered.
    /// </summary>
    /// <param name="type">The contract type.</param>
    /// <returns>A fresh node each call, so callers may attach it to a schema.</returns>
    public static JsonNode? TryGetExample(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return ByType.TryGetValue(type, out var json) ? JsonNode.Parse(json) : null;
    }

    /// <summary>
    /// Returns the registered description for <paramref name="type"/>, or <c>null</c>.
    /// </summary>
    /// <param name="type">The contract type.</param>
    public static string? TryGetDescription(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return DescriptionsByType.GetValueOrDefault(type);
    }
}
