using Helpdesk.Application.Agents;
using Helpdesk.Domain.Entities;

namespace Helpdesk.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);

    /// <returns>The names of the users with these ids; ids that match no user are left out.</returns>
    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);

    /// <returns>
    /// The users who pass <see cref="User.ActiveAgent"/>, the same rule as assignment, ordered by name with the
    /// Unicode root collation used for categories. Names are not unique, so equal names are ordered by id.
    /// </returns>
    Task<IReadOnlyList<AgentResponse>> ListActiveAgentsAsync(CancellationToken cancellationToken);

    void Add(User user);
}
