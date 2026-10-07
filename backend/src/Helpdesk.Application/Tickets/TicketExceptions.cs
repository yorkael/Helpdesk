namespace Helpdesk.Application.Tickets;

/// <summary>
/// Same answer for a ticket that does not exist and one outside what the user may see,
/// so the response does not reveal which tickets exist.
/// </summary>
public sealed class TicketNotFoundException() : Exception("The ticket was not found.");

/// <summary>The user may see the ticket but their role does not allow this change.</summary>
public sealed class TicketActionForbiddenException(string message) : Exception(message);
