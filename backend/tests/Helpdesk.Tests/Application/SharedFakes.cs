using Helpdesk.Application.Abstractions;

namespace Helpdesk.Tests.Application;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Exception? ExceptionOnNextSave { get; set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (ExceptionOnNextSave is { } exception)
        {
            ExceptionOnNextSave = null;
            throw exception;
        }

        SaveCount++;
        return Task.CompletedTask;
    }
}
