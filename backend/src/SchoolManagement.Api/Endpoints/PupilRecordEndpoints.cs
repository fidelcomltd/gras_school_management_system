using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Http;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Pupils.Records;

namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// The admission form's per-pupil sections (spec 6.5.5 to 6.5.8, 6.5.12): contacts, the pickup and barred lists, health,
/// the document checklist, and the admission completeness report.
/// </summary>
/// <remarks>
/// PRIVILEGES ARE ENFORCED IN THE HANDLER, NOT THE ROUTE — same pattern as <c>GET /pupils/{id}</c>. A route-level pupil
/// scope resolves the pupil's arm from its open enrolment, and a pending admission has none, so the route check would
/// refuse even a school-wide grant. Each handler applies <c>PupilRecordAccess</c>: school-wide grants cover every pupil,
/// arm-scoped grants only pupils enrolled in their arms. The privilege each route needs is named in its description.
/// </remarks>
public sealed class PupilRecordEndpoints : IEndpointModule
{
    private const string Tag = "Pupil records";

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var pupils = endpoints.MapGroup("/pupils/{pupilId:guid}").WithTags(Tag);

        Read(pupils.MapGet("/contacts", async (Guid pupilId, ISender sender, CancellationToken cancellationToken) =>
                (await sender.SendAsync(new GetPupilContactsQuery(pupilId), cancellationToken)).Match(TypedResults.Ok)),
            "GetPupilContacts", "Read a pupil's contacts",
            "Spec 6.5.5: father, mother, guardian and the two emergency contacts, as recorded. Needs `contact.view` over the pupil.")
            .Produces<PupilContactListDto>(StatusCodes.Status200OK);

