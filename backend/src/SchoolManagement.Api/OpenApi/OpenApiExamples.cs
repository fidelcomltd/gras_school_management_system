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
using SchoolManagement.Application.Pins;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Application.Pupils.Records;
using SchoolManagement.Application.Reference.Ping;
using SchoolManagement.Application.Reference.SampleRecords;
using SchoolManagement.Application.Results;
using SchoolManagement.Application.Results.Annual;
using SchoolManagement.Application.Security.Assignments;
using SchoolManagement.Application.Security.PrivilegeRegister;
using SchoolManagement.Application.Security.Roles;
using SchoolManagement.Application.Sessions;
using SchoolManagement.Application.Settings;
using SchoolManagement.Application.Subjects;
using SchoolManagement.Application.Weekly;
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

    /// <summary>Example identifiers for the TASK-0076 score-sheet examples, each a distinct entity.</summary>
    private const string ExampleResultSetId = "0192f0c4-37e9-7566-4a38-e6960588b1b0";
    private const string ExamplePupilId = "0192f0c4-48fa-7667-5b49-f7a71699c2c1";
    private const string ExampleSecondPupilId = "0192f0c4-590b-7768-6c5a-08b827aad3d2";
    private const string ExampleComponentId = "0192f0c4-6a1c-7869-7d6b-19c938bbe4e3";
    private const string ExampleExaminationComponentId = "0192f0c4-7b2d-796a-8e7c-2ada49ccf5f4";

    /// <summary>Example identifiers for the TASK-0088 stage B readiness examples, each a distinct entity.</summary>
    private const string ExampleThirdPupilId = "0192f0c4-8c3e-7a6b-9f8d-3bebafd60605";

    /// <summary>Spec 6.5.5: a father contact, reused by the contact examples.</summary>
    private const string PupilFatherExample = """
        {
          "id": "0192f0c4-8c3e-7b6b-9f8d-3beaafd60706",
          "role": "Father",
          "fullName": "Emeka Okafor",
          "relationship": null,
          "phone": "+2348031234567",
          "whatsappNumber": "+2348031234567",
          "occupation": "Engineer",
          "email": null,
          "isPrimaryContact": true
        }
        """;

    /// <summary>Spec 6.10: one weekly day panel, reused inside every weekly example that carries days.</summary>
    private const string WeeklyMondayExample = $$"""
        {
          "dayOfWeek": "Monday",
          "date": "2027-01-11",
          "behaviour": "Calm and helpful",
          "performance": "Finished her reading book",
          "dressing": null,
          "homeWork": "Returned, neat",
          "eating": "Ate everything",
          "symptomsOfIllness": null,
          "teacherComment": "A lovely start to the week.",
          "parentComment": null,
          "lastEditedAt": "{{CanonicalTimestamp}}",
          "lastEditedById": "{{ExampleAdminAccountId}}",
          "lastEditedBy": "Mrs Adaeze Okonkwo"
        }
        """;

    /// <summary>Spec 6.10.7 phrase memory.</summary>
    private const string WeeklyPhrasesExample = """
        {
          "behaviour": ["Calm and helpful", "Settled well"],
          "performance": ["Finished her reading book"],
          "dressing": [],
          "homeWork": ["Returned, neat"],
          "eating": ["Ate everything", "Ate half"],
          "symptomsOfIllness": [],
          "teacherComment": ["A lovely start to the week."],
          "parentComment": []
        }
        """;

    /// <summary>Spec 6.10.12 completion row.</summary>
    private const string WeeklyCompletionRowExample = $$"""
        {
          "armId": "{{ExampleArmId}}",
          "armName": "Primary 2 Gold",
          "weekNumber": 4,
          "startDate": "2027-01-11",
          "endDate": "2027-01-15",
          "pupilsOnRoll": 28,
          "pupilsWithNotes": 27,
          "cellsFilled": 612,
          "cellsAvailable": 1120,
          "published": true,
          "lastEditedAt": "{{CanonicalTimestamp}}",
          "lastEditedBy": "Mrs Adaeze Okonkwo"
        }
        """;

    /// <summary>Spec 6.10.12 illness summary row.</summary>
    private const string WeeklyIllnessRowExample = $$"""
        {
          "pupilId": "{{ExamplePupilId}}",
          "registrationNumber": "GRAS/2026/0041",
          "displayName": "Okafor Chidera Ngozi",
          "armName": "Primary 2 Gold",
          "observations": [
            { "date": "2027-01-12", "text": "Runny nose, sent home at noon" },
            { "date": "2027-01-13", "text": "Still coughing" }
          ]
        }
        """;

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

        [typeof(SettingsIdentityGroupDto)] = $$"""
            {
              "schoolName": "Golden Royal Ark School",
              "shortName": "GRAS",
              "address": "12 Ark Crescent, Lekki, Lagos",
              "phone": "+2348012345678",
              "email": "info@goldenroyalark.example",
              "motto": "Excellence Through Character",
              "headTeacherName": "Chisom Maxwell",
              "timezone": "Africa/Lagos",
              "versionNumber": 3,
              "logo": {
                "width": 512,
                "height": 512,
                "uploadedAt": "{{CanonicalTimestamp}}",
                "uploadedByName": "Chisom Maxwell"
              },
              "signature": null
            }
            """,

        [typeof(SchoolImageDto)] = $$"""
            {
              "width": 512,
              "height": 512,
              "uploadedAt": "{{CanonicalTimestamp}}",
              "uploadedByName": "Chisom Maxwell"
            }
            """,

        // TASK-0005b stage B2: the multipart request body's own `file` part. A framework type — see
        // DescriptionsByType's remarks below — whose schema is a bare `type: string, format: binary`,
        // so its example is a JSON string rather than an object.
        [typeof(Microsoft.AspNetCore.Http.IFormFile)] = """
            "binary image content, sent as the multipart request's `file` part"
            """,

        // TASK-0005b stage C: the image-serving responses' body. Same bare binary-string shape.
        [typeof(Stream)] = """
            "binary PNG or JPEG bytes"
            """,

        [typeof(SettingsDto)] = $$"""
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
                "versionNumber": 3,
                "logo": {
                  "width": 512,
                  "height": 512,
                  "uploadedAt": "{{CanonicalTimestamp}}",
                  "uploadedByName": "Chisom Maxwell"
                },
                "signature": null
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
              "ratingScales": {
                "scales": [
                  {
                    "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6601",
                    "name": "Nursery development",
                    "points": [
                      { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6701", "pointCode": "N", "pointLabel": "Needs Improvement", "pointOrder": 1 },
                      { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6702", "pointCode": "I", "pointLabel": "Improving", "pointOrder": 2 },
                      { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6703", "pointCode": "S", "pointLabel": "Satisfied", "pointOrder": 3 },
                      { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6704", "pointCode": "E", "pointLabel": "Excellent", "pointOrder": 4 }
                    ]
                  }
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
              },
              "developmentDomains": {
                "domains": [
                  {
                    "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6801",
                    "sectionId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6301",
                    "section": "Nursery",
                    "name": "Personal & Physical Development",
                    "displayOrder": 3,
                    "ratingScaleId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6601",
                    "allowsIndicatorComment": true,
                    "status": "Active",
                    "activeIndicatorCount": 1,
                    "indicators": [
                      { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6901", "name": "Potty trained", "displayOrder": 1, "status": "Active" }
                    ]
                  }
                ],
                "versionNumber": 0
              },
              "traits": {
                "affectiveRatingScaleId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6602",
                "psychomotorRatingScaleId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6602",
                "traits": [
                  { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6a01", "domain": "Affective", "name": "Punctuality", "displayOrder": 2, "status": "Active" }
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

        [typeof(RatingScalePointDto)] = """
            {
              "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6701",
              "pointCode": "E",
              "pointLabel": "Excellent",
              "pointOrder": 4
            }
            """,

        [typeof(RatingScaleDto)] = """
            {
              "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6601",
              "name": "Nursery development",
              "points": [
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6701", "pointCode": "N", "pointLabel": "Needs Improvement", "pointOrder": 1 },
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6702", "pointCode": "I", "pointLabel": "Improving", "pointOrder": 2 },
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6703", "pointCode": "S", "pointLabel": "Satisfied", "pointOrder": 3 },
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6704", "pointCode": "E", "pointLabel": "Excellent", "pointOrder": 4 }
              ]
            }
            """,

        [typeof(SettingsRatingScaleGroupDto)] = """
            {
              "scales": [
                {
                  "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6601",
                  "name": "Nursery development",
                  "points": [
                    { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6701", "pointCode": "N", "pointLabel": "Needs Improvement", "pointOrder": 1 },
                    { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6702", "pointCode": "I", "pointLabel": "Improving", "pointOrder": 2 },
                    { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6703", "pointCode": "S", "pointLabel": "Satisfied", "pointOrder": 3 },
                    { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6704", "pointCode": "E", "pointLabel": "Excellent", "pointOrder": 4 }
                  ]
                }
              ],
              "versionNumber": 0
            }
            """,

        [typeof(RatingScalePointInput)] = """
            {
              "pointCode": "E",
              "pointLabel": "Excellent",
              "pointOrder": 4
            }
            """,

        [typeof(RatingScaleInput)] = """
            {
              "name": "Nursery development",
              "points": [
                { "pointCode": "N", "pointLabel": "Needs Improvement", "pointOrder": 1 },
                { "pointCode": "I", "pointLabel": "Improving", "pointOrder": 2 },
                { "pointCode": "S", "pointLabel": "Satisfied", "pointOrder": 3 },
                { "pointCode": "E", "pointLabel": "Excellent", "pointOrder": 4 }
              ]
            }
            """,

        [typeof(UpdateRatingScalesCommand)] = """
            {
              "scales": [
                {
                  "name": "Nursery development",
                  "points": [
                    { "pointCode": "N", "pointLabel": "Needs Improvement", "pointOrder": 1 },
                    { "pointCode": "I", "pointLabel": "Improving", "pointOrder": 2 },
                    { "pointCode": "S", "pointLabel": "Satisfied", "pointOrder": 3 },
                    { "pointCode": "E", "pointLabel": "Excellent", "pointOrder": 4 }
                  ]
                }
              ],
              "expectedVersion": 0,
              "reason": null
            }
            """,

        [typeof(DevelopmentIndicatorDto)] = """
            {
              "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6901",
              "name": "Potty trained",
              "displayOrder": 1,
              "status": "Active"
            }
            """,

        [typeof(DevelopmentDomainDto)] = """
            {
              "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6801",
              "sectionId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6301",
              "section": "Nursery",
              "name": "Personal & Physical Development",
              "displayOrder": 3,
              "ratingScaleId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6601",
              "allowsIndicatorComment": true,
              "status": "Active",
              "activeIndicatorCount": 1,
              "indicators": [
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6901", "name": "Potty trained", "displayOrder": 1, "status": "Active" }
              ]
            }
            """,

        [typeof(SettingsDevelopmentDomainGroupDto)] = """
            {
              "domains": [
                {
                  "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6801",
                  "sectionId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6301",
                  "section": "Nursery",
                  "name": "Personal & Physical Development",
                  "displayOrder": 3,
                  "ratingScaleId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6601",
                  "allowsIndicatorComment": true,
                  "status": "Active",
                  "activeIndicatorCount": 1,
                  "indicators": [
                    { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6901", "name": "Potty trained", "displayOrder": 1, "status": "Active" }
                  ]
                }
              ],
              "versionNumber": 0
            }
            """,

        [typeof(DevelopmentIndicatorInput)] = """
            {
              "name": "Potty trained",
              "displayOrder": 1,
              "status": "Active"
            }
            """,

        [typeof(DevelopmentDomainInput)] = """
            {
              "sectionId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6301",
              "name": "Personal & Physical Development",
              "displayOrder": 3,
              "ratingScaleId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6601",
              "allowsIndicatorComment": true,
              "status": "Active",
              "indicators": [
                { "name": "Potty trained", "displayOrder": 1, "status": "Active" }
              ]
            }
            """,

        [typeof(UpdateDevelopmentDomainsCommand)] = """
            {
              "domains": [
                {
                  "sectionId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6301",
                  "name": "Personal & Physical Development",
                  "displayOrder": 3,
                  "ratingScaleId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6601",
                  "allowsIndicatorComment": true,
                  "status": "Active",
                  "indicators": [
                    { "name": "Potty trained", "displayOrder": 1, "status": "Active" }
                  ]
                }
              ],
              "expectedVersion": 0,
              "reason": null
            }
            """,

        [typeof(TraitDto)] = """
            {
              "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6a01",
              "domain": "Affective",
              "name": "Punctuality",
              "displayOrder": 2,
              "status": "Active"
            }
            """,

        [typeof(SettingsTraitsGroupDto)] = """
            {
              "affectiveRatingScaleId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6602",
              "psychomotorRatingScaleId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6602",
              "traits": [
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6a01", "domain": "Affective", "name": "Punctuality", "displayOrder": 2, "status": "Active" }
              ],
              "versionNumber": 0
            }
            """,

        [typeof(TraitInput)] = """
            {
              "domain": "Affective",
              "name": "Punctuality",
              "displayOrder": 2,
              "status": "Active"
            }
            """,

        [typeof(UpdateTraitsCommand)] = """
            {
              "affectiveRatingScaleId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6602",
              "psychomotorRatingScaleId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6602",
              "traits": [
                { "domain": "Affective", "name": "Punctuality", "displayOrder": 2, "status": "Active" }
              ],
              "expectedVersion": 0,
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

        [typeof(ResultRulesDto)] = """
            {
              "annualMethod": "SimpleAverage",
              "weightFirst": null,
              "weightSecond": null,
              "weightThird": null,
              "primaryPositionScope": "Arm",
              "showLevelPosition": true,
              "tieBreakRule": "SharedPosition",
              "passMark": 40,
              "promotionThreshold": 40,
              "requireCorePass": true,
              "coreSubjectIds": [],
              "minSubjectsForPosition": 1,
              "versionNumber": 0
            }
            """,

        [typeof(UpdateResultRulesCommand)] = """
            {
              "annualMethod": "SimpleAverage",
              "weightFirst": null,
              "weightSecond": null,
              "weightThird": null,
              "primaryPositionScope": "Arm",
              "showLevelPosition": true,
              "tieBreakRule": "SharedPosition",
              "passMark": 40,
              "promotionThreshold": 40,
              "requireCorePass": true,
              "coreSubjectIds": [],
              "minSubjectsForPosition": 1,
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

        // TASK-0088 stage B: the contract's first typed problem-details extension — see
        // ResultSetNotReadyProblemDetails's own remarks. `readiness` is the SAME shape
        // ResultSetReadinessDto's own example above carries, condensed here to one incomplete pupil.
        [typeof(SchoolManagement.Api.Http.ResultSetNotReadyProblemDetails)] = $$"""
            {
              "type": "urn:schoolmanagement:error:result_set.not_ready",
              "title": "Validation failed",
              "status": 422,
              "detail": "This result set is not ready to submit. See the readiness grid.",
              "instance": "/api/v1/result-sets/{{ExampleResultSetId}}/submit",
              "errorCode": "result_set.not_ready",
              "traceId": "0af7651916cd43dd8448eb211c80319c",
              "readiness": {
                "armId": "{{ExampleArmId}}",
                "termId": "{{ExampleTermId}}",
                "resultSet": {
                  "id": "{{ExampleResultSetId}}",
                  "state": "Draft",
                  "needsRecompute": false,
                  "returnReason": null
                },
                "subjects": [
                  {
                    "subjectId": "{{ExampleSubjectId}}",
                    "name": "Mathematics"
                  }
                ],
                "componentCount": 2,
                "pupils": [
                  {
                    "pupilId": "{{ExampleSecondPupilId}}",
                    "registrationNumber": "GRAS/2026/0042",
                    "displayName": "Bello Musa",
                    "marks": [
                      {
                        "subjectId": "{{ExampleSubjectId}}",
                        "status": "Partial",
                        "filledParts": 1
                      }
                    ],
                    "ratingsComplete": false,
                    "attendanceComplete": false,
                    "classTeacherRemarkPresent": false,
                    "headTeacherRemarkPresent": false
                  }
                ],
                "leftDuringTerm": [],
                "counters": {
                  "marks": { "complete": 0, "total": 1 },
                  "ratings": { "complete": 0, "total": 1 },
                  "attendance": { "complete": 0, "total": 1 },
                  "classTeacherRemarks": { "complete": 0, "total": 1 },
                  "headTeacherRemarks": { "complete": 0, "total": 1 }
                },
                "blockers": [
                  {
                    "code": "marks_incomplete",
                    "message": "1 of 1 mark cells are still missing."
                  }
                ],
                "canSubmit": false
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
              "name": "Secondary",
              "ratesTraits": false
            }
            """,

        [typeof(UpdateSectionCommand)] = $$"""
            {
              "id": "{{ExampleId}}",
              "name": "Secondary",
              "ratesTraits": true
            }
            """,

        [typeof(SectionDto)] = $$"""
            {
              "id": "{{ExampleId}}",
              "name": "Primary",
              "ratesTraits": true
            }
            """,

        [typeof(SectionListResponse)] = $$"""
            {
              "sections": [
                { "id": "0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d45", "name": "Nursery", "ratesTraits": false },
                { "id": "{{ExampleId}}", "name": "Primary", "ratesTraits": true }
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

        [typeof(ScoreSheetComponentDto)] = $$"""
            {
              "id": "{{ExampleComponentId}}",
              "label": "CA1",
              "maxMark": 20
            }
            """,

        [typeof(ResultSetSummaryDto)] = $$"""
            {
              "id": "{{ExampleResultSetId}}",
              "state": "Draft",
              "needsRecompute": true,
              "returnReason": null
            }
            """,

        [typeof(ScoreSheetRowDto)] = $$"""
            {
              "pupilId": "{{ExamplePupilId}}",
              "registrationNumber": "GRAS/2026/0041",
              "displayName": "Okafor Chidera Ngozi",
              "componentMarks": {
                "{{ExampleComponentId}}": 18
              },
              "examMark": 55,
              "examAbsent": false,
              "caTotal": 18,
              "subjectTotal": 73
            }
            """,

        [typeof(ScoreSheetDto)] = $$"""
            {
              "armId": "{{ExampleArmId}}",
              "subjectId": "{{ExampleSubjectId}}",
              "termId": "{{ExampleTermId}}",
              "version": "5f3759df1f2c4a9b8e0d6c7a3b1f9e2d4c6a8b0d2e4f6a8c0e2d4f6a8b0c2e4f",
              "resultSet": {
                "id": "{{ExampleResultSetId}}",
                "state": "Draft",
                "needsRecompute": true,
                "returnReason": null
              },
              "components": [
                {
                  "id": "{{ExampleComponentId}}",
                  "label": "CA1",
                  "maxMark": 20
                }
              ],
              "examination": {
                "id": "{{ExampleExaminationComponentId}}",
                "label": "Exam",
                "maxMark": 60
              },
              "rows": [
                {
                  "pupilId": "{{ExamplePupilId}}",
                  "registrationNumber": "GRAS/2026/0041",
                  "displayName": "Okafor Chidera Ngozi",
                  "componentMarks": {
                    "{{ExampleComponentId}}": 18
                  },
                  "examMark": 55,
                  "examAbsent": false,
                  "caTotal": 18,
                  "subjectTotal": 73
                },
                {
                  "pupilId": "{{ExampleSecondPupilId}}",
                  "registrationNumber": "GRAS/2026/0042",
                  "displayName": "Bello Musa",
                  "componentMarks": {
                    "{{ExampleComponentId}}": null
                  },
                  "examMark": null,
                  "examAbsent": false,
                  "caTotal": null,
                  "subjectTotal": null
                }
              ]
            }
            """,

        [typeof(SaveScoreSheetRowInput)] = $$"""
            {
              "pupilId": "{{ExamplePupilId}}",
              "componentMarks": {
                "{{ExampleComponentId}}": 18
              },
              "examMark": 55,
              "examAbsent": false
            }
            """,

        [typeof(SaveScoreSheetCommand)] = $$"""
            {
              "armId": "{{ExampleArmId}}",
              "subjectId": "{{ExampleSubjectId}}",
              "termId": "{{ExampleTermId}}",
              "version": "5f3759df1f2c4a9b8e0d6c7a3b1f9e2d4c6a8b0d2e4f6a8c0e2d4f6a8b0c2e4f",
              "rows": [
                {
                  "pupilId": "{{ExamplePupilId}}",
                  "componentMarks": {
                    "{{ExampleComponentId}}": 18
                  },
                  "examMark": 55,
                  "examAbsent": false
                },
                {
                  "pupilId": "{{ExampleSecondPupilId}}",
                  "componentMarks": {
                    "{{ExampleComponentId}}": null
                  },
                  "examMark": null,
                  "examAbsent": false
                }
              ]
            }
            """,

        [typeof(TraitRatingBlockDto)] = """
            {
              "domain": "Affective",
              "scale": {
                "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6602",
                "name": "Primary trait",
                "points": [
                  { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6705", "pointCode": "N", "pointLabel": "Needs Improvement", "pointOrder": 1 },
                  { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6706", "pointCode": "I", "pointLabel": "Improving", "pointOrder": 2 },
                  { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6707", "pointCode": "E", "pointLabel": "Excellent", "pointOrder": 3 }
                ]
              },
              "traits": [
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6a01", "domain": "Affective", "name": "Punctuality", "displayOrder": 2, "status": "Active" }
              ]
            }
            """,

        [typeof(TraitRatingRowDto)] = $$"""
            {
              "pupilId": "{{ExamplePupilId}}",
              "registrationNumber": "GRAS/2026/0041",
              "displayName": "Okafor Chidera Ngozi",
              "ratings": {
                "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6a01": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6707"
              }
            }
            """,

        [typeof(TraitRatingSheetDto)] = $$"""
            {
              "armId": "{{ExampleArmId}}",
              "termId": "{{ExampleTermId}}",
              "version": "5f3759df1f2c4a9b8e0d6c7a3b1f9e2d4c6a8b0d2e4f6a8c0e2d4f6a8b0c2e4f",
              "resultSet": {
                "id": "{{ExampleResultSetId}}",
                "state": "Draft",
                "needsRecompute": true,
                "returnReason": null
              },
              "blocks": [
                {
                  "domain": "Affective",
                  "scale": {
                    "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6602",
                    "name": "Primary trait",
                    "points": [
                      { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6705", "pointCode": "N", "pointLabel": "Needs Improvement", "pointOrder": 1 },
                      { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6706", "pointCode": "I", "pointLabel": "Improving", "pointOrder": 2 },
                      { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6707", "pointCode": "E", "pointLabel": "Excellent", "pointOrder": 3 }
                    ]
                  },
                  "traits": [
                    { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6a01", "domain": "Affective", "name": "Punctuality", "displayOrder": 2, "status": "Active" }
                  ]
                }
              ],
              "rows": [
                {
                  "pupilId": "{{ExamplePupilId}}",
                  "registrationNumber": "GRAS/2026/0041",
                  "displayName": "Okafor Chidera Ngozi",
                  "ratings": {
                    "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6a01": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6707"
                  }
                },
                {
                  "pupilId": "{{ExampleSecondPupilId}}",
                  "registrationNumber": "GRAS/2026/0042",
                  "displayName": "Bello Musa",
                  "ratings": {
                    "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6a01": null
                  }
                }
              ]
            }
            """,

        [typeof(SaveTraitRatingsRowInput)] = $$"""
            {
              "pupilId": "{{ExamplePupilId}}",
              "ratings": {
                "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6a01": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6707"
              }
            }
            """,

        [typeof(SaveTraitRatingsCommand)] = $$"""
            {
              "armId": "{{ExampleArmId}}",
              "termId": "{{ExampleTermId}}",
              "version": "5f3759df1f2c4a9b8e0d6c7a3b1f9e2d4c6a8b0d2e4f6a8c0e2d4f6a8b0c2e4f",
              "rows": [
                {
                  "pupilId": "{{ExamplePupilId}}",
                  "ratings": {
                    "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6a01": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6707"
                  }
                },
                {
                  "pupilId": "{{ExampleSecondPupilId}}",
                  "ratings": {
                    "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6a01": null
                  }
                }
              ]
            }
            """,

        [typeof(DevelopmentRatingCellDto)] = """
            {
              "pointId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6704",
              "comment": "Needs reminding after lunch."
            }
            """,

        [typeof(DevelopmentRatingDomainDto)] = """
            {
              "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6801",
              "name": "Personal & Physical Development",
              "displayOrder": 3,
              "ratingScaleId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6601",
              "scale": {
                "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6601",
                "name": "Nursery development",
                "points": [
                  { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6701", "pointCode": "N", "pointLabel": "Needs Improvement", "pointOrder": 1 },
                  { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6702", "pointCode": "I", "pointLabel": "Improving", "pointOrder": 2 },
                  { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6703", "pointCode": "S", "pointLabel": "Satisfied", "pointOrder": 3 },
                  { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6704", "pointCode": "E", "pointLabel": "Excellent", "pointOrder": 4 }
                ]
              },
              "allowsIndicatorComment": true,
              "activeIndicatorCount": 1,
              "indicators": [
                { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6901", "name": "Potty trained", "displayOrder": 1, "status": "Active" }
              ]
            }
            """,

        [typeof(DevelopmentRatingRowDto)] = $$"""
            {
              "pupilId": "{{ExamplePupilId}}",
              "registrationNumber": "GRAS/2026/0041",
              "displayName": "Okafor Chidera Ngozi",
              "ratings": {
                "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6901": { "pointId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6704", "comment": "Needs reminding after lunch." }
              }
            }
            """,

        [typeof(DevelopmentRatingSheetDto)] = $$"""
            {
              "armId": "{{ExampleArmId}}",
              "termId": "{{ExampleTermId}}",
              "version": "5f3759df1f2c4a9b8e0d6c7a3b1f9e2d4c6a8b0d2e4f6a8c0e2d4f6a8b0c2e4f",
              "resultSet": {
                "id": "{{ExampleResultSetId}}",
                "state": "Draft",
                "needsRecompute": true,
                "returnReason": null
              },
              "domains": [
                {
                  "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6801",
                  "name": "Personal & Physical Development",
                  "displayOrder": 3,
                  "ratingScaleId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6601",
                  "scale": {
                    "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6601",
                    "name": "Nursery development",
                    "points": [
                      { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6701", "pointCode": "N", "pointLabel": "Needs Improvement", "pointOrder": 1 },
                      { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6702", "pointCode": "I", "pointLabel": "Improving", "pointOrder": 2 },
                      { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6703", "pointCode": "S", "pointLabel": "Satisfied", "pointOrder": 3 },
                      { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6704", "pointCode": "E", "pointLabel": "Excellent", "pointOrder": 4 }
                    ]
                  },
                  "allowsIndicatorComment": true,
                  "activeIndicatorCount": 1,
                  "indicators": [
                    { "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6901", "name": "Potty trained", "displayOrder": 1, "status": "Active" }
                  ]
                }
              ],
              "activeIndicatorTotal": 1,
              "rows": [
                {
                  "pupilId": "{{ExamplePupilId}}",
                  "registrationNumber": "GRAS/2026/0041",
                  "displayName": "Okafor Chidera Ngozi",
                  "ratings": {
                    "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6901": { "pointId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6704", "comment": "Needs reminding after lunch." }
                  }
                },
                {
                  "pupilId": "{{ExampleSecondPupilId}}",
                  "registrationNumber": "GRAS/2026/0042",
                  "displayName": "Bello Musa",
                  "ratings": {
                    "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6901": { "pointId": null, "comment": null }
                  }
                }
              ]
            }
            """,

        [typeof(SaveDevelopmentRatingsRowInput)] = $$"""
            {
              "pupilId": "{{ExamplePupilId}}",
              "ratings": {
                "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6901": { "pointId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6704", "comment": "Needs reminding after lunch." }
              }
            }
            """,

        [typeof(SaveDevelopmentRatingsCommand)] = $$"""
            {
              "armId": "{{ExampleArmId}}",
              "termId": "{{ExampleTermId}}",
              "version": "5f3759df1f2c4a9b8e0d6c7a3b1f9e2d4c6a8b0d2e4f6a8c0e2d4f6a8b0c2e4f",
              "rows": [
                {
                  "pupilId": "{{ExamplePupilId}}",
                  "ratings": {
                    "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6901": { "pointId": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6704", "comment": "Needs reminding after lunch." }
                  }
                },
                {
                  "pupilId": "{{ExampleSecondPupilId}}",
                  "ratings": {
                    "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6901": null
                  }
                }
              ]
            }
            """,

        [typeof(AttendanceRowDto)] = $$"""
            {
              "pupilId": "{{ExamplePupilId}}",
              "registrationNumber": "GRAS/2026/0041",
              "displayName": "Okafor Chidera Ngozi",
              "timesPresent": 58,
              "timesAbsent": 4
            }
            """,

        [typeof(AttendanceSheetDto)] = $$"""
            {
              "armId": "{{ExampleArmId}}",
              "termId": "{{ExampleTermId}}",
              "version": "5f3759df1f2c4a9b8e0d6c7a3b1f9e2d4c6a8b0d2e4f6a8c0e2d4f6a8b0c2e4f",
              "resultSet": {
                "id": "{{ExampleResultSetId}}",
                "state": "Draft",
                "needsRecompute": true,
                "returnReason": null
              },
              "timesSchoolOpened": 62,
              "rows": [
                {
                  "pupilId": "{{ExamplePupilId}}",
                  "registrationNumber": "GRAS/2026/0041",
                  "displayName": "Okafor Chidera Ngozi",
                  "timesPresent": 58,
                  "timesAbsent": 4
                },
                {
                  "pupilId": "{{ExampleSecondPupilId}}",
                  "registrationNumber": "GRAS/2026/0042",
                  "displayName": "Bello Musa",
                  "timesPresent": null,
                  "timesAbsent": null
                }
              ]
            }
            """,

        [typeof(SaveAttendanceRowInput)] = $$"""
            {
              "pupilId": "{{ExamplePupilId}}",
              "timesPresent": 58
            }
            """,

        [typeof(SaveAttendanceCommand)] = $$"""
            {
              "armId": "{{ExampleArmId}}",
              "termId": "{{ExampleTermId}}",
              "version": "5f3759df1f2c4a9b8e0d6c7a3b1f9e2d4c6a8b0d2e4f6a8c0e2d4f6a8b0c2e4f",
              "rows": [
                { "pupilId": "{{ExamplePupilId}}", "timesPresent": 58 },
                { "pupilId": "{{ExampleSecondPupilId}}", "timesPresent": null }
              ]
            }
            """,

        [typeof(RemarkRowDto)] = $$"""
            {
              "pupilId": "{{ExamplePupilId}}",
              "registrationNumber": "GRAS/2026/0041",
              "displayName": "Okafor Chidera Ngozi",
              "remark": "A diligent and attentive pupil this term.",
              "writtenByName": "Mrs Adeyemi",
              "writtenAt": "2026-12-12T09:30:00+01:00"
            }
            """,

        [typeof(RemarkSheetDto)] = $$"""
            {
              "armId": "{{ExampleArmId}}",
              "termId": "{{ExampleTermId}}",
              "version": "5f3759df1f2c4a9b8e0d6c7a3b1f9e2d4c6a8b0d2e4f6a8c0e2d4f6a8b0c2e4f",
              "resultSet": {
                "id": "{{ExampleResultSetId}}",
                "state": "Draft",
                "needsRecompute": true,
                "returnReason": null
              },
              "rows": [
                {
                  "pupilId": "{{ExamplePupilId}}",
                  "registrationNumber": "GRAS/2026/0041",
                  "displayName": "Okafor Chidera Ngozi",
                  "remark": "A diligent and attentive pupil this term.",
                  "writtenByName": "Mrs Adeyemi",
                  "writtenAt": "2026-12-12T09:30:00+01:00"
                },
                {
                  "pupilId": "{{ExampleSecondPupilId}}",
                  "registrationNumber": "GRAS/2026/0042",
                  "displayName": "Bello Musa",
                  "remark": null,
                  "writtenByName": null,
                  "writtenAt": null
                }
              ]
            }
            """,

        [typeof(SaveRemarkRowInput)] = $$"""
            {
              "pupilId": "{{ExamplePupilId}}",
              "remark": "A diligent and attentive pupil this term."
            }
            """,

        [typeof(SaveClassTeacherRemarksCommand)] = $$"""
            {
              "armId": "{{ExampleArmId}}",
              "termId": "{{ExampleTermId}}",
              "version": "5f3759df1f2c4a9b8e0d6c7a3b1f9e2d4c6a8b0d2e4f6a8c0e2d4f6a8b0c2e4f",
              "rows": [
                { "pupilId": "{{ExamplePupilId}}", "remark": "A diligent and attentive pupil this term." },
                { "pupilId": "{{ExampleSecondPupilId}}", "remark": null }
              ]
            }
            """,

        [typeof(SaveHeadTeacherRemarksCommand)] = $$"""
            {
              "armId": "{{ExampleArmId}}",
              "termId": "{{ExampleTermId}}",
              "version": "5f3759df1f2c4a9b8e0d6c7a3b1f9e2d4c6a8b0d2e4f6a8c0e2d4f6a8b0c2e4f",
              "rows": [
                { "pupilId": "{{ExamplePupilId}}", "remark": "A pleasure to have in school." }
              ],
              "fillEmpty": "Keep up the good work."
            }
            """,

        [typeof(RemarkTemplateDto)] = $$"""
            {
              "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6a41",
              "kind": "ClassTeacher",
              "text": "A pleasure to have in school.",
              "createdAt": "2026-09-19T09:00:00Z"
            }
            """,

        [typeof(RemarkTemplateListDto)] = $$"""
            {
              "templates": [
                {
                  "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6a41",
                  "kind": "ClassTeacher",
                  "text": "A pleasure to have in school.",
                  "createdAt": "2026-09-19T09:00:00Z"
                },
                {
                  "id": "0192f0c4-e1a5-7f00-8f11-2c3d4e5f6a42",
                  "kind": "ClassTeacher",
                  "text": "Needs to concentrate more in class.",
                  "createdAt": "2026-09-19T09:05:00Z"
                }
              ]
            }
            """,

        [typeof(CreateRemarkTemplateCommand)] = """
            {
              "kind": "ClassTeacher",
              "text": "A pleasure to have in school."
            }
            """,

        [typeof(VoidScoreSheetCommand)] = $$"""
            {
              "armId": "{{ExampleArmId}}",
              "subjectId": "{{ExampleSubjectId}}",
              "termId": "{{ExampleTermId}}",
              "reason": "Whole class re-marked after a transcription error in the mark book."
            }
            """,

        [typeof(VoidScoreSheetResponse)] = """
            {
              "voidedCount": 27
            }
            """,

        [typeof(ComputeAnnualResultsResponse)] = $$"""
            {
              "armId": "{{ExampleArmId}}",
              "sessionId": "{{ExampleSessionId}}",
              "computedAt": "2027-07-24T10:00:00Z",
              "pupilCount": 28,
              "rankedCount": 27,
              "proposedPromoted": 26,
              "proposedRepeat": 2
            }
            """,
        [typeof(ComputeResultSetResponse)] = $$"""
            {
              "resultSetId": "{{ExampleResultSetId}}",
              "computedAt": "2026-12-18T09:30:00Z",
              "pupilCount": 28,
              "subjectCount": 9,
              "flags": [
                {
                  "code": "absent_all_examinations",
                  "subjectId": null,
                  "pupilId": "{{ExamplePupilId}}"
                }
              ]
            }
            """,

        [typeof(ComputeResultSetFlagDto)] = $$"""
            {
              "code": "no_examination_sat",
              "subjectId": "{{ExampleSubjectId}}",
              "pupilId": null
            }
            """,

        [typeof(ReadinessSubjectDto)] = $$"""
            {
              "subjectId": "{{ExampleSubjectId}}",
              "name": "Mathematics"
            }
            """,

        [typeof(ReadinessMarkCellDto)] = $$"""
            {
              "subjectId": "{{ExampleSubjectId}}",
              "status": "Complete",
              "filledParts": 2
            }
            """,

        [typeof(ReadinessPupilRowDto)] = $$"""
            {
              "pupilId": "{{ExamplePupilId}}",
              "registrationNumber": "GRAS/2026/0041",
              "displayName": "Okafor Chidera Ngozi",
              "marks": [
                {
                  "subjectId": "{{ExampleSubjectId}}",
                  "status": "Complete",
                  "filledParts": 2
                }
              ],
              "ratingsComplete": true,
              "attendanceComplete": true,
              "classTeacherRemarkPresent": true,
              "headTeacherRemarkPresent": false
            }
            """,

        [typeof(ReadinessLeftDuringTermPupilDto)] = $$"""
            {
              "pupilId": "{{ExampleThirdPupilId}}",
              "registrationNumber": "GRAS/2026/0009",
              "displayName": "Nwachukwu Ifeoma",
              "leftOn": "2026-11-02"
            }
            """,

        [typeof(ReadinessCounterDto)] = """
            {
              "complete": 26,
              "total": 28
            }
            """,

        [typeof(ReadinessCountersDto)] = """
            {
              "marks": { "complete": 246, "total": 252 },
              "ratings": { "complete": 28, "total": 28 },
              "attendance": { "complete": 26, "total": 28 },
              "classTeacherRemarks": { "complete": 24, "total": 28 },
              "headTeacherRemarks": { "complete": 0, "total": 28 }
            }
            """,

        [typeof(ReadinessBlockerDto)] = """
            {
              "code": "attendance_incomplete",
              "message": "2 of 28 pupils are missing attendance."
            }
            """,

        [typeof(ResultSetReadinessDto)] = $$"""
            {
              "armId": "{{ExampleArmId}}",
              "termId": "{{ExampleTermId}}",
              "resultSet": {
                "id": "{{ExampleResultSetId}}",
                "state": "Draft",
                "needsRecompute": false,
                "returnReason": null
              },
              "subjects": [
                {
                  "subjectId": "{{ExampleSubjectId}}",
                  "name": "Mathematics"
                }
              ],
              "componentCount": 2,
              "pupils": [
                {
                  "pupilId": "{{ExamplePupilId}}",
                  "registrationNumber": "GRAS/2026/0041",
                  "displayName": "Okafor Chidera Ngozi",
                  "marks": [
                    {
                      "subjectId": "{{ExampleSubjectId}}",
                      "status": "Complete",
                      "filledParts": 2
                    }
                  ],
                  "ratingsComplete": true,
                  "attendanceComplete": true,
                  "classTeacherRemarkPresent": true,
                  "headTeacherRemarkPresent": false
                },
                {
                  "pupilId": "{{ExampleSecondPupilId}}",
                  "registrationNumber": "GRAS/2026/0042",
                  "displayName": "Bello Musa",
                  "marks": [
                    {
                      "subjectId": "{{ExampleSubjectId}}",
                      "status": "Partial",
                      "filledParts": 1
                    }
                  ],
                  "ratingsComplete": false,
                  "attendanceComplete": false,
                  "classTeacherRemarkPresent": false,
                  "headTeacherRemarkPresent": false
                }
              ],
              "leftDuringTerm": [
                {
                  "pupilId": "{{ExampleThirdPupilId}}",
                  "registrationNumber": "GRAS/2026/0009",
                  "displayName": "Nwachukwu Ifeoma",
                  "leftOn": "2026-11-02"
                }
              ],
              "counters": {
                "marks": { "complete": 1, "total": 2 },
                "ratings": { "complete": 1, "total": 2 },
                "attendance": { "complete": 1, "total": 2 },
                "classTeacherRemarks": { "complete": 1, "total": 2 },
                "headTeacherRemarks": { "complete": 0, "total": 2 }
              },
              "blockers": [
                {
                  "code": "marks_incomplete",
                  "message": "1 of 2 mark cells are still missing."
                }
              ],
              "canSubmit": false
            }
            """,

        [typeof(SubmitResultSetResponse)] = $$"""
            {
              "resultSet": {
                "id": "{{ExampleResultSetId}}",
                "state": "AwaitingApproval",
                "needsRecompute": false,
                "returnReason": null
              },
              "submittedAt": "{{CanonicalTimestamp}}"
            }
            """,

        [typeof(ApproveResultSetResponse)] = $$"""
            {
              "resultSet": {
                "id": "{{ExampleResultSetId}}",
                "state": "Approved",
                "needsRecompute": false,
                "returnReason": null
              },
              "approvedAt": "{{CanonicalTimestamp}}"
            }
            """,

        [typeof(PinBatchDto)] = $$"""
            {
                  "id": "0192f0c4-9a10-7000-8000-000000000301",
                  "sessionId": "0192f0c4-9a10-7000-8000-000000000302",
                  "name": "2026/2027 First Term batch 1",
                  "purposeNote": "Primary 3 and Primary 4 parents",
                  "pinLength": 10,
                  "maxUses": 3,
                  "pinCount": 120,
                  "pinsUsed": 34,
                  "pinsExhausted": 2,
                  "pinsSuspended": 0,
                  "pinsRevoked": 0,
                  "state": "Active",
                  "generatedAt": "{{CanonicalTimestamp}}",
                  "plaintextPurgeAt": "2026-09-02T09:30:00+00:00",
                  "revokeReason": null
                }
            """,

        [typeof(PinSummaryDto)] = $$"""
            {
                  "id": "0192f0c4-9a10-7000-8000-000000000303",
                  "prefix": "H7K2",
                  "state": "Active",
                  "useCount": 1,
                  "maxUses": 3,
                  "distinctPupilCount": 1,
                  "stateReason": null
                }
            """,

        [typeof(PinBatchDetailDto)] = $$"""
            {
              "batch": {
                  "id": "0192f0c4-9a10-7000-8000-000000000301",
                  "sessionId": "0192f0c4-9a10-7000-8000-000000000302",
                  "name": "2026/2027 First Term batch 1",
                  "purposeNote": "Primary 3 and Primary 4 parents",
                  "pinLength": 10,
                  "maxUses": 3,
                  "pinCount": 120,
                  "pinsUsed": 34,
                  "pinsExhausted": 2,
                  "pinsSuspended": 0,
                  "pinsRevoked": 0,
                  "state": "Active",
                  "generatedAt": "{{CanonicalTimestamp}}",
                  "plaintextPurgeAt": "2026-09-02T09:30:00+00:00",
                  "revokeReason": null
                },
              "pins": [
                {
                  "id": "0192f0c4-9a10-7000-8000-000000000303",
                  "prefix": "H7K2",
                  "state": "Active",
                  "useCount": 1,
                  "maxUses": 3,
                  "distinctPupilCount": 1,
                  "stateReason": null
                }
              ]
            }
            """,

        [typeof(CursorPage<PinBatchDto>)] = $$"""
            {
              "items": [
                {
                  "id": "0192f0c4-9a10-7000-8000-000000000301",
                  "sessionId": "0192f0c4-9a10-7000-8000-000000000302",
                  "name": "2026/2027 First Term batch 1",
                  "purposeNote": "Primary 3 and Primary 4 parents",
                  "pinLength": 10,
                  "maxUses": 3,
                  "pinCount": 120,
                  "pinsUsed": 34,
                  "pinsExhausted": 2,
                  "pinsSuspended": 0,
                  "pinsRevoked": 0,
                  "state": "Active",
                  "generatedAt": "{{CanonicalTimestamp}}",
                  "plaintextPurgeAt": "2026-09-02T09:30:00+00:00",
                  "revokeReason": null
                }
              ],
              "nextCursor": "0192f0c4-9a10-7000-8000-000000000301"
            }
            """,

        [typeof(GeneratePinBatchCommand)] = """
            {
              "sessionId": "0192f0c4-9a10-7000-8000-000000000302",
              "name": null,
              "purposeNote": "Primary 3 and Primary 4 parents",
              "pinCount": 120,
              "pinLength": 10,
              "maxUses": 3,
              "confirmMaxUses": null
            }
            """,

        [typeof(PinReasonRequest)] = """
            {
              "reason": "A sheet of slips went missing from the office."
            }
            """,

        [typeof(PublishResultSetResponse)] = $$"""
            {
              "resultSet": {
                "id": "{{ExampleResultSetId}}",
                "state": "Published",
                "needsRecompute": false,
                "returnReason": null
              },
              "publishedAt": "{{CanonicalTimestamp}}",
              "revisionNumber": 1
            }
            """,

        [typeof(WithdrawResultSetRequest)] = """
            {
              "reason": "Mathematics marks were entered for the wrong class. Withdrawing to correct them."
            }
            """,

        [typeof(ResultSetTransitionResponse)] = $$"""
            {
              "resultSet": {
                "id": "{{ExampleResultSetId}}",
                "state": "Withdrawn",
                "needsRecompute": false,
                "returnReason": null
              }
            }
            """,

        [typeof(ReturnResultSetCommand)] = $$"""
            {
              "resultSetId": "{{ExampleResultSetId}}",
              "reason": "Mathematics examination marks for the whole class look 10 marks too low. Check against the mark book."
            }
            """,

        [typeof(ReturnResultSetResponse)] = $$"""
            {
              "resultSet": {
                "id": "{{ExampleResultSetId}}",
                "state": "ReturnedForCorrection",
                "needsRecompute": false,
                "returnReason": "Mathematics examination marks for the whole class look 10 marks too low. Check against the mark book."
              }
            }
            """,

        // Spec 6.10: weekly report sheets.
        [typeof(WeeklyDayDto)] = WeeklyMondayExample,

        [typeof(WeeklyGridRowDto)] = $$"""
            {
              "pupilId": "{{ExamplePupilId}}",
              "registrationNumber": "GRAS/2026/0041",
              "displayName": "Okafor Chidera Ngozi",
              "onRoll": true,
              "illnessDays": 0,
              "days": [ {{WeeklyMondayExample}} ]
            }
            """,

        [typeof(WeeklyWeekSummaryDto)] = """
            {
              "weekNumber": 4,
              "startDate": "2027-01-11",
              "endDate": "2027-01-15",
              "outsideTerm": false,
              "published": true,
              "pupilsWithNotes": 27
            }
            """,

        [typeof(WeeklyPhrasesDto)] = WeeklyPhrasesExample,

        [typeof(WeeklyGridDto)] = $$"""
            {
              "armId": "{{ExampleArmId}}",
              "termId": "{{ExampleTermId}}",
              "weekNumber": 4,
              "weekStartDate": "2027-01-11",
              "weekEndDate": "2027-01-15",
              "outsideTerm": false,
              "published": true,
              "publishedAt": "{{CanonicalTimestamp}}",
              "autoPublish": false,
              "locked": false,
              "rows": [
                {
                  "pupilId": "{{ExamplePupilId}}",
                  "registrationNumber": "GRAS/2026/0041",
                  "displayName": "Okafor Chidera Ngozi",
                  "onRoll": true,
                  "illnessDays": 0,
                  "days": [ {{WeeklyMondayExample}} ]
                }
              ],
              "weeks": [
                { "weekNumber": 4, "startDate": "2027-01-11", "endDate": "2027-01-15", "outsideTerm": false, "published": true, "pupilsWithNotes": 27 }
              ],
              "phrases": {{WeeklyPhrasesExample}}
            }
            """,

        [typeof(TermWeekDto)] = """
            { "weekNumber": 4, "startDate": "2027-01-11", "endDate": "2027-01-15" }
            """,

        [typeof(TermWeekListResponse)] = $$"""
            {
              "termId": "{{ExampleTermId}}",
              "items": [
                { "weekNumber": 1, "startDate": "2026-12-21", "endDate": "2026-12-25" },
                { "weekNumber": 2, "startDate": "2026-12-28", "endDate": "2027-01-01" }
              ]
            }
            """,

        [typeof(PupilWeeklyWeekDto)] = $$"""
            {
              "weekNumber": 4,
              "startDate": "2027-01-11",
              "endDate": "2027-01-15",
              "outsideTerm": false,
              "armId": "{{ExampleArmId}}",
              "published": true,
              "days": [ {{WeeklyMondayExample}} ]
            }
            """,

        [typeof(PupilWeeklyTermDto)] = $$"""
            {
              "pupilId": "{{ExamplePupilId}}",
              "registrationNumber": "GRAS/2026/0041",
              "displayName": "Okafor Chidera Ngozi",
              "termId": "{{ExampleTermId}}",
              "weeks": [
                { "weekNumber": 3, "startDate": "2027-01-04", "endDate": "2027-01-08", "outsideTerm": false, "armId": null, "published": false, "days": null },
                {
                  "weekNumber": 4,
                  "startDate": "2027-01-11",
                  "endDate": "2027-01-15",
                  "outsideTerm": false,
                  "armId": "{{ExampleArmId}}",
                  "published": true,
                  "days": [ {{WeeklyMondayExample}} ]
                }
              ]
            }
            """,

        [typeof(WeeklySettingsDto)] = $$"""
            { "armId": "{{ExampleArmId}}", "autoPublish": true }
            """,

        [typeof(WeeklyCompletionRowDto)] = WeeklyCompletionRowExample,

        [typeof(WeeklyCompletionReportDto)] = $$"""
            { "termId": "{{ExampleTermId}}", "items": [ {{WeeklyCompletionRowExample}} ] }
            """,

        [typeof(WeeklyIllnessObservationDto)] = """
            { "date": "2027-01-12", "text": "Runny nose, sent home at noon" }
            """,

        [typeof(WeeklyIllnessRowDto)] = WeeklyIllnessRowExample,

        [typeof(WeeklyIllnessReportDto)] = $$"""
            { "termId": "{{ExampleTermId}}", "items": [ {{WeeklyIllnessRowExample}} ] }
            """,

        [typeof(WeeklyCellInput)] = $$"""
            { "pupilId": "{{ExamplePupilId}}", "dayOfWeek": "Monday", "field": "Eating", "value": "Ate everything" }
            """,

        [typeof(SaveWeeklyNotesCommand)] = $$"""
            {
              "armId": "{{ExampleArmId}}",
              "termId": "{{ExampleTermId}}",
              "weekNumber": 4,
              "cells": [
                { "pupilId": "{{ExamplePupilId}}", "dayOfWeek": "Monday", "field": "Eating", "value": "Ate everything" },
                { "pupilId": "{{ExampleSecondPupilId}}", "dayOfWeek": "Monday", "field": "Eating", "value": null }
              ]
            }
            """,

        [typeof(WeeklyPublicationRequest)] = $$"""
            { "termId": "{{ExampleTermId}}" }
            """,

        [typeof(UpdateWeeklySettingsCommand)] = $$"""
            { "armId": "{{ExampleArmId}}", "autoPublish": true }
            """,

        // Spec 6.5.5-6.5.8 and 6.5.12: the admission form's per-pupil sections.
        [typeof(PupilContactDto)] = PupilFatherExample,

        [typeof(PupilContactListDto)] = $$"""
            {
              "pupilId": "{{ExamplePupilId}}",
              "items": [
                {{PupilFatherExample}},
                {
                  "id": "0192f0c4-9d4f-7c7c-a09e-4cfcbfe71817",
                  "role": "EmergencyPrimary",
                  "fullName": "Ngozi Okafor",
                  "relationship": "Aunt",
                  "phone": "+2348059876543",
                  "whatsappNumber": null,
                  "occupation": null,
                  "email": null,
                  "isPrimaryContact": false
                }
              ]
            }
            """,

        [typeof(PupilContactInput)] = """
            {
              "role": "Father",
              "fullName": "Emeka Okafor",
              "relationship": null,
              "phone": "08031234567",
              "whatsappNumber": "08031234567",
              "occupation": "Engineer",
              "email": null,
              "isPrimaryContact": true
            }
            """,

        [typeof(SavePupilContactsCommand)] = $$"""
            {
              "pupilId": "{{ExamplePupilId}}",
              "contacts": [
                { "role": "Father", "fullName": "Emeka Okafor", "relationship": null, "phone": "08031234567", "whatsappNumber": "08031234567", "occupation": "Engineer", "email": null, "isPrimaryContact": true },
                { "role": "EmergencyPrimary", "fullName": "Ngozi Okafor", "relationship": "Aunt", "phone": "08059876543", "whatsappNumber": null, "occupation": null, "email": null, "isPrimaryContact": false }
              ]
            }
            """,

        [typeof(PickupPersonDto)] = """
            { "id": "0192f0c4-ae50-7d8d-b1af-5d0dc0f82928", "fullName": "Chinedu Obi", "relationship": "Driver", "phone": "+2348021112222" }
            """,

        [typeof(PickupPersonListDto)] = $$"""
            {
              "pupilId": "{{ExamplePupilId}}",
              "items": [ { "id": "0192f0c4-ae50-7d8d-b1af-5d0dc0f82928", "fullName": "Chinedu Obi", "relationship": "Driver", "phone": "+2348021112222" } ]
            }
            """,

        [typeof(PickupPersonInput)] = """
            { "fullName": "Chinedu Obi", "relationship": "Driver", "phone": "08021112222" }
            """,

        [typeof(SavePickupPersonsCommand)] = $$"""
            { "pupilId": "{{ExamplePupilId}}", "persons": [ { "fullName": "Chinedu Obi", "relationship": "Driver", "phone": "08021112222" } ] }
            """,

        [typeof(BarredPersonDto)] = """
            { "id": "0192f0c4-bf61-7e9e-c2b0-6e1ed1093a39", "fullName": "John Doe", "details": "Court order dated 03/02/2026; office holds a copy." }
            """,

        [typeof(BarredPersonsDto)] = $$"""
            {
              "pupilId": "{{ExamplePupilId}}",
              "hasBarredPersons": true,
              "items": [ { "id": "0192f0c4-bf61-7e9e-c2b0-6e1ed1093a39", "fullName": "John Doe", "details": "Court order dated 03/02/2026; office holds a copy." } ]
            }
            """,

        [typeof(BarredPersonInput)] = """
            { "fullName": "John Doe", "details": "Court order dated 03/02/2026; office holds a copy." }
            """,

        [typeof(SaveBarredPersonsCommand)] = $$"""
            { "pupilId": "{{ExamplePupilId}}", "hasBarredPersons": false, "persons": [] }
            """,

        [typeof(PupilHealthDto)] = $$"""
            {
              "pupilId": "{{ExamplePupilId}}",
              "hasAllergy": true,
              "allergyDetails": "Peanuts: severe. EpiPen in the office.",
              "hasMedicalCondition": false,
              "medicalConditionDetails": null,
              "takesRegularMedication": false,
              "medicationDetails": null,
              "specialInstructions": "Vegetarian.",
              "preferredHospital": "St. Charles Borromeo Hospital, Onitsha",
              "hospitalPhone": "+2348037776666",
              "bloodGroup": "OPositive",
              "genotype": "AA"
            }
            """,

        [typeof(SavePupilHealthCommand)] = $$"""
            {
              "pupilId": "{{ExamplePupilId}}",
              "hasAllergy": true,
              "allergyDetails": "Peanuts: severe. EpiPen in the office.",
              "hasMedicalCondition": false,
              "medicalConditionDetails": null,
              "takesRegularMedication": false,
              "medicationDetails": null,
              "specialInstructions": "Vegetarian.",
              "preferredHospital": "St. Charles Borromeo Hospital, Onitsha",
              "hospitalPhone": "08037776666",
              "bloodGroup": "OPositive",
              "genotype": "AA"
            }
            """,

        [typeof(PupilDocumentDto)] = """
            { "documentType": "BirthCertificate", "otherLabel": null, "received": true, "receivedDate": "2026-09-14", "remarks": "Photocopy; original seen." }
            """,

        [typeof(PupilDocumentListDto)] = $$"""
            {
              "pupilId": "{{ExamplePupilId}}",
              "items": [
                { "documentType": "BirthCertificate", "otherLabel": null, "received": true, "receivedDate": "2026-09-14", "remarks": "Photocopy; original seen." },
                { "documentType": "PassportPhotograph", "otherLabel": null, "received": false, "receivedDate": null, "remarks": null },
                { "documentType": "PreviousSchoolResult", "otherLabel": null, "received": false, "receivedDate": null, "remarks": null },
                { "documentType": "TransferLetter", "otherLabel": null, "received": false, "receivedDate": null, "remarks": null },
                { "documentType": "Other", "otherLabel": null, "received": false, "receivedDate": null, "remarks": null }
              ]
            }
            """,

        [typeof(PupilDocumentInput)] = """
            { "received": true, "receivedDate": "2026-09-14", "remarks": "Photocopy; original seen.", "otherLabel": null }
            """,

        [typeof(CompletenessItemDto)] = """
            { "step": 5, "code": "health.unanswered", "message": "Answer all three health questions: allergy, medical condition, medication." }
            """,

        [typeof(AdmissionCompletenessDto)] = $$"""
            {
              "pupilId": "{{ExamplePupilId}}",
              "blocking": [ { "step": 5, "code": "health.unanswered", "message": "Answer all three health questions: allergy, medical condition, medication." } ],
              "chased": [ { "step": 7, "code": "documents.BirthCertificate", "message": "Birth certificate." } ],
              "chasedPercent": 90
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
        // Spec 6.5.7: enums used only as NULLABLE properties reach the transformer as Nullable<T>, whose component schema
        // the XML doc comment on T does not reach.
        [typeof(SchoolManagement.Domain.Pupils.BloodGroup?)] = "A blood group (spec 6.5.7): A+, A-, B+, B-, AB+, AB-, O+ or O-, spelled out. Free text is not accepted.",
        [typeof(SchoolManagement.Domain.Pupils.Genotype?)] = "A genotype (spec 6.5.7): AA, AS, SS, AC or SC.",

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

        // TASK-0005b stage B2: another framework type — see this dictionary's own remarks above.
        [typeof(Microsoft.AspNetCore.Http.IFormFile)] =
            "One uploaded file, sent as a multipart/form-data part. Verified server-side by its magic " +
            "bytes, never by a declared content type or file name (spec 9.6) — neither is part of this " +
            "contract.",

        // TASK-0005b stage C: the image-serving responses' body.
        [typeof(Stream)] =
            "The raw image bytes, PNG or JPEG as the response's Content-Type says. Streamed through this " +
            "privilege-checked endpoint, never from a public URL (spec 9.6).",
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
