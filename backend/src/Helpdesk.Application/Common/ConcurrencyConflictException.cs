namespace Helpdesk.Application.Common;

public sealed class ConcurrencyConflictException(Exception innerException)
    : Exception("The data was changed by another operation.", innerException);
