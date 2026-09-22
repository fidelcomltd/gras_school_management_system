using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results;

/// <summary>Publishes an Approved result set to the parent portal (spec 6.7.9).</summary>
/// <param name="ResultSetId">The result set, from the route.</param>
public sealed record PublishResultSetCommand(Guid ResultSetId) : ICommand<Result<PublishResultSetResponse>>;

/// <summary>No body to validate; the route supplies the id.</summary>
internal sealed class PublishResultSetCommandValidator : AbstractValidator<PublishResultSetCommand>;

/// <summary>The published set.</summary>
/// <param name="ResultSet">State Published.</param>
/// <param name="PublishedAt">When it became visible to parents.</param>
/// <param name="RevisionNumber">1 on first publication; later revisions follow a withdrawal.</param>
public sealed record PublishResultSetResponse(ResultSetSummaryDto ResultSet, DateTimeOffset PublishedAt, int RevisionNumber);
