using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Agents;
using Helpdesk.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(HelpdeskDbContext context) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        context.Users.SingleOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        context.Users.AnyAsync(user => user.Email == normalizedEmail, cancellationToken);

    /// <summary>One query for all the ids. Only id and name are read, so no email leaves the database.</summary>
    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken) =>
        await context.Users
            .Where(user => ids.Contains(user.Id))
            .Select(user => new { user.Id, user.Name })
            .ToDictionaryAsync(user => user.Id, user => user.Name, cancellationToken);

    /// <summary>Only id and name are read; the projection is not tracked.</summary>
    public async Task<IReadOnlyList<AgentResponse>> ListActiveAgentsAsync(CancellationToken cancellationToken) =>
        await context.Users
            .Where(User.ActiveAgent)
            .OrderBy(user => EF.Functions.Collate(user.Name, Collations.NameOrder))
            .ThenBy(user => user.Id)
            .Select(user => new AgentResponse(user.Id, user.Name))
            .ToListAsync(cancellationToken);

    public void Add(User user) => context.Users.Add(user);
}
