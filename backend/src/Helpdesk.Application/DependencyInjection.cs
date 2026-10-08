using FluentValidation;
using Helpdesk.Application.Authentication;
using Helpdesk.Application.Tickets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Helpdesk.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        services.AddScoped<AuthService>();
        services.AddScoped<TicketService>();
        services.AddScoped<TicketCommentService>();
        services.AddScoped<TicketHistoryService>();

        return services;
    }
}