        Write(pupils.MapPut("/contacts", async (Guid pupilId, SavePupilContactsCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.SendAsync(command with { PupilId = pupilId }, cancellationToken)).Match(TypedResults.Ok)),
            "SavePupilContacts", "Save a pupil's contacts",
            "Spec 6.5.5: the whole set, one per role; a role left out is removed. Phones in either Nigerian form are stored as " +
            "+234. Exactly one father, mother or guardian is the primary contact whenever any is recorded. 409 " +
            "`contact.last_responsible_adult` when an active pupil would be left with no father, mother or guardian. Needs " +
            "`contact.update` over the pupil.")
            .Produces<PupilContactListDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict);

        Read(pupils.MapGet("/pickup-persons", async (Guid pupilId, ISender sender, CancellationToken cancellationToken) =>
                (await sender.SendAsync(new GetPickupPersonsQuery(pupilId), cancellationToken)).Match(TypedResults.Ok)),
            "GetPickupPersons", "Read the authorised pickup list",
            "Spec 6.5.6: in the parent's order. Needs `contact.view` over the pupil.")
            .Produces<PickupPersonListDto>(StatusCodes.Status200OK);

        Write(pupils.MapPut("/pickup-persons", async (Guid pupilId, SavePickupPersonsCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.SendAsync(command with { PupilId = pupilId }, cancellationToken)).Match(TypedResults.Ok)),
            "SavePickupPersons", "Save the authorised pickup list",
            "Spec 6.5.6: the whole list, replaced; empty is allowed. Needs `contact.update` over the pupil.")
            .Produces<PickupPersonListDto>(StatusCodes.Status200OK);

        Read(pupils.MapGet("/barred-persons", async (Guid pupilId, ISender sender, CancellationToken cancellationToken) =>
                (await sender.SendAsync(new ReadBarredPersonsCommand(pupilId), cancellationToken)).Match(TypedResults.Ok)),
            "GetBarredPersons", "Read who must not collect the pupil",
            "Spec 6.5.6: the most sensitive data in the system. Needs `pupil.safeguarding.view` over the pupil, and every read " +
            "writes an audit event. `hasBarredPersons` null means the question has not been asked.")
            .Produces<BarredPersonsDto>(StatusCodes.Status200OK);

        Write(pupils.MapPut("/barred-persons", async (Guid pupilId, SaveBarredPersonsCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.SendAsync(command with { PupilId = pupilId }, cancellationToken)).Match(TypedResults.Ok)),
            "SaveBarredPersons", "Answer the barred-persons question",
            "Spec 6.5.6: the explicit yes or no, with at least one name when yes and none when no. Needs " +
            "`pupil.safeguarding.update` over the pupil. Names never enter the audit log.")
            .Produces<BarredPersonsDto>(StatusCodes.Status200OK);

        Read(pupils.MapGet("/health", async (Guid pupilId, ISender sender, CancellationToken cancellationToken) =>
                (await sender.SendAsync(new ReadPupilHealthCommand(pupilId), cancellationToken)).Match(TypedResults.Ok)),
            "GetPupilHealth", "Read a pupil's health and safety section",
            "Spec 6.5.7: section F. Needs `pupil.safeguarding.view` over the pupil; every read writes an audit event. A null " +
            "answer means the question has not been asked, which is not the same as No.")
            .Produces<PupilHealthDto>(StatusCodes.Status200OK);

        Write(pupils.MapPut("/health", async (Guid pupilId, SavePupilHealthCommand command, ISender sender, CancellationToken cancellationToken) =>
                (await sender.SendAsync(command with { PupilId = pupilId }, cancellationToken)).Match(TypedResults.Ok)),
            "SavePupilHealth", "Save a pupil's health and safety section",
            "Spec 6.5.7: the whole of section F. A detail is required where its question is Yes and cleared where No. " +
            "Unanswered questions may stay null until approval. Needs `pupil.safeguarding.update` over the pupil.")
            .Produces<PupilHealthDto>(StatusCodes.Status200OK);

        Read(pupils.MapGet("/documents", async (Guid pupilId, ISender sender, CancellationToken cancellationToken) =>
                (await sender.SendAsync(new GetPupilDocumentsQuery(pupilId), cancellationToken)).Match(TypedResults.Ok)),
            "GetPupilDocuments", "Read the admission document checklist",
            "Spec 6.5.8: always five rows in form order; a row never ticked reads as not received. Needs `pupil.view`.")
            .Produces<PupilDocumentListDto>(StatusCodes.Status200OK);

        Write(pupils.MapPut("/documents/{documentType}", async (
                Guid pupilId, Domain.Pupils.PupilDocumentType documentType, PupilDocumentInput body, ISender sender, CancellationToken cancellationToken) =>
                (await sender.SendAsync(
                    new SavePupilDocumentCommand(pupilId, documentType, body.Received, body.ReceivedDate, body.Remarks, body.OtherLabel), cancellationToken))
                .Match(TypedResults.Ok)),
            "SavePupilDocument", "Tick or untick one checklist document",
            "Spec 6.5.8: the received date defaults to today; the Other row needs a label when ticked. A ticked row needs no " +
            "file: the school keeps paper. Needs `pupil.document.manage` over the pupil.")
            .Produces<PupilDocumentListDto>(StatusCodes.Status200OK);

        Read(endpoints.MapGet("/admissions/{id:guid}/completeness", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.SendAsync(new GetAdmissionCompletenessQuery(id), cancellationToken)).Match(TypedResults.Ok)),
            "GetAdmissionCompleteness", "What an admission still lacks",
            "Spec 6.5.12: `blocking` items stop approval (contacts, the barred-persons answer, the three health answers, the " +
            "declaration, a required assessment's outcome); `chased` items are tracked after approval. Each is keyed to its " +
            "admission-flow step. Reports whether health is answered, never what it says. Needs `pupil.view`.")
            .WithTags(Tag)
            .Produces<AdmissionCompletenessDto>(StatusCodes.Status200OK);

        endpoints.MapGet("/reports/incomplete-records", async (
                [FromQuery(Name = "armId")] string? armId, ISender sender, CancellationToken cancellationToken) =>
                (await sender.SendAsync(new GetIncompleteRecordsQuery(armId), cancellationToken)).Match(TypedResults.Ok))
            // The handler checks report.view: a route check with no arm to resolve would refuse an arm-restricted grant.
            .RequireAuthenticatedCaller()
            .WithTags(Tag)
            .WithName("GetIncompleteRecordsReport")
            .WithSummary("Active pupils with records still to chase")
            .WithDescription(
                "Spec 6.5.12: every ACTIVE pupil in the active session with something missing, by arm then surname, each with " +
                "the `required` items still missing (possible after a bulk import or a health override) and the `chased` " +
                "items, plus `counts` of each gap across the report. `report.view`; an arm-restricted grant sees only its " +
                "arms, and `armId` outside them is 403. Codes only: never what a health or barred-person answer says. Empty " +
                "when no session is active. Bounded by the active roll, so not paginated.")
            .Produces<IncompleteRecordsReportDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    private static RouteHandlerBuilder Read(RouteHandlerBuilder route, string name, string summary, string description) =>
        route.RequireAuthenticatedCaller()
            .WithName(name)
            .WithSummary(summary)
            .WithDescription(description)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static RouteHandlerBuilder Write(RouteHandlerBuilder route, string name, string summary, string description) =>
        Read(route, name, summary, description)
            .RequireCsrfToken()
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
}
