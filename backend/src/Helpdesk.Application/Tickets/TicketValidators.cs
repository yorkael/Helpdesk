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
}

public sealed class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    private static readonly string AllowedPriorities = string.Join(", ", Enum.GetNames<TicketPriority>());

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
            .WithMessage($"'Priority' must be one of: {AllowedPriorities}.");

        // Stop skips the database lookup when the id is missing.
        RuleFor(request => request.CategoryId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MustAsync(categories.ExistsAsync)
            .WithMessage("The category does not exist.");
    }
}
