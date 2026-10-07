using FluentValidation;
using Helpdesk.Application.Abstractions;
using Helpdesk.Domain.Enums;

namespace Helpdesk.Application.Tickets;

internal static class TicketLimits
{
    // Matches the column size in TicketConfiguration.
    public const int TitleMaxLength = 200;

    // The column is unbounded text; this only caps the request size.
    public const int DescriptionMaxLength = 4000;

    public const int DefaultPageSize = 20;

    // Bounds the rows a single request can pull from the database.
    public const int MaxPageSize = 100;

    public const int SearchMaxLength = 200;
}

internal static class AllowedValues
{
    public static readonly string Priorities = string.Join(", ", Enum.GetNames<TicketPriority>());

    public static readonly string Statuses = string.Join(", ", Enum.GetNames<TicketStatus>());
}

public sealed class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketRequestValidator(ICategoryRepository categories)
    {
        RuleFor(request => request.Title)
            .NotEmpty()
            .MaximumLength(TicketLimits.TitleMaxLength);

        RuleFor(request => request.Description)
            .NotEmpty()
            .MaximumLength(TicketLimits.DescriptionMaxLength);

        // Compared by name, so numeric strings such as "1" are rejected instead of being mapped to an enum value.
        RuleFor(request => request.Priority)
            .NotEmpty()
            .IsEnumName(typeof(TicketPriority), caseSensitive: true)
            .WithMessage($"'Priority' must be one of: {AllowedValues.Priorities}.");

        // Stop skips the database lookup when the id is missing.
        RuleFor(request => request.CategoryId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MustAsync(categories.ExistsAsync)
            .WithMessage("The category does not exist.");
    }
}

/// <summary>Every parameter is optional; a missing one passes and gets its default in the service.</summary>
public sealed class ListTicketsRequestValidator : AbstractValidator<ListTicketsRequest>
{
    public ListTicketsRequestValidator()
    {
        // The repository skips (page - 1) * pageSize rows as an int; computed in long so it cannot wrap around.
        RuleFor(request => request.Page)
            .Cascade(CascadeMode.Stop)
            .GreaterThanOrEqualTo(1)
            .Must((request, page) => page is null || RowsToSkip(page.Value, request.PageSize) <= int.MaxValue)
            .WithMessage("'Page' is too large for the page size.");

        RuleFor(request => request.PageSize)
            .InclusiveBetween(1, TicketLimits.MaxPageSize);

        // Exact names only, as when creating a ticket.
        RuleFor(request => request.Status)
            .IsEnumName(typeof(TicketStatus), caseSensitive: true)
            .WithMessage($"'Status' must be one of: {AllowedValues.Statuses}.");

        RuleFor(request => request.Priority)
            .IsEnumName(typeof(TicketPriority), caseSensitive: true)
            .WithMessage($"'Priority' must be one of: {AllowedValues.Priorities}.");

        RuleFor(request => request.Search)
            .MaximumLength(TicketLimits.SearchMaxLength);
    }

    private static long RowsToSkip(int page, int? pageSize) =>
        (page - 1L) * (pageSize ?? TicketLimits.DefaultPageSize);
}
