namespace Helpdesk.Application.Tickets;

/// <summary>
/// Has no author on purpose: the author comes from the access token, and unknown JSON members are ignored.
/// <see cref="IsInternal"/> is nullable so a missing value is a validation error instead of defaulting to public.
/// </summary>
public sealed record CreateCommentRequest(string Content, bool? IsInternal);

/// <summary>Carries the author's name but never their email.</summary>
public sealed record CommentResponse(
    Guid Id,
    Guid TicketId,
    Guid AuthorId,
    string AuthorName,
    string Content,
    bool IsInternal,
    DateTimeOffset CreatedAt);

/// <summary>Which comments of a ticket a user may read; decided from their role, applied by the repository.</summary>
public enum CommentVisibility
{
    PublicOnly,
    IncludeInternal
}
