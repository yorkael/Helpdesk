using System.Data.Common;
using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Helpdesk.Infrastructure;
using Helpdesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Tests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public class TicketListQueryTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static readonly TicketListQuery EveryTicket =
        new(TicketVisibility.All, Guid.Empty, null, null, null, null, null, 1, 20);

    private string _connectionString = null!;
    private Guid _generalId;
    private Guid _billingId;

    public async Task InitializeAsync()
    {
        _connectionString = await fixture.CreateMigratedDatabaseAsync();

        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        _generalId = await context.Categories.Where(c => c.Name == "General").Select(c => c.Id).SingleAsync();
        _billingId = await context.Categories.Where(c => c.Name == "Billing").Select(c => c.Id).SingleAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Creator_visibility_returns_only_the_users_own_tickets()
    {
        var ana = await SaveUserAsync("ana@example.com", UserRole.Client);
        var luis = await SaveUserAsync("luis@example.com", UserRole.Client);
        var first = await SaveTicketAsync(ana.Id);
        var second = await SaveTicketAsync(ana.Id);
        await SaveTicketAsync(luis.Id);

        var page = await ListAsync(EveryTicket with { Visibility = TicketVisibility.CreatedByUser, UserId = ana.Id });

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { first.Id, second.Id }.Order(), page.Items.Select(item => item.Id).Order());
    }

    [Fact]
    public async Task Agent_visibility_returns_tickets_assigned_to_the_agent_or_unassigned()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var agent = await SaveUserAsync("luis@example.com", UserRole.Agent);
        var otherAgent = await SaveUserAsync("eva@example.com", UserRole.Agent);
        var assigned = await SaveTicketAsync(client.Id, assignedToId: agent.Id);
        var unassigned = await SaveTicketAsync(client.Id);
        await SaveTicketAsync(client.Id, assignedToId: otherAgent.Id);

        var page = await ListAsync(EveryTicket with
        {
            Visibility = TicketVisibility.AssignedToUserOrUnassigned,
            UserId = agent.Id
        });

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { assigned.Id, unassigned.Id }.Order(), page.Items.Select(item => item.Id).Order());
    }

    [Fact]
    public async Task All_visibility_returns_every_ticket()
    {
        var ana = await SaveUserAsync("ana@example.com", UserRole.Client);
        var luis = await SaveUserAsync("luis@example.com", UserRole.Client);
        var agent = await SaveUserAsync("eva@example.com", UserRole.Agent);
        await SaveTicketAsync(ana.Id);
        await SaveTicketAsync(luis.Id, assignedToId: agent.Id);

        var page = await ListAsync(EveryTicket);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public async Task Status_filter_keeps_only_that_status()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var resolved = await SaveTicketAsync(client.Id, status: TicketStatus.Resolved);
        await SaveTicketAsync(client.Id);

        var page = await ListAsync(EveryTicket with { Status = TicketStatus.Resolved });

        Assert.Equal(resolved.Id, Assert.Single(page.Items).Id);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task Priority_filter_keeps_only_that_priority()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var urgent = await SaveTicketAsync(client.Id, priority: TicketPriority.Urgent);
        await SaveTicketAsync(client.Id, priority: TicketPriority.Low);

        var page = await ListAsync(EveryTicket with { Priority = TicketPriority.Urgent });

        Assert.Equal(urgent.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task Category_filter_keeps_only_that_category()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var billing = await SaveTicketAsync(client.Id, categoryId: _billingId);
        await SaveTicketAsync(client.Id);

        var page = await ListAsync(EveryTicket with { CategoryId = _billingId });

        Assert.Equal(billing.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task Assignee_filter_keeps_only_tickets_assigned_to_that_agent()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var agent = await SaveUserAsync("luis@example.com", UserRole.Agent);
        var otherAgent = await SaveUserAsync("eva@example.com", UserRole.Agent);
        var assigned = await SaveTicketAsync(client.Id, assignedToId: agent.Id);
        await SaveTicketAsync(client.Id, assignedToId: otherAgent.Id);
        await SaveTicketAsync(client.Id);

        var page = await ListAsync(EveryTicket with { AssignedToId = agent.Id });

        Assert.Equal(assigned.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task Filters_combine_so_a_ticket_must_match_all_of_them()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var match = await SaveTicketAsync(client.Id, priority: TicketPriority.High, categoryId: _billingId);
        await SaveTicketAsync(client.Id, priority: TicketPriority.High);
        await SaveTicketAsync(client.Id, priority: TicketPriority.Low, categoryId: _billingId);

        var page = await ListAsync(EveryTicket with { Priority = TicketPriority.High, CategoryId = _billingId });

        Assert.Equal(match.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task Filters_never_widen_the_visibility()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var agent = await SaveUserAsync("luis@example.com", UserRole.Agent);
        var otherAgent = await SaveUserAsync("eva@example.com", UserRole.Agent);
        await SaveTicketAsync(client.Id, assignedToId: otherAgent.Id);

        var page = await ListAsync(EveryTicket with
        {
            Visibility = TicketVisibility.AssignedToUserOrUnassigned,
            UserId = agent.Id,
            AssignedToId = otherAgent.Id
        });

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Theory]
    [InlineData("PRINTER")]
    [InlineData("error 42")]
    public async Task Search_matches_the_title_or_the_description_ignoring_case(string search)
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var match = await SaveTicketAsync(client.Id, title: "Printer offline", description: "It shows Error 42.");
        await SaveTicketAsync(client.Id, title: "Cannot log in", description: "Password rejected.");

        var page = await ListAsync(EveryTicket with { Search = search });

        Assert.Equal(match.Id, Assert.Single(page.Items).Id);
    }

    [Theory]
    [InlineData("100%", "CPU at 100%", "CPU at 1000")]
    [InlineData("a_b", "File a_b.txt", "File axb.txt")]
    [InlineData(@"a\b", @"Path a\b", "Path ab")]
    public async Task Search_treats_like_wildcards_as_literal_text(string search, string matching, string notMatching)
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var match = await SaveTicketAsync(client.Id, title: matching);
        await SaveTicketAsync(client.Id, title: notMatching);

        var page = await ListAsync(EveryTicket with { Search = search });

        Assert.Equal(match.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task Tickets_are_ordered_newest_first_with_the_id_breaking_ties()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var oldest = await SaveTicketAsync(client.Id, createdAt: UtcNow.AddHours(-2));
        var newest = await SaveTicketAsync(client.Id, createdAt: UtcNow);
        var tiedA = await SaveTicketAsync(client.Id, createdAt: UtcNow.AddHours(-1));
        var tiedB = await SaveTicketAsync(client.Id, createdAt: UtcNow.AddHours(-1));

        var page = await ListAsync(EveryTicket);

        // PostgreSQL orders uuids by their bytes, which is the ordinal order of their text form.
        var tied = new[] { tiedA.Id, tiedB.Id }.OrderByDescending(id => id.ToString(), StringComparer.Ordinal);
        Assert.Equal([newest.Id, .. tied, oldest.Id], page.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Paging_through_tickets_created_at_the_same_instant_returns_each_one_once()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add((await SaveTicketAsync(client.Id, createdAt: UtcNow)).Id);
        }

        var pages = new List<PagedResponse<TicketListItem>>();
        for (var number = 1; number <= 4; number++)
        {
            pages.Add(await ListAsync(EveryTicket with { Page = number, PageSize = 2 }));
        }

        Assert.Equal([2, 2, 1, 0], pages.Select(page => page.Items.Count));
        Assert.All(pages, page => Assert.Equal(5, page.TotalCount));
        Assert.Equal(
            ids.OrderByDescending(id => id.ToString(), StringComparer.Ordinal),
            pages.SelectMany(page => page.Items).Select(item => item.Id));
        Assert.Equal([1, 2, 3, 4], pages.Select(page => page.Page));
        Assert.All(pages, page => Assert.Equal(2, page.PageSize));
    }

    [Fact]
    public async Task Items_carry_the_category_creator_and_assignee_names()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client, "Ana Ruiz");
        var agent = await SaveUserAsync("luis@example.com", UserRole.Agent, "Luis Vega");
        var assigned = await SaveTicketAsync(
            client.Id,
            title: "Invoice missing",
            priority: TicketPriority.High,
            categoryId: _billingId,
            createdAt: UtcNow,
            status: TicketStatus.InProgress,
            assignedToId: agent.Id);
        var unassigned = await SaveTicketAsync(client.Id, createdAt: UtcNow.AddMinutes(-1));

        var page = await ListAsync(EveryTicket);

        Assert.Equal(
            [
                new TicketListItem(assigned.Id, "Invoice missing", "InProgress", "High", _billingId, "Billing",
                    client.Id, "Ana Ruiz", agent.Id, "Luis Vega", UtcNow),
                new TicketListItem(unassigned.Id, "Printer offline", "Open", "Medium", _generalId, "General",
                    client.Id, "Ana Ruiz", null, null, UtcNow.AddMinutes(-1))
            ],
            page.Items);
    }

    [Fact]
    public async Task Listing_runs_two_queries_tracks_nothing_and_never_reads_descriptions()
    {
        var ana = await SaveUserAsync("ana@example.com", UserRole.Client);
        var luis = await SaveUserAsync("luis@example.com", UserRole.Client);
        var agent = await SaveUserAsync("eva@example.com", UserRole.Agent);
        await SaveTicketAsync(ana.Id, assignedToId: agent.Id);
        await SaveTicketAsync(luis.Id, categoryId: _billingId);
        await SaveTicketAsync(ana.Id);

        var recorder = new CommandRecorder();
        var services = new ServiceCollection()
            .AddInfrastructure(TestConfiguration.CreateConfiguration(TestConfiguration.CreateSettings(_connectionString)))
            .ConfigureDbContext<HelpdeskDbContext>(options => options.AddInterceptors(recorder))
            .BuildServiceProvider()
            .CreateScope()
            .ServiceProvider;

        var page = await services.GetRequiredService<ITicketRepository>().ListAsync(EveryTicket, CancellationToken.None);

        Assert.Equal(3, page.Items.Count);
        // One count and one page, however many tickets, categories and users are involved.
        Assert.Equal(2, recorder.CommandTexts.Count);
        Assert.All(recorder.CommandTexts, text =>
        {
            Assert.DoesNotContain("description", text);
            Assert.DoesNotContain("comments", text);
        });
        Assert.Empty(services.GetRequiredService<HelpdeskDbContext>().ChangeTracker.Entries());
    }

    private async Task<PagedResponse<TicketListItem>> ListAsync(TicketListQuery query) =>
        await TestConfiguration.CreateInfrastructureServices(_connectionString)
            .GetRequiredService<ITicketRepository>()
            .ListAsync(query, CancellationToken.None);

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
        TicketPriority priority = TicketPriority.Medium,
        Guid? categoryId = null,
        DateTimeOffset? createdAt = null,
        TicketStatus status = TicketStatus.Open,
        Guid? assignedToId = null)
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var ticket = new Ticket(title, description, priority, categoryId ?? _generalId, creatorId, createdAt ?? UtcNow);
        context.Tickets.Add(ticket);

        // Ticket cannot be assigned or change status until HU-6, so the columns are set directly.
        context.Entry(ticket).Property(t => t.Status).CurrentValue = status;
        context.Entry(ticket).Property(t => t.AssignedToId).CurrentValue = assignedToId;

        await context.SaveChangesAsync();
        return ticket;
    }

    private sealed class CommandRecorder : DbCommandInterceptor
    {
        public List<string> CommandTexts { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            CommandTexts.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            CommandTexts.Add(command.CommandText);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
