using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Helpdesk.Tests.Persistence;

internal sealed class CommandRecorder : DbCommandInterceptor
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
