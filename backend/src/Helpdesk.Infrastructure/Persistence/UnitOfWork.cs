using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Authentication;
using Helpdesk.Application.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Helpdesk.Infrastructure.Persistence;

internal sealed class UnitOfWork(HelpdeskDbContext context) : IUnitOfWork
{
    private const string UserEmailIndex = "ix_users_email";

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: UserEmailIndex
        })
        {
            throw new EmailAlreadyRegisteredException(exception);
        }
    }
}
