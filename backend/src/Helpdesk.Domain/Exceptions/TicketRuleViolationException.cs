namespace Helpdesk.Domain.Exceptions;

/// <summary>
/// A valid request that the ticket's current state does not allow, such as a status change missing from the
/// transition table. Its own type, so the API can answer it as a conflict without catching every
/// <see cref="InvalidOperationException"/>.
/// </summary>
public sealed class TicketRuleViolationException(string message) : Exception(message);
