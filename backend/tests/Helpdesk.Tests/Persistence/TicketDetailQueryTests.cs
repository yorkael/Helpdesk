using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Helpdesk.Infrastructure;
using Helpdesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Tests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public class TicketDetailQueryTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private string _connectionString = null!;
    private Guid _generalId;

    public async Task InitializeAsync()
    {
        _connectionString = await fixture.CreateMigratedDatabaseAsync();

        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        _generalId = await context.Categories.Where(c => c.Name == "General").Select(c => c.Id).SingleAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(TicketVisibility.All, new[] { "client's", "agent's", "other agent's", "unassigned" })]
    [InlineData(TicketVisibility.CreatedByUser, new[] { "client's" })]
    [InlineData(TicketVisibility.AssignedToUserOrUnassigned, new[] { "agent's", "unassigned" })]
    public async Task Ticket_is_read_only_when_it_is_visible_to_the_user(TicketVisibility visibility, string[] expected)
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var otherClient = await SaveUserAsync("luis@example.com", UserRole.Client);
        var agent = await SaveUserAsync("eva@example.com", UserRole.Agent);
        var otherAgent = await SaveUserAsync("mario@example.com", UserRole.Agent);
        // The client reads under CreatedByUser; the agent under the other two (All ignores the user).
        var userId = visibility == TicketVisibility.CreatedByUser ? client.Id : agent.Id;
        var tickets = new Dictionary<string, Ticket>
        {
            ["client's"] = await SaveTicketAsync(client.Id, assignedToId: otherAgent.Id),
            ["agent's"] = await SaveTicketAsync(otherClient.Id, assignedToId: agent.Id),
            ["other agent's"] = await SaveTicketAsync(otherClient.Id, assignedToId: otherAgent.Id),
            ["unassigned"] = await SaveTicketAsync(otherClient.Id)
        };

        var visible = new List<string>();
        foreach (var (name, ticket) in tickets)
        {
            if (await GetDetailAsync(ticket.Id, visibility, userId) is not null)
            {
                visible.Add(name);
            }
        }

        Assert.Equal(expected, visible);
    }

    [Fact]
    public async Task Detail_carries_the_category_creator_and_assignee_names()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client, "Ana Ruiz");
        var agent = await SaveUserAsync("luis@example.com", UserRole.Agent, "Luis Vega");
        var ticket = await SaveTicketAsync(client.Id, assignedToId: agent.Id);

        var detail = (await GetDetailAsync(ticket.Id, TicketVisibility.All, Guid.Empty))!;

        Assert.Equal(
            (_generalId, "General", client.Id, "Ana Ruiz", agent.Id, "Luis Vega"),
            (detail.CategoryId, detail.CategoryName, detail.CreatedById, detail.CreatedByName, detail.AssignedToId,
                detail.AssignedToName));
    }

    [Fact]
    public async Task Detail_carries_the_title_and_the_full_description()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var ticket = await SaveTicketAsync(client.Id, "Printer offline", "It shows error 42 after every restart.");

        var detail = (await GetDetailAsync(ticket.Id, TicketVisibility.All, Guid.Empty))!;

        Assert.Equal(
            ("Printer offline", "It shows error 42 after every restart."),
            (detail.Title, detail.Description));
    }

    [Fact]
    public async Task Unassigned_ticket_has_no_assignee_id_or_name()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client, "Ana Ruiz");
        var ticket = await SaveTicketAsync(client.Id);

        var detail = (await GetDetailAsync(ticket.Id, TicketVisibility.All, Guid.Empty))!;

        Assert.Equal("Ana Ruiz", detail.CreatedByName);
        Assert.Null(detail.AssignedToId);
        Assert.Null(detail.AssignedToName);
    }

    [Fact]
    public async Task Unknown_ticket_id_is_not_found()
    {
        var detail = await GetDetailAsync(Guid.NewGuid(), TicketVisibility.All, Guid.NewGuid());

        Assert.Null(detail);
    }

    [Fact]
    public async Task Reading_a_ticket_runs_one_query_and_tracks_nothing()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var agent = await SaveUserAsync("luis@example.com", UserRole.Agent);
        var ticket = await SaveTicketAsync(client.Id, assignedToId: agent.Id);

        var recorder = new CommandRecorder();
        var services = new ServiceCollection()
            .AddInfrastructure(TestConfiguration.CreateConfiguration(TestConfiguration.CreateSettings(_connectionString)))
            .ConfigureDbContext<HelpdeskDbContext>(options => options.AddInterceptors(recorder))
            .BuildServiceProvider()
            .CreateScope()
            .ServiceProvider;

        var detail = await services.GetRequiredService<ITicketRepository>()
            .GetDetailAsync(ticket.Id, TicketVisibility.All, Guid.Empty, CancellationToken.None);

        Assert.NotNull(detail);
        // The category, creator and assignee names come from joins in the same query.
        Assert.Single(recorder.CommandTexts);
        Assert.Empty(services.GetRequiredService<HelpdeskDbContext>().ChangeTracker.Entries());
    }

    private async Task<TicketDetailResponse?> GetDetailAsync(Guid id, TicketVisibility visibility, Guid userId) =>
        await TestConfiguration.CreateInfrastructureServices(_connectionString)
            .GetRequiredService<ITicketRepository>()
            .GetDetailAsync(id, visibility, userId, CancellationToken.None);

    private async Task<User> SaveUserAsync(string email, UserRole role, string name = "Test User")
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var user = new User(name, email, "hash", role);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private async Task<Ticket> SaveTicketAsync(
        Guid creatorId,
        string title = "Printer offline",
        string description = "It shows error 42.",
        Guid? assignedToId = null)
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var ticket = new Ticket(title, description, TicketPriority.Medium, _generalId, creatorId, UtcNow);
        context.Tickets.Add(ticket);

        // Set directly instead of through Assign, so a test can give any agent a ticket in one step.
        context.Entry(ticket).Property(t => t.AssignedToId).CurrentValue = assignedToId;

        await context.SaveChangesAsync();
        return ticket;
    }
}
