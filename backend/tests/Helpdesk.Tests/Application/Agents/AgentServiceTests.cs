using Helpdesk.Application.Agents;
using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Helpdesk.Tests.Application.Authentication;

namespace Helpdesk.Tests.Application.Agents;

public class AgentServiceTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly AgentService _agentService;

    public AgentServiceTests()
    {
        _agentService = new AgentService(_users);
    }

    [Fact]
    public async Task Admin_gets_the_agents_the_repository_lists()
    {
        var eva = SeedUser("Eva Agent", UserRole.Agent);
        var luis = SeedUser("Luis Agent", UserRole.Agent);

        var agents = await _agentService.ListAsync(UserRole.Admin, CancellationToken.None);

        Assert.Equal([new AgentResponse(eva.Id, "Eva Agent"), new AgentResponse(luis.Id, "Luis Agent")], agents);
        Assert.Equal(1, _users.AgentListings);
    }

    [Theory]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.Client)]
    public async Task Non_admin_is_forbidden_before_the_agents_are_listed(UserRole role)
    {
        SeedUser("Eva Agent", UserRole.Agent);

        await Assert.ThrowsAsync<TicketActionForbiddenException>(() =>
            _agentService.ListAsync(role, CancellationToken.None));

        Assert.Equal(0, _users.AgentListings);
    }

    private User SeedUser(string name, UserRole role)
    {
        var user = new User(name, $"{name.Replace(' ', '.')}@example.com", "hash", role);
        _users.Add(user);
        return user;
    }
}
