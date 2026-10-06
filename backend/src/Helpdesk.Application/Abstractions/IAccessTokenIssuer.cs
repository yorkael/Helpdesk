using Helpdesk.Domain.Entities;

namespace Helpdesk.Application.Abstractions;

public interface IAccessTokenIssuer
{
    AccessToken Issue(User user, DateTimeOffset now);
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
