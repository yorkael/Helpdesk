using FluentValidation.TestHelper;
using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Helpdesk.Tests.Application.Authentication;

namespace Helpdesk.Tests.Application.Tickets;

public class AssignTicketValidatorTests
{
    private const string Message = "The assignee must be an active agent.";

    private readonly InMemoryUserRepository _users = new();
    private readonly AssignTicketRequestValidator _validator;

    public AssignTicketValidatorTests()
    {
        _validator = new AssignTicketRequestValidator(_users);
    }

    [Fact]
    public async Task Active_agent_passes()
    {
        var agent = SeedUser(UserRole.Agent);

        var result = await _validator.TestValidateAsync(new AssignTicketRequest(agent.Id));

        result.ShouldNotHaveAnyValidationErrors();
    }

    // Without Stop, the active-agent check would also run and add a second error.
    [Fact]
    public async Task Missing_assignee_is_reported_once()
    {
        var result = await _validator.TestValidateAsync(new AssignTicketRequest(Guid.Empty));

        Assert.Single(result.ShouldHaveValidationErrorFor(request => request.AssigneeId));
    }

    [Fact]
    public async Task Unknown_user_is_rejected()
    {
        var result = await _validator.TestValidateAsync(new AssignTicketRequest(Guid.NewGuid()));

        result.ShouldHaveValidationErrorFor(request => request.AssigneeId).WithErrorMessage(Message);
    }

    [Fact]
    public async Task Inactive_agent_is_rejected()
    {
        var agent = SeedUser(UserRole.Agent);
        TestUsers.Deactivate(agent);

        var result = await _validator.TestValidateAsync(new AssignTicketRequest(agent.Id));

        result.ShouldHaveValidationErrorFor(request => request.AssigneeId).WithErrorMessage(Message);
    }

    [Theory]
    [InlineData(UserRole.Client)]
    [InlineData(UserRole.Admin)]
    public async Task User_who_is_not_an_agent_is_rejected(UserRole role)
    {
        var user = SeedUser(role);

        var result = await _validator.TestValidateAsync(new AssignTicketRequest(user.Id));

        result.ShouldHaveValidationErrorFor(request => request.AssigneeId).WithErrorMessage(Message);
    }

    private User SeedUser(UserRole role)
    {
        var user = new User("Eva Ruiz", $"{role}@example.com".ToLowerInvariant(), "hash", role);
        _users.Add(user);
        return user;
    }
}
