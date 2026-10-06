namespace Helpdesk.Application.Abstractions;

public interface IUnitOfWork
{
    /// <exception cref="Common.ConcurrencyConflictException">A row changed since it was read.</exception>
    /// <exception cref="Authentication.EmailAlreadyRegisteredException">Another user took the email first.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
